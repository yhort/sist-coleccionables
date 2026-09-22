using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using CapitalPos.Tcg.Api.Infrastructure.WooCommerce;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence;

/// <summary>
/// Bootstrap mínimo de Development para probar JWT y los controladores.
/// No sustituye el seed operativo de Trunqi (sedes Mira/Surco, catálogo real).
/// </summary>
public static class DevelopmentDataSeeder
{
    public static readonly Guid EmpresaId = Guid.Parse("c0a1e001-0000-4000-8000-000000000001");
    public static readonly Guid SedeId = Guid.Parse("c0a1e001-0000-4000-8000-000000000010");
    public static readonly Guid UsuarioId = Guid.Parse("c0a1e001-0000-4000-8000-000000000100");

    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var protector = scope.ServiceProvider.GetRequiredService<WooCredentialProtector>();

        if (!config.GetValue("DevelopmentSeed:Enabled", false))
        {
            return;
        }

        var email = config["DevelopmentSeed:AdminEmail"] ?? "admin@trunqi.local";
        var password = config["DevelopmentSeed:AdminPassword"] ?? "Admin123!";
        var ahora = DateTimeOffset.UtcNow;

        if (!await db.Empresas.IgnoreQueryFilters().AnyAsync())
        {
            tenant.SetEmpresa(EmpresaId);

            var empresa = new Empresa
            {
                Id = EmpresaId,
                Ruc = "20123456789",
                RazonSocial = "TRUNQI TCG SAC",
                NombreComercial = "TRUNQI",
                Activa = true,
                FechaCreacion = ahora
            };

            var sede = new Sede
            {
                Id = SedeId,
                EmpresaId = EmpresaId,
                Nombre = "Tienda Miraflores",
                Tipo = TipoSede.TIENDA,
                EsAlmacenPrincipal = true,
                Activa = true,
                FechaCreacion = ahora
            };

            db.Empresas.Add(empresa);
            db.Sedes.Add(sede);
            await db.SaveChangesAsync();
        }

        // Siempre reescribe el hash del admin en Development (evita UPDATE manual / hash inválido).
        await EnsureAdminAsync(db, tenant, hasher, email, password, ahora);

        tenant.SetEmpresa(EmpresaId);
        await EnsureSeriesComprobanteAsync(db);
        await EnsureFiscalAsync(db);
        await EnsureClienteVariosAsync(db);
        await EnsureWooAsync(db, protector, config);
        await EnsureEcosistemaAsync(db);
        await EnsureDemoCatalogoAsync(db);
    }

    private static async Task EnsureAdminAsync(
        ApplicationDbContext db,
        ITenantProvider tenant,
        IPasswordHasher<Usuario> hasher,
        string email,
        string password,
        DateTimeOffset ahora)
    {
        var empresaId = await db.Empresas.IgnoreQueryFilters()
            .Where(e => e.Activa)
            .OrderBy(e => e.FechaCreacion)
            .Select(e => e.Id)
            .FirstOrDefaultAsync();
        if (empresaId == Guid.Empty)
        {
            return;
        }

        tenant.SetEmpresa(empresaId);

        var usuario = await db.Usuarios.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        if (usuario is null)
        {
            usuario = new Usuario
            {
                Id = UsuarioId,
                EmpresaId = empresaId,
                Dni = "00000001",
                Nombres = "Admin",
                Apellidos = "Trunqi",
                Nombre = "Admin Trunqi",
                Email = email,
                Rol = RolUsuario.ADMIN,
                Activo = true,
                FechaCreacion = ahora
            };
            usuario.SincronizarNombreCompleto();
            usuario.PasswordHash = hasher.HashPassword(usuario, password);
            db.Usuarios.Add(usuario);
        }
        else
        {
            usuario.EmpresaId = empresaId;
            usuario.Activo = true;
            usuario.Rol = RolUsuario.ADMIN;
            usuario.PasswordHash = hasher.HashPassword(usuario, password);
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureSeriesComprobanteAsync(ApplicationDbContext db)
    {
        var ahora = DateTimeOffset.UtcNow;
        await AsegurarSerieAsync(db, TipoComprobanteSunat.BOLETA, "B001", Guid.Parse("c0a1e001-0000-4000-8000-000000000201"), ahora);
        await AsegurarSerieAsync(db, TipoComprobanteSunat.FACTURA, "F001", Guid.Parse("c0a1e001-0000-4000-8000-000000000202"), ahora);
        await AsegurarSerieAsync(db, TipoComprobanteSunat.NOTA_CREDITO, "FC01", Guid.Parse("c0a1e001-0000-4000-8000-000000000203"), ahora);
        await AsegurarSerieAsync(db, TipoComprobanteSunat.NOTA_CREDITO, "BC01", Guid.Parse("c0a1e001-0000-4000-8000-000000000204"), ahora);
        await AsegurarSerieAsync(db, TipoComprobanteSunat.NOTA_VENTA, "NV01", Guid.Parse("c0a1e001-0000-4000-8000-000000000205"), ahora);
        await db.SaveChangesAsync();
    }

    private static async Task AsegurarSerieAsync(
        ApplicationDbContext db,
        TipoComprobanteSunat tipo,
        string codigo,
        Guid id,
        DateTimeOffset ahora)
    {
        if (await db.SeriesComprobante.AnyAsync(s => s.Tipo == tipo && s.Serie == codigo))
        {
            return;
        }

        db.SeriesComprobante.Add(new SerieComprobante
        {
            Id = id,
            EmpresaId = EmpresaId,
            Tipo = tipo,
            Serie = codigo,
            Correlativo = 0,
            Activa = true,
            FechaCreacion = ahora
        });
    }

    private static async Task EnsureFiscalAsync(ApplicationDbContext db)
    {
        if (await db.ConfiguracionesFiscales.AnyAsync())
        {
            return;
        }

        var ahora = DateTimeOffset.UtcNow;
        db.ConfiguracionesFiscales.Add(new ConfiguracionFiscalEmpresa
        {
            Id = Guid.Parse("c0a1e001-0000-4000-8000-000000000300"),
            EmpresaId = EmpresaId,
            Ruc = "20123456789",
            RazonSocial = "TRUNQI TCG SAC",
            NombreComercial = "TRUNQI",
            DireccionFiscal = "AV. DEMO 123",
            Ubigeo = "150101",
            Departamento = "LIMA",
            Provincia = "LIMA",
            Distrito = "LIMA",
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureClienteVariosAsync(ApplicationDbContext db)
    {
        if (await db.Clientes.AnyAsync(c => c.NumeroDocumento == "00000000"))
        {
            return;
        }

        db.Clientes.Add(new Cliente
        {
            Id = Guid.Parse("c0a1e001-0000-4000-8000-000000000700"),
            EmpresaId = EmpresaId,
            Nombre = "CLIENTES VARIOS",
            TipoDocumento = TipoDocumentoIdentidad.SIN_DOCUMENTO,
            NumeroDocumento = "00000000",
            FechaCreacion = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureWooAsync(
        ApplicationDbContext db,
        WooCredentialProtector protector,
        IConfiguration config)
    {
        var url = (config["WooCommerce:UrlTienda"] ?? "https://trunqitcg.com").Trim().TrimEnd('/');
        var consumerKey = config["WooCommerce:ConsumerKey"]?.Trim();
        var consumerSecret = config["WooCommerce:ConsumerSecret"]?.Trim();
        var ahora = DateTimeOffset.UtcNow;

        var integracion = await db.IntegracionesWooCommerce.FirstOrDefaultAsync();
        if (integracion is not null)
        {
            return;
        }

        integracion = new IntegracionWooCommerce
        {
            Id = Guid.Parse("c0a1e001-0000-4000-8000-000000000400"),
            EmpresaId = EmpresaId,
            UrlTienda = url,
            ConsumerKeyCifrado = protector.Cifrar(consumerKey ?? "ck_demo_capitalpos"),
            ConsumerSecretCifrado = protector.Cifrar(consumerSecret ?? "cs_demo_capitalpos"),
            SedeOrigenId = SedeId,
            ModoSincronizacion = ModoSincronizacionWoo.MANUAL,
            ModoRecepcionPedidos = ModoRecepcionPedidosWoo.WEBHOOK,
            Activa = true,
            EstadoConexion = EstadoConexionWoo.DESCONECTADO,
            MensajeConexion = "Credenciales de Development aplicadas. Prueba la conexión REST.",
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };
        db.IntegracionesWooCommerce.Add(integracion);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureEcosistemaAsync(ApplicationDbContext db)
    {
        if (await db.EcosistemaConexiones.AnyAsync())
        {
            return;
        }

        var ahora = DateTimeOffset.UtcNow;
        db.EcosistemaConexiones.AddRange(
            new EcosistemaConexion
            {
                Id = Guid.Parse("c0a1e001-0000-4000-8000-000000000501"),
                EmpresaId = EmpresaId,
                Tipo = TipoConexionEcosistema.CPE_LOCAL,
                Nombre = "CPE local / SUNAT",
                EstadoSalud = EstadoSaludConexion.DESCONOCIDO,
                FechaCreacion = ahora
            },
            new EcosistemaConexion
            {
                Id = Guid.Parse("c0a1e001-0000-4000-8000-000000000502"),
                EmpresaId = EmpresaId,
                Tipo = TipoConexionEcosistema.WOOCOMMERCE,
                Nombre = "WooCommerce",
                EstadoSalud = EstadoSaludConexion.DESCONOCIDO,
                FechaCreacion = ahora
            },
            new EcosistemaConexion
            {
                Id = Guid.Parse("c0a1e001-0000-4000-8000-000000000503"),
                EmpresaId = EmpresaId,
                Tipo = TipoConexionEcosistema.YAPE,
                Nombre = "Yape",
                EstadoSalud = EstadoSaludConexion.DESCONOCIDO,
                FechaCreacion = ahora
            },
            new EcosistemaConexion
            {
                Id = Guid.Parse("c0a1e001-0000-4000-8000-000000000504"),
                EmpresaId = EmpresaId,
                Tipo = TipoConexionEcosistema.IZIPAY,
                Nombre = "Izipay",
                EstadoSalud = EstadoSaludConexion.DESCONOCIDO,
                FechaCreacion = ahora
            });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoCatalogoAsync(ApplicationDbContext db)
    {
        if (await db.Productos.AnyAsync())
        {
            return;
        }

        var ahora = DateTimeOffset.UtcNow;
        var cartaId = Guid.Parse("c0a1e001-0000-4000-8000-000000000610");
        db.ProductosCarta.Add(new ProductoCarta
        {
            Id = cartaId,
            EmpresaId = EmpresaId,
            TipoProducto = TipoProducto.CARTA,
            Nombre = "Charizard ex — Obsidian Flames",
            CodigoSku = "PKM-OBF-215-NM",
            PrecioVenta = 89.90m,
            Costo = 55m,
            Activo = true,
            FechaCreacion = ahora,
            Juego = "Pokemon",
            SetCodigo = "OBF",
            SetNombre = "Obsidian Flames",
            NumeroCarta = "215",
            Rareza = RarezaTcg.ULTRA,
            Idioma = IdiomaTcg.ES,
            Condicion = CondicionTcg.NM,
            EsFoil = true
        });
        db.StocksProductos.Add(new StockProducto
        {
            Id = Guid.Parse("c0a1e001-0000-4000-8000-000000000611"),
            EmpresaId = EmpresaId,
            SedeId = SedeId,
            ProductoId = cartaId,
            CantidadDisponible = 5,
            CantidadReservada = 0
        });
        await db.SaveChangesAsync();
    }
}
