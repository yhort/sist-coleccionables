using CapitalPos.Tcg.Api.Contracts.Catalogo;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Catalogo;

public sealed class CatalogoTcgService(ApplicationDbContext db, ITenantProvider tenant)
{
    public async Task<IReadOnlyList<TcgSerieResponse>> ListarSeriesAsync(CancellationToken cancellationToken)
    {
        var series = await db.TcgSeries.AsNoTracking()
            .Where(s => s.Activa)
            .OrderBy(s => s.Nombre)
            .ToListAsync(cancellationToken);
        return series.Select(MapSerie).ToList();
    }

    public async Task<TcgSerieResponse> CrearSerieAsync(
        UpsertTcgSerieRequest request,
        CancellationToken cancellationToken)
    {
        var juego = NormalizarJuego(request.Juego);
        var codigo = NormalizarCodigo(request.Codigo);
        var nombre = request.Nombre.Trim();
        if (nombre.Length == 0)
        {
            throw new BusinessRuleException("El nombre de la serie es obligatorio.");
        }

        var duplicada = await db.TcgSeries.AnyAsync(
            s => s.Juego == juego && s.Codigo == codigo,
            cancellationToken);
        if (duplicada)
        {
            throw new BusinessRuleException("Ya existe una serie con ese juego y código.", StatusCodes.Status409Conflict);
        }

        var serie = new TcgSerie
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Juego = juego,
            Codigo = codigo,
            Nombre = nombre,
            Activa = request.Activa
        };
        db.TcgSeries.Add(serie);
        await db.SaveChangesAsync(cancellationToken);
        return MapSerie(serie);
    }

    public async Task<IReadOnlyList<TcgSetResponse>> ListarSetsAsync(Guid serieId, CancellationToken cancellationToken)
    {
        if (serieId == Guid.Empty)
        {
            throw new BusinessRuleException("serieId es obligatorio.");
        }

        var existeSerie = await db.TcgSeries.AsNoTracking()
            .AnyAsync(s => s.Id == serieId, cancellationToken);
        if (!existeSerie)
        {
            throw new BusinessRuleException("No existe la serie.", StatusCodes.Status404NotFound);
        }

        var sets = await db.TcgSets.AsNoTracking()
            .Include(s => s.Serie)
            .Where(s => s.SerieId == serieId)
            .OrderBy(s => s.Codigo)
            .ToListAsync(cancellationToken);
        return sets.Select(MapSet).ToList();
    }

    public async Task<TcgSetResponse> CrearSetAsync(
        UpsertTcgSetRequest request,
        CancellationToken cancellationToken)
    {
        var serie = await db.TcgSeries.FirstOrDefaultAsync(s => s.Id == request.SerieId, cancellationToken)
            ?? throw new BusinessRuleException("No existe la serie.", StatusCodes.Status404NotFound);

        var codigo = NormalizarCodigo(request.Codigo);
        var nombre = request.Nombre.Trim();
        if (nombre.Length == 0)
        {
            throw new BusinessRuleException("El nombre del set es obligatorio.");
        }

        var duplicado = await db.TcgSets.AnyAsync(s => s.Codigo == codigo, cancellationToken);
        if (duplicado)
        {
            throw new BusinessRuleException("Ya existe un set con ese código.", StatusCodes.Status409Conflict);
        }

        var set = new TcgSet
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SerieId = serie.Id,
            Codigo = codigo,
            Nombre = nombre,
            NombreEn = TextoOpcional(request.NombreEn),
            CodigoImpresion = TextoOpcional(request.CodigoImpresion),
            TotalCartas = request.TotalCartas,
            FechaLanzamiento = request.FechaLanzamiento,
            Serie = serie
        };
        db.TcgSets.Add(set);
        await db.SaveChangesAsync(cancellationToken);
        return MapSet(set);
    }

    public async Task<IReadOnlyList<TcgCartaResponse>> ListarCartasAsync(
        Guid setId,
        string? numero,
        RarezaTcg? rareza,
        CancellationToken cancellationToken)
    {
        if (setId == Guid.Empty)
        {
            throw new BusinessRuleException("setId es obligatorio.");
        }

        var existeSet = await db.TcgSets.AsNoTracking()
            .AnyAsync(s => s.Id == setId, cancellationToken);
        if (!existeSet)
        {
            throw new BusinessRuleException("No existe el set.", StatusCodes.Status404NotFound);
        }

        var query = db.TcgCartas.AsNoTracking()
            .Include(c => c.Set)
            .Where(c => c.SetId == setId);

        if (!string.IsNullOrWhiteSpace(numero))
        {
            var num = numero.Trim();
            query = query.Where(c => EF.Functions.ILike(c.Numero, num));
        }

        if (rareza.HasValue)
        {
            query = query.Where(c => c.Rareza == rareza.Value);
        }

        var cartas = await query
            .OrderBy(c => c.Numero)
            .ThenBy(c => c.Rareza)
            .ToListAsync(cancellationToken);
        return cartas.Select(MapCarta).ToList();
    }

    public async Task<ImportarSetTcgResponse> ImportarSetAsync(
        ImportarSetTcgRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Cartas.Count == 0)
        {
            throw new BusinessRuleException("El set debe incluir al menos una ficha.");
        }

        var duplicadas = request.Cartas
            .GroupBy(c => (Numero: c.Numero.Trim(), c.Rareza), StringPairComparer)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Numero} {g.Key.Rareza}")
            .ToList();
        if (duplicadas.Count > 0)
        {
            throw new BusinessRuleException(
                $"Hay fichas duplicadas en la importación (mismo número y rareza): {string.Join(", ", duplicadas)}.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var serie = await ResolverSerieAsync(request.Serie, cancellationToken);
        var set = await ResolverSetAsync(serie, request.Set, request.Cartas.Count, cancellationToken);

        var existentes = await db.TcgCartas
            .Include(c => c.Set)
            .Where(c => c.SetId == set.Id)
            .ToListAsync(cancellationToken);
        var porClave = existentes.ToDictionary(
            c => (c.Numero, c.Rareza),
            StringPairComparer);

        var creadas = 0;
        var actualizadas = 0;
        var resultado = new List<TcgCarta>(request.Cartas.Count);

        foreach (var input in request.Cartas)
        {
            var numero = input.Numero.Trim();
            if (numero.Length == 0)
            {
                throw new BusinessRuleException("El número de carta es obligatorio.");
            }

            var nombre = input.Nombre.Trim();
            if (nombre.Length == 0)
            {
                throw new BusinessRuleException($"El nombre de la ficha #{numero} es obligatorio.");
            }

            if (porClave.TryGetValue((numero, input.Rareza), out var ficha))
            {
                ficha.Nombre = nombre;
                ficha.TipoCarta = input.TipoCarta;
                ficha.Artista = TextoOpcional(input.Artista);
                ficha.ImagenOficialUrl = TextoOpcional(input.ImagenOficialUrl);
                actualizadas++;
                resultado.Add(ficha);
                continue;
            }

            ficha = new TcgCarta
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                SetId = set.Id,
                Numero = numero,
                Nombre = nombre,
                TipoCarta = input.TipoCarta,
                Rareza = input.Rareza,
                Artista = TextoOpcional(input.Artista),
                ImagenOficialUrl = TextoOpcional(input.ImagenOficialUrl),
                Set = set
            };
            db.TcgCartas.Add(ficha);
            porClave[(numero, input.Rareza)] = ficha;
            creadas++;
            resultado.Add(ficha);
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new ImportarSetTcgResponse
        {
            Serie = MapSerie(serie),
            Set = MapSet(set),
            CartasCreadas = creadas,
            CartasActualizadas = actualizadas,
            Cartas = resultado.Select(MapCarta).ToList()
        };
    }

    private async Task<TcgSerie> ResolverSerieAsync(
        UpsertTcgSerieRequest request,
        CancellationToken cancellationToken)
    {
        var juego = NormalizarJuego(request.Juego);
        var codigo = NormalizarCodigo(request.Codigo);
        var nombre = request.Nombre.Trim();
        if (nombre.Length == 0)
        {
            throw new BusinessRuleException("El nombre de la serie es obligatorio.");
        }

        var serie = await db.TcgSeries
            .FirstOrDefaultAsync(s => s.Juego == juego && s.Codigo == codigo, cancellationToken);
        if (serie is null)
        {
            serie = new TcgSerie
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                Juego = juego,
                Codigo = codigo,
                Nombre = nombre,
                Activa = request.Activa
            };
            db.TcgSeries.Add(serie);
            return serie;
        }

        serie.Nombre = nombre;
        serie.Activa = request.Activa;
        return serie;
    }

    private async Task<TcgSet> ResolverSetAsync(
        TcgSerie serie,
        ImportarTcgSetRequest request,
        int cartasEnLote,
        CancellationToken cancellationToken)
    {
        var codigo = NormalizarCodigo(request.Codigo);
        var nombre = request.Nombre.Trim();
        if (nombre.Length == 0)
        {
            throw new BusinessRuleException("El nombre del set es obligatorio.");
        }

        var set = await db.TcgSets
            .Include(s => s.Serie)
            .FirstOrDefaultAsync(s => s.Codigo == codigo, cancellationToken);
        var total = request.TotalCartas > 0 ? request.TotalCartas : cartasEnLote;

        if (set is null)
        {
            set = new TcgSet
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                SerieId = serie.Id,
                Codigo = codigo,
                Nombre = nombre,
                NombreEn = TextoOpcional(request.NombreEn),
                CodigoImpresion = TextoOpcional(request.CodigoImpresion),
                TotalCartas = total,
                FechaLanzamiento = request.FechaLanzamiento,
                Serie = serie
            };
            db.TcgSets.Add(set);
            return set;
        }

        if (set.SerieId != serie.Id)
        {
            throw new BusinessRuleException(
                $"El set {codigo} ya pertenece a otra serie.",
                StatusCodes.Status409Conflict);
        }

        set.Nombre = nombre;
        set.NombreEn = TextoOpcional(request.NombreEn) ?? set.NombreEn;
        set.CodigoImpresion = TextoOpcional(request.CodigoImpresion) ?? set.CodigoImpresion;
        set.TotalCartas = total;
        if (request.FechaLanzamiento.HasValue)
        {
            set.FechaLanzamiento = request.FechaLanzamiento;
        }

        return set;
    }

    private static readonly IEqualityComparer<(string Numero, RarezaTcg Rareza)> StringPairComparer =
        new NumeroRarezaComparer();

    private static TcgSerieResponse MapSerie(TcgSerie serie) => new()
    {
        Id = serie.Id,
        Juego = serie.Juego,
        Codigo = serie.Codigo,
        Nombre = serie.Nombre,
        Activa = serie.Activa
    };

    private static TcgSetResponse MapSet(TcgSet set) => new()
    {
        Id = set.Id,
        SerieId = set.SerieId,
        SerieCodigo = set.Serie.Codigo,
        SerieNombre = set.Serie.Nombre,
        Codigo = set.Codigo,
        Nombre = set.Nombre,
        NombreEn = set.NombreEn,
        CodigoImpresion = set.CodigoImpresion,
        TotalCartas = set.TotalCartas,
        FechaLanzamiento = set.FechaLanzamiento
    };

    private static TcgCartaResponse MapCarta(TcgCarta carta) => new()
    {
        Id = carta.Id,
        SetId = carta.SetId,
        SetCodigo = carta.Set.Codigo,
        SetNombre = carta.Set.Nombre,
        Numero = carta.Numero,
        Nombre = carta.Nombre,
        TipoCarta = carta.TipoCarta,
        Rareza = carta.Rareza,
        Artista = carta.Artista,
        ImagenOficialUrl = carta.ImagenOficialUrl
    };

    internal static string NormalizarJuego(string juego)
    {
        var texto = juego.Trim();
        if (texto.Length == 0)
        {
            throw new BusinessRuleException("El juego es obligatorio.");
        }

        return texto.ToUpperInvariant() switch
        {
            "POKEMON" or "POKÉMON" => "Pokémon",
            "MAGIC" => "Magic",
            "YUGIOH" or "YU-GI-OH" or "YU-GI-OH!" => "Yu-Gi-Oh!",
            _ => texto
        };
    }

    private static string NormalizarCodigo(string codigo)
    {
        var texto = codigo.Trim().ToUpperInvariant();
        if (texto.Length == 0)
        {
            throw new BusinessRuleException("El código es obligatorio.");
        }

        return texto;
    }

    private static string? TextoOpcional(string? valor)
    {
        var texto = valor?.Trim();
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    private sealed class NumeroRarezaComparer : IEqualityComparer<(string Numero, RarezaTcg Rareza)>
    {
        public bool Equals((string Numero, RarezaTcg Rareza) x, (string Numero, RarezaTcg Rareza) y) =>
            string.Equals(x.Numero, y.Numero, StringComparison.OrdinalIgnoreCase) && x.Rareza == y.Rareza;

        public int GetHashCode((string Numero, RarezaTcg Rareza) obj) =>
            HashCode.Combine(obj.Numero.ToUpperInvariant(), obj.Rareza);
    }
}
