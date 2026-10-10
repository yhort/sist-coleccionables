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
        bool? activo,
        CancellationToken cancellationToken)
    {
        var query = db.Clientes.AsNoTracking().AsQueryable();
        if (activo.HasValue)
        {
            query = query.Where(c => c.Activo == activo.Value);
        }

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

    /// <summary>
    /// Catálogo dinámico de puntos de entrega preferidos usados en clientes (más sugerencias del FE).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListarPuntosEntregaPreferidosAsync(
        CancellationToken cancellationToken)
    {
        return await db.Clientes.AsNoTracking()
            .Where(c => c.PuntoEntregaPreferido != null && c.PuntoEntregaPreferido != "")
            .Select(c => c.PuntoEntregaPreferido!)
            .Distinct()
            .OrderBy(p => p)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<ClienteResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return cliente is null ? null : Map(cliente);
    }

    public async Task<ClienteResponse> CrearAsync(UpsertClienteRequest request, CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        if (request.EsPublicoGeneral)
        {
            var publico = await ResolverPublicoGeneralAsync(ahora, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Map(publico);
        }

        var (tipo, numero, nombre) = DocumentoIdentidad.NormalizarCliente(
            request.Nombre,
            request.TipoDocumento,
            request.NumeroDocumento,
            request.EsPublicoGeneral);
        var telefono = TelefonoCliente.Normalizar(request.Telefono);
        var punto = TextoOpcionalLargo(request.PuntoEntregaPreferido, 80);
        var contactoRef = TextoOpcionalLargo(request.ContactoReferencia, 160);

        // Si hay un cliente inactivo con el mismo documento, lo reactivamos.
        var inactivo = await BuscarPorDocumentoAsync(numero, soloActivos: false, cancellationToken);
        if (inactivo is { Activo: false })
        {
            await AsegurarDocumentoLibreAsync(numero, inactivo.Id, cancellationToken);
            if (EsClienteSinDocumento(tipo, numero))
            {
                await AsegurarNombreLibreSinDocumentoAsync(nombre, inactivo.Id, cancellationToken);
            }

            AplicarDatos(inactivo, nombre, telefono, punto, request.CanalContacto, contactoRef, tipo, numero);
            inactivo.Activo = true;
            await db.SaveChangesAsync(cancellationToken);
            return Map(inactivo);
        }

        // Sin documento: reutilizar inactivo homónimo (evita duplicar al “recrear” tras desactivar).
        if (EsClienteSinDocumento(tipo, numero))
        {
            var inactivoPorNombre = await BuscarPorNombreSinDocumentoAsync(
                nombre,
                soloActivos: false,
                cancellationToken);
            if (inactivoPorNombre is { Activo: false })
            {
                AplicarDatos(
                    inactivoPorNombre,
                    nombre,
                    telefono,
                    punto,
                    request.CanalContacto,
                    contactoRef,
                    tipo,
                    numero);
                inactivoPorNombre.Activo = true;
                await db.SaveChangesAsync(cancellationToken);
                return Map(inactivoPorNombre);
            }

            await AsegurarNombreLibreSinDocumentoAsync(nombre, excluirId: null, cancellationToken);
        }

        await AsegurarDocumentoLibreAsync(numero, excluirId: null, cancellationToken);

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Nombre = NormalizarNombrePresentacion(nombre),
            Telefono = telefono,
            PuntoEntregaPreferido = punto,
            CanalContacto = request.CanalContacto,
            ContactoReferencia = contactoRef,
            TipoDocumento = tipo,
            NumeroDocumento = DocumentoIdentidad.NumeroParaPersistir(numero),
            Activo = true,
            FechaCreacion = ahora
        };
        db.Clientes.Add(cliente);
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
        if (EsClienteSinDocumento(tipo, numero)
            && !DocumentoIdentidad.EsPublicoGeneral(nombre, tipo, numero))
        {
            await AsegurarNombreLibreSinDocumentoAsync(nombre, id, cancellationToken);
        }

        AplicarDatos(
            cliente,
            nombre,
            TelefonoCliente.Normalizar(request.Telefono),
            TextoOpcionalLargo(request.PuntoEntregaPreferido, 80),
            request.CanalContacto,
            TextoOpcionalLargo(request.ContactoReferencia, 160),
            tipo,
            numero);
        await db.SaveChangesAsync(cancellationToken);
        return Map(cliente);
    }

    /// <summary>Soft delete: oculta el cliente de listados activos sin borrar historial.</summary>
    public async Task<ClienteResponse?> DesactivarAsync(Guid id, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cliente is null)
        {
            return null;
        }

        if (DocumentoIdentidad.EsPublicoGeneral(cliente.Nombre, cliente.TipoDocumento, cliente.NumeroDocumento))
        {
            throw new BusinessRuleException("No se puede desactivar el cliente varios / público general.");
        }

        if (!cliente.Activo)
        {
            return Map(cliente);
        }

        cliente.Activo = false;
        await db.SaveChangesAsync(cancellationToken);
        return Map(cliente);
    }

    public async Task<ClienteResponse?> ReactivarAsync(Guid id, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cliente is null)
        {
            return null;
        }

        if (cliente.Activo)
        {
            return Map(cliente);
        }

        if (!string.IsNullOrWhiteSpace(cliente.NumeroDocumento)
            && !DocumentoIdentidad.EsNumeroLegadoSinDocumento(cliente.NumeroDocumento))
        {
            await AsegurarDocumentoLibreAsync(cliente.NumeroDocumento, id, cancellationToken);
        }
        else if (EsClienteSinDocumento(cliente.TipoDocumento, cliente.NumeroDocumento)
                 && !DocumentoIdentidad.EsPublicoGeneral(
                     cliente.Nombre,
                     cliente.TipoDocumento,
                     cliente.NumeroDocumento))
        {
            await AsegurarNombreLibreSinDocumentoAsync(cliente.Nombre, id, cancellationToken);
        }

        cliente.Activo = true;
        await db.SaveChangesAsync(cancellationToken);
        return Map(cliente);
    }

    public async Task<Cliente> ResolverPublicoGeneralAsync(
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // Buscar por nombre (y legado 00000000) — el número ya no se usa como clave.
        var existente = await db.Clientes.FirstOrDefaultAsync(
            c => (c.Nombre == DocumentoIdentidad.NombreClienteVarios
                    || c.Nombre == DocumentoIdentidad.NombrePublicoGeneral)
                && (c.TipoDocumento == TipoDocumentoIdentidad.SIN_DOCUMENTO
                    || c.TipoDocumento == TipoDocumentoIdentidad.DNI),
            cancellationToken);
        if (existente is null)
        {
            // Transición: registros antiguos identificados solo por 00000000 / 0000000.
            existente = await db.Clientes.FirstOrDefaultAsync(
                c => c.NumeroDocumento == DocumentoIdentidad.NumeroSinDocumentoLegado
                    || c.NumeroDocumento == "0000000",
                cancellationToken);
        }

        if (existente is not null)
        {
            existente.TipoDocumento = TipoDocumentoIdentidad.SIN_DOCUMENTO;
            existente.NumeroDocumento = null;
            existente.Activo = true;
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
            NumeroDocumento = null,
            Activo = true,
            FechaCreacion = ahora
        };
        db.Clientes.Add(cliente);
        return cliente;
    }

    /// <summary>
    /// Alta desde pedido digital: reutiliza cliente activo por documento o crea uno nuevo
    /// con teléfono / punto / canal / contacto de referencia del formulario.
    /// </summary>
    public async Task<Cliente> ResolverOCrearDesdePedidoAsync(
        UpsertClienteRequest request,
        CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        if (request.EsPublicoGeneral)
        {
            return await ResolverPublicoGeneralAsync(ahora, cancellationToken);
        }

        var (tipo, numero, nombre) = DocumentoIdentidad.NormalizarCliente(
            request.Nombre,
            request.TipoDocumento,
            request.NumeroDocumento,
            request.EsPublicoGeneral);
        var telefono = TelefonoCliente.Normalizar(request.Telefono);
        var punto = TextoOpcionalLargo(request.PuntoEntregaPreferido, 80);
        var contactoRef = TextoOpcionalLargo(request.ContactoReferencia, 160);

        if (!string.IsNullOrWhiteSpace(numero))
        {
            var existente = await BuscarPorDocumentoAsync(numero, soloActivos: false, cancellationToken);
            if (existente is not null)
            {
                AplicarDatos(
                    existente,
                    nombre,
                    telefono ?? existente.Telefono,
                    punto ?? existente.PuntoEntregaPreferido,
                    request.CanalContacto ?? existente.CanalContacto,
                    contactoRef ?? existente.ContactoReferencia,
                    tipo,
                    numero);
                existente.Activo = true;
                return existente;
            }
        }
        else if (EsClienteSinDocumento(tipo, numero))
        {
            // Sin documento: reutiliza por nombre y actualiza punto/teléfono/canal (evita duplicados).
            var porNombre = await BuscarPorNombreSinDocumentoAsync(
                nombre,
                soloActivos: false,
                cancellationToken);
            if (porNombre is not null)
            {
                AplicarDatos(
                    porNombre,
                    nombre,
                    telefono ?? porNombre.Telefono,
                    punto ?? porNombre.PuntoEntregaPreferido,
                    request.CanalContacto ?? porNombre.CanalContacto,
                    contactoRef ?? porNombre.ContactoReferencia,
                    tipo,
                    numero);
                porNombre.Activo = true;
                return porNombre;
            }
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Nombre = NormalizarNombrePresentacion(nombre),
            Telefono = telefono,
            PuntoEntregaPreferido = punto,
            CanalContacto = request.CanalContacto,
            ContactoReferencia = contactoRef,
            TipoDocumento = tipo,
            NumeroDocumento = DocumentoIdentidad.NumeroParaPersistir(numero),
            Activo = true,
            FechaCreacion = ahora
        };
        db.Clientes.Add(cliente);
        return cliente;
    }

    private async Task<Cliente?> BuscarPorDocumentoAsync(
        string? numero,
        bool soloActivos,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numero) || DocumentoIdentidad.EsNumeroLegadoSinDocumento(numero))
        {
            return null;
        }

        var query = db.Clientes.Where(c => c.NumeroDocumento == numero);
        if (soloActivos)
        {
            query = query.Where(c => c.Activo);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Cliente?> BuscarPorNombreSinDocumentoAsync(
        string nombre,
        bool soloActivos,
        CancellationToken cancellationToken)
    {
        var clave = NormalizarNombreClave(nombre);
        if (clave.Length < 2)
        {
            return null;
        }

        var query = db.Clientes.Where(c =>
            c.TipoDocumento == TipoDocumentoIdentidad.SIN_DOCUMENTO
            && (c.NumeroDocumento == null || c.NumeroDocumento == "")
            && c.Nombre.ToLower() == clave);
        if (soloActivos)
        {
            query = query.Where(c => c.Activo);
        }

        // Preferir activo si hay varios legados; si no, el más reciente.
        return await query
            .OrderByDescending(c => c.Activo)
            .ThenByDescending(c => c.FechaCreacion)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task AsegurarDocumentoLibreAsync(
        string? numero,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numero) || DocumentoIdentidad.EsNumeroLegadoSinDocumento(numero))
        {
            return;
        }

        var query = db.Clientes.Where(c => c.Activo && c.NumeroDocumento == numero);
        if (excluirId.HasValue)
        {
            query = query.Where(c => c.Id != excluirId.Value);
        }

        var choque = await query.AnyAsync(cancellationToken);
        if (choque)
        {
            throw new BusinessRuleException(
                "El cliente ya se encuentra registrado con ese DNI/RUC.");
        }
    }

    private async Task AsegurarNombreLibreSinDocumentoAsync(
        string nombre,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        var clave = NormalizarNombreClave(nombre);
        if (clave.Length < 2)
        {
            return;
        }

        var query = db.Clientes.Where(c =>
            c.Activo
            && c.TipoDocumento == TipoDocumentoIdentidad.SIN_DOCUMENTO
            && (c.NumeroDocumento == null || c.NumeroDocumento == "")
            && c.Nombre.ToLower() == clave);
        if (excluirId.HasValue)
        {
            query = query.Where(c => c.Id != excluirId.Value);
        }

        var choque = await query.AnyAsync(cancellationToken);
        if (choque)
        {
            throw new BusinessRuleException(
                "Ya existe un cliente activo sin documento con ese nombre. "
                + "Edítalo desde el listado para actualizar el punto de entrega u otros datos.");
        }
    }

    private static bool EsClienteSinDocumento(TipoDocumentoIdentidad tipo, string? numero) =>
        tipo == TipoDocumentoIdentidad.SIN_DOCUMENTO
        || string.IsNullOrWhiteSpace(numero)
        || DocumentoIdentidad.EsNumeroLegadoSinDocumento(numero);

    private static string NormalizarNombreClave(string nombre) =>
        NormalizarNombrePresentacion(nombre).ToLowerInvariant();

    private static void AplicarDatos(
        Cliente cliente,
        string nombre,
        string? telefono,
        string? punto,
        CanalContactoCliente? canal,
        string? contactoRef,
        TipoDocumentoIdentidad tipo,
        string? numero)
    {
        cliente.Nombre = NormalizarNombrePresentacion(nombre);
        cliente.Telefono = telefono;
        cliente.PuntoEntregaPreferido = punto;
        cliente.CanalContacto = canal;
        cliente.ContactoReferencia = contactoRef;
        cliente.TipoDocumento = tipo;
        cliente.NumeroDocumento = DocumentoIdentidad.NumeroParaPersistir(numero);
    }

    private static string? TextoOpcionalLargo(string? valor, int max)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, max)];
    }

    private static string NormalizarNombrePresentacion(string nombre)
    {
        var partes = nombre.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', partes);
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
        Activo = cliente.Activo,
        FechaCreacion = cliente.FechaCreacion
    };
}
