using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Clientes;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Clientes;

public sealed class ClientesService(ApplicationDbContext db, ITenantProvider tenant)
{
    public async Task<IReadOnlyList<ClienteResponse>> ListarAsync(
        string? q,
        CancellationToken cancellationToken)
    {
        var query = db.Clientes.AsNoTracking().AsQueryable();
        var filtro = q?.Trim() ?? string.Empty;
        if (filtro.Length > 0)
        {
            var like = filtro.ToLower();
            query = query.Where(c =>
                c.Nombre.ToLower().Contains(like)
                || (c.NumeroDocumento != null && c.NumeroDocumento.Contains(filtro))
                || (c.Telefono != null && c.Telefono.Contains(filtro)));
        }

        var items = await query
            .OrderByDescending(c => c.TipoDocumento == TipoDocumentoIdentidad.SIN_DOCUMENTO)
            .ThenBy(c => c.Nombre)
            .Take(200)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<ClienteResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return cliente is null ? null : Map(cliente);
    }

    public async Task<ClienteResponse> CrearAsync(UpsertClienteRequest request, CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        var cliente = request.EsPublicoGeneral
            ? await ResolverPublicoGeneralAsync(ahora, cancellationToken)
            : await CrearOActualizarPorDocumentoAsync(request, ahora, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return Map(cliente);
    }

    public async Task<ClienteResponse?> ActualizarAsync(
        Guid id,
        UpsertClienteRequest request,
        CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cliente is null)
        {
            return null;
        }

        var (tipo, numero, nombre) = DocumentoIdentidad.NormalizarCliente(
            request.Nombre,
            request.TipoDocumento,
            request.NumeroDocumento,
            request.EsPublicoGeneral);
        await AsegurarDocumentoLibreAsync(numero, id, cancellationToken);

        cliente.Nombre = nombre;
        cliente.Telefono = TextoOpcional(request.Telefono);
        cliente.PuntoEntregaPreferido = TextoOpcionalLargo(request.PuntoEntregaPreferido, 80);
        cliente.CanalContacto = request.CanalContacto;
        cliente.ContactoReferencia = TextoOpcionalLargo(request.ContactoReferencia, 160);
        cliente.TipoDocumento = tipo;
        cliente.NumeroDocumento = numero;
        await db.SaveChangesAsync(cancellationToken);
        return Map(cliente);
    }

    public async Task<Cliente> ResolverPublicoGeneralAsync(
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var existente = await db.Clientes.FirstOrDefaultAsync(
            c => c.NumeroDocumento == DocumentoIdentidad.NumeroSinDocumento
                && (c.TipoDocumento == TipoDocumentoIdentidad.SIN_DOCUMENTO
                    || c.TipoDocumento == TipoDocumentoIdentidad.DNI)
                && (c.Nombre == DocumentoIdentidad.NombreClienteVarios
                    || c.Nombre == DocumentoIdentidad.NombrePublicoGeneral),
            cancellationToken);
        if (existente is not null)
        {
            existente.TipoDocumento = TipoDocumentoIdentidad.SIN_DOCUMENTO;
            existente.NumeroDocumento = DocumentoIdentidad.NumeroSinDocumento;
            if (string.IsNullOrWhiteSpace(existente.Nombre))
            {
                existente.Nombre = DocumentoIdentidad.NombreClienteVarios;
            }

            return existente;
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Nombre = DocumentoIdentidad.NombreClienteVarios,
            TipoDocumento = TipoDocumentoIdentidad.SIN_DOCUMENTO,
            NumeroDocumento = DocumentoIdentidad.NumeroSinDocumento,
            FechaCreacion = ahora
        };
        db.Clientes.Add(cliente);
        return cliente;
    }

    private async Task<Cliente> CrearOActualizarPorDocumentoAsync(
        UpsertClienteRequest request,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var (tipo, numero, nombre) = DocumentoIdentidad.NormalizarCliente(
            request.Nombre,
            request.TipoDocumento,
            request.NumeroDocumento,
            request.EsPublicoGeneral);

        var existente = string.IsNullOrWhiteSpace(numero) || numero == DocumentoIdentidad.NumeroSinDocumento
            ? null
            : await db.Clientes.FirstOrDefaultAsync(c => c.NumeroDocumento == numero, cancellationToken);
        if (existente is not null)
        {
            existente.Nombre = nombre;
            existente.Telefono = TextoOpcional(request.Telefono) ?? existente.Telefono;
            existente.PuntoEntregaPreferido =
                TextoOpcionalLargo(request.PuntoEntregaPreferido, 80) ?? existente.PuntoEntregaPreferido;
            existente.CanalContacto = request.CanalContacto ?? existente.CanalContacto;
            existente.ContactoReferencia =
                TextoOpcionalLargo(request.ContactoReferencia, 160) ?? existente.ContactoReferencia;
            existente.TipoDocumento = tipo;
            existente.NumeroDocumento = numero;
            return existente;
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Nombre = nombre,
            Telefono = TextoOpcional(request.Telefono),
            PuntoEntregaPreferido = TextoOpcionalLargo(request.PuntoEntregaPreferido, 80),
            CanalContacto = request.CanalContacto,
            ContactoReferencia = TextoOpcionalLargo(request.ContactoReferencia, 160),
            TipoDocumento = tipo,
            NumeroDocumento = numero,
            FechaCreacion = ahora
        };
        db.Clientes.Add(cliente);
        return cliente;
    }

    private async Task AsegurarDocumentoLibreAsync(
        string numero,
        Guid idActual,
        CancellationToken cancellationToken)
    {
        if (numero == DocumentoIdentidad.NumeroSinDocumento)
        {
            return;
        }

        var choque = await db.Clientes.AnyAsync(
            c => c.NumeroDocumento == numero && c.Id != idActual,
            cancellationToken);
        if (choque)
        {
            throw new BusinessRuleException("Ya existe un cliente con ese documento.");
        }
    }

    private static string? TextoOpcional(string? valor)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, 32)];
    }

    private static string? TextoOpcionalLargo(string? valor, int max)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, max)];
    }

    internal static ClienteResponse Map(Cliente cliente) => new()
    {
        Id = cliente.Id,
        Nombre = cliente.Nombre,
        Telefono = cliente.Telefono,
        PuntoEntregaPreferido = cliente.PuntoEntregaPreferido,
        CanalContacto = cliente.CanalContacto,
        ContactoReferencia = cliente.ContactoReferencia,
        TipoDocumento = cliente.TipoDocumento,
        NumeroDocumento = cliente.NumeroDocumento,
        EsPublicoGeneral = DocumentoIdentidad.EsPublicoGeneral(
            cliente.Nombre,
            cliente.TipoDocumento,
            cliente.NumeroDocumento),
        FechaCreacion = cliente.FechaCreacion
    };
}
