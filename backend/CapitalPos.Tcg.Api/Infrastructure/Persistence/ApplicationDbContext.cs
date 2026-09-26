using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    private readonly ITenantProvider _tenant;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    /// <summary>
    /// Leído por los query filters. EF parametriza el miembro del DbContext en cada consulta.
    /// </summary>
    public Guid EmpresaActivaId => _tenant.EmpresaId;

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sede> Sedes => Set<Sede>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoCarta> ProductosCarta => Set<ProductoCarta>();
    public DbSet<ProductoSellado> ProductosSellado => Set<ProductoSellado>();
    public DbSet<ProductoSelladoContenidoFijo> ProductoSelladoContenidoFijo => Set<ProductoSelladoContenidoFijo>();
    public DbSet<TcgSerie> TcgSeries => Set<TcgSerie>();
    public DbSet<TcgSet> TcgSets => Set<TcgSet>();
    public DbSet<TcgCarta> TcgCartas => Set<TcgCarta>();
    public DbSet<StockProducto> StocksProductos => Set<StockProducto>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<AperturaTcg> AperturasTcg => Set<AperturaTcg>();
    public DbSet<AperturaTcgDetalle> AperturaTcgDetalles => Set<AperturaTcgDetalle>();
    public DbSet<SubastaTcg> SubastasTcg => Set<SubastaTcg>();
    public DbSet<SubastaDetalle> SubastaDetalles => Set<SubastaDetalle>();
    public DbSet<Puja> Pujas => Set<Puja>();
    public DbSet<PedidoDigital> PedidosDigitales => Set<PedidoDigital>();
    public DbSet<PedidoDigitalDetalle> PedidoDigitalDetalles => Set<PedidoDigitalDetalle>();
    public DbSet<PedidoDigitalHistorialEstado> PedidoDigitalHistorialEstados => Set<PedidoDigitalHistorialEstado>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<CompraDetalle> CompraDetalles => Set<CompraDetalle>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Entrega> Entregas => Set<Entrega>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaDetalle> VentaDetalles => Set<VentaDetalle>();
    public DbSet<VentaPago> VentaPagos => Set<VentaPago>();
    public DbSet<CajaSesion> CajaSesiones => Set<CajaSesion>();
    public DbSet<CajaMovimiento> CajaMovimientos => Set<CajaMovimiento>();
    public DbSet<SerieComprobante> SeriesComprobante => Set<SerieComprobante>();
    public DbSet<Comprobante> Comprobantes => Set<Comprobante>();
    public DbSet<BoletaConsolidada> BoletasConsolidadas => Set<BoletaConsolidada>();
    public DbSet<IntegracionWooCommerce> IntegracionesWooCommerce => Set<IntegracionWooCommerce>();
    public DbSet<WooCommerceMapeoProducto> WooCommerceMapeosProducto => Set<WooCommerceMapeoProducto>();
    public DbSet<WooCommerceSyncLog> WooCommerceSyncLogs => Set<WooCommerceSyncLog>();
    public DbSet<ConfiguracionFiscalEmpresa> ConfiguracionesFiscales => Set<ConfiguracionFiscalEmpresa>();
    public DbSet<EcosistemaConexion> EcosistemaConexiones => Set<EcosistemaConexion>();
    public DbSet<IntegracionIzipay> IntegracionesIzipay => Set<IntegracionIzipay>();
    public DbSet<PasarelaWebhookLog> PasarelaWebhookLogs => Set<PasarelaWebhookLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureEmpresa(modelBuilder);
        ConfigureSede(modelBuilder);
        ConfigureUsuario(modelBuilder);
        ConfigureProducto(modelBuilder);
        ConfigureCatalogoTcg(modelBuilder);
        ConfigureStockProducto(modelBuilder);
        ConfigureMovimientoInventario(modelBuilder);
        ConfigureAperturaTcg(modelBuilder);
        ConfigureSubastaTcg(modelBuilder);
        ConfigurePedidoDigital(modelBuilder);
        ConfigureCliente(modelBuilder);
        ConfigureProveedor(modelBuilder);
        ConfigureCompra(modelBuilder);
        ConfigurePago(modelBuilder);
        ConfigureEntrega(modelBuilder);
        ConfigureVenta(modelBuilder);
        ConfigureCaja(modelBuilder);
        ConfigureWooCommerce(modelBuilder);
        ConfigureEcosistema(modelBuilder);
        ConfigureIzipay(modelBuilder);

        modelBuilder.Entity<Sede>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Usuario>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Producto>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<ProductoSelladoContenidoFijo>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<TcgSerie>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<TcgSet>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<TcgCarta>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<StockProducto>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<MovimientoInventario>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<AperturaTcg>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<AperturaTcgDetalle>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<SubastaTcg>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<SubastaDetalle>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Puja>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<PedidoDigital>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<PedidoDigitalDetalle>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<PedidoDigitalHistorialEstado>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Cliente>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Proveedor>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Compra>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<CompraDetalle>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Pago>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Entrega>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Venta>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<VentaDetalle>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<VentaPago>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<SerieComprobante>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<Comprobante>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<BoletaConsolidada>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<CajaSesion>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<CajaMovimiento>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<IntegracionWooCommerce>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<WooCommerceMapeoProducto>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<WooCommerceSyncLog>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<ConfiguracionFiscalEmpresa>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<EcosistemaConexion>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<IntegracionIzipay>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);
        modelBuilder.Entity<PasarelaWebhookLog>().HasQueryFilter(e => e.EmpresaId == EmpresaActivaId);

        SnakeCase.ApplyColumnNames(modelBuilder);

        modelBuilder.Entity<StockProducto>()
            .Property(s => s.CantidadLibre)
            .HasComputedColumnSql("cantidad_disponible - cantidad_reservada", stored: true);
    }

    private static void ConfigureEmpresa(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Empresa>();
        entity.ToTable("empresas");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Ruc).HasMaxLength(11).IsRequired();
        entity.Property(e => e.RazonSocial).HasMaxLength(200).IsRequired();
        entity.Property(e => e.NombreComercial).HasMaxLength(200).IsRequired();
        entity.HasIndex(e => e.Ruc).IsUnique().HasDatabaseName("ux_empresas_ruc");
    }

    private static void ConfigureSede(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Sede>();
        entity.ToTable("sedes");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Nombre).HasMaxLength(160).IsRequired();
        EnumAsString(entity.Property(e => e.Tipo), 16);
        entity.Property(e => e.Direccion).HasMaxLength(300);
        entity.Property(e => e.Distrito).HasMaxLength(80);
        entity.Property(e => e.Provincia).HasMaxLength(80);
        entity.Property(e => e.Departamento).HasMaxLength(80);
        entity.Property(e => e.Ubigeo).HasMaxLength(6);

        entity.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_sedes_empresa_id");

        entity.HasOne(e => e.Empresa)
            .WithMany(e => e.Sedes)
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.Nombre })
            .HasDatabaseName("ix_sedes_empresa_id_nombre");
    }

    private static void ConfigureUsuario(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Usuario>();
        entity.ToTable("usuarios");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Dni).HasMaxLength(8).IsRequired();
        entity.Property(e => e.Nombres).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Apellidos).HasMaxLength(80).IsRequired();
        entity.Property(e => e.Nombre).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
        entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
        EnumAsString(entity.Property(e => e.Rol), 16);

        entity.HasOne(e => e.Empresa)
            .WithMany(e => e.Usuarios)
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.Email })
            .IsUnique()
            .HasDatabaseName("ux_usuarios_empresa_id_email");

        entity.HasIndex(e => new { e.EmpresaId, e.Dni })
            .IsUnique()
            .HasDatabaseName("ux_usuarios_empresa_id_dni");
    }

    private static void ConfigureProducto(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Producto>();
        entity.ToTable("productos");
        entity.UseTptMappingStrategy();
        entity.HasKey(e => e.Id);
        EnumAsString(entity.Property(e => e.TipoProducto), 16);
        entity.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        entity.Property(e => e.CodigoSku).HasMaxLength(64).IsRequired();
        entity.Property(e => e.CodigoBarras).HasMaxLength(64);
        entity.Property(e => e.PrecioVenta).HasPrecision(18, 2);
        entity.Property(e => e.Costo).HasPrecision(18, 4);
        entity.Property(e => e.Imagenes);

        entity.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_productos_empresa_id");

        entity.HasOne(e => e.Empresa)
            .WithMany(e => e.Productos)
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.CodigoSku })
            .IsUnique()
            .HasDatabaseName("ux_productos_empresa_id_codigo_sku");

        var carta = modelBuilder.Entity<ProductoCarta>();
        carta.ToTable("producto_cartas");
        carta.Property(e => e.Juego).HasMaxLength(80).IsRequired();
        carta.Property(e => e.SetCodigo).HasMaxLength(32).IsRequired();
        carta.Property(e => e.SetNombre).HasMaxLength(120).IsRequired();
        carta.Property(e => e.NumeroCarta).HasMaxLength(16).IsRequired();
        EnumAsString(carta.Property(e => e.Rareza), 32);
        EnumAsString(carta.Property(e => e.Idioma), 8);
        EnumAsString(carta.Property(e => e.Condicion), 8);
        carta.Property(e => e.Artista).HasMaxLength(120);

        var sellado = modelBuilder.Entity<ProductoSellado>();
        sellado.ToTable("producto_sellados");
        sellado.Property(e => e.Juego).HasMaxLength(80).IsRequired();
        sellado.Property(e => e.Edicion).HasMaxLength(120).IsRequired();
        EnumAsString(sellado.Property(e => e.TipoSellado), 16);

        var contenido = modelBuilder.Entity<ProductoSelladoContenidoFijo>();
        contenido.ToTable("producto_sellado_contenido_fijo");
        contenido.HasKey(e => e.Id);

        contenido.HasOne(e => e.ProductoSellado)
            .WithMany(s => s.ContenidoFijo)
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoSelladoId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Cascade);

        contenido.HasOne(e => e.ProductoComponente)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoComponenteId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        contenido.HasIndex(e => new { e.EmpresaId, e.ProductoSelladoId, e.ProductoComponenteId })
            .IsUnique()
            .HasDatabaseName("ux_prod_sellado_contenido_fijo_comp");
    }

    private static void ConfigureCatalogoTcg(ModelBuilder modelBuilder)
    {
        var serie = modelBuilder.Entity<TcgSerie>();
        serie.ToTable("tcg_series");
        serie.HasKey(e => e.Id);
        serie.Property(e => e.Juego).HasMaxLength(80).IsRequired();
        serie.Property(e => e.Codigo).HasMaxLength(32).IsRequired();
        serie.Property(e => e.Nombre).HasMaxLength(120).IsRequired();

        serie.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_tcg_series_empresa_id");

        serie.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        serie.HasIndex(e => new { e.EmpresaId, e.Juego, e.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_tcg_series_empresa_juego_codigo");

        var set = modelBuilder.Entity<TcgSet>();
        set.ToTable("tcg_sets");
        set.HasKey(e => e.Id);
        set.Property(e => e.Codigo).HasMaxLength(32).IsRequired();
        set.Property(e => e.Nombre).HasMaxLength(120).IsRequired();
        set.Property(e => e.NombreEn).HasMaxLength(120);
        set.Property(e => e.CodigoImpresion).HasMaxLength(32);

        set.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_tcg_sets_empresa_id");

        set.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        set.HasOne(e => e.Serie)
            .WithMany(s => s.Sets)
            .HasForeignKey(e => new { e.EmpresaId, e.SerieId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        set.HasIndex(e => new { e.EmpresaId, e.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_tcg_sets_empresa_codigo");

        set.HasIndex(e => new { e.EmpresaId, e.SerieId })
            .HasDatabaseName("ix_tcg_sets_empresa_serie");

        var ficha = modelBuilder.Entity<TcgCarta>();
        ficha.ToTable("tcg_cartas");
        ficha.HasKey(e => e.Id);
        ficha.Property(e => e.Numero).HasMaxLength(16).IsRequired();
        ficha.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        EnumAsString(ficha.Property(e => e.TipoCarta), 16);
        EnumAsString(ficha.Property(e => e.Rareza), 32);
        ficha.Property(e => e.Artista).HasMaxLength(120);
        ficha.Property(e => e.ImagenOficialUrl).HasMaxLength(500);

        ficha.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_tcg_cartas_empresa_id");

        ficha.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        ficha.HasOne(e => e.Set)
            .WithMany(s => s.Cartas)
            .HasForeignKey(e => new { e.EmpresaId, e.SetId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        ficha.HasIndex(e => new { e.EmpresaId, e.SetId, e.Numero, e.Rareza })
            .IsUnique()
            .HasDatabaseName("ux_tcg_cartas_empresa_set_numero_rareza");

        var productoCarta = modelBuilder.Entity<ProductoCarta>();
        productoCarta.HasOne(e => e.CartaCatalogo)
            .WithMany(c => c.Productos)
            .HasForeignKey(e => e.CartaCatalogoId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        productoCarta.HasIndex(e => e.CartaCatalogoId)
            .HasDatabaseName("ix_producto_cartas_carta_catalogo_id");
    }

    private static void ConfigureStockProducto(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockProducto>();
        entity.ToTable("stocks_productos");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CantidadDisponible).HasPrecision(18, 3);
        entity.Property(e => e.CantidadReservada).HasPrecision(18, 3);
        entity.Property(e => e.CantidadLibre)
            .HasPrecision(18, 3)
            .ValueGeneratedOnAddOrUpdate();

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Sede)
            .WithMany(s => s.Stocks)
            .HasForeignKey(e => new { e.EmpresaId, e.SedeId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Producto)
            .WithMany(p => p.Stocks)
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.SedeId, e.ProductoId })
            .IsUnique()
            .HasDatabaseName("ux_stocks_productos_empresa_sede_producto");
    }

    private static void ConfigureMovimientoInventario(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MovimientoInventario>();
        entity.ToTable("movimientos_inventario");
        entity.HasKey(e => e.Id);
        EnumAsString(entity.Property(e => e.TipoMovimiento), 40);
        entity.Property(e => e.Cantidad).HasPrecision(18, 3);
        entity.Property(e => e.StockAnterior).HasPrecision(18, 3);
        entity.Property(e => e.StockPosterior).HasPrecision(18, 3);
        entity.Property(e => e.ReservadoAnterior).HasPrecision(18, 3);
        entity.Property(e => e.ReservadoPosterior).HasPrecision(18, 3);
        entity.Property(e => e.ReferenciaTipo).HasMaxLength(40);
        entity.Property(e => e.Motivo).HasMaxLength(500);

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Sede)
            .WithMany(s => s.Movimientos)
            .HasForeignKey(e => new { e.EmpresaId, e.SedeId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Producto)
            .WithMany(p => p.Movimientos)
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Usuario)
            .WithMany(u => u.Movimientos)
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.EmpresaId, e.SedeId, e.ProductoId, e.FechaCreacion })
            .HasDatabaseName("ix_movimientos_inventario_empresa_sede_producto_fecha");

        entity.HasIndex(e => new { e.EmpresaId, e.ReferenciaTipo, e.ReferenciaId })
            .HasDatabaseName("ix_movimientos_inventario_empresa_referencia");
    }

    private static void ConfigureAperturaTcg(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AperturaTcg>();
        entity.ToTable("aperturas_tcg");
        entity.HasKey(e => e.Id);
        EnumAsString(entity.Property(e => e.Estado), 16);
        entity.Property(e => e.Observacion).HasMaxLength(500);

        entity.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_aperturas_tcg_empresa_id");

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Sede)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.SedeId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ProductoSellado)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoSelladoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.SedeId, e.FechaCreacion })
            .HasDatabaseName("ix_aperturas_tcg_empresa_sede_fecha");

        var detalle = modelBuilder.Entity<AperturaTcgDetalle>();
        detalle.ToTable("apertura_tcg_detalles");
        detalle.HasKey(e => e.Id);
        detalle.Property(e => e.CostoUnitarioAsignado).HasPrecision(18, 4);
        EnumAsString(detalle.Property(e => e.Estado), 8);

        detalle.HasOne(e => e.Apertura)
            .WithMany(a => a.Detalles)
            .HasForeignKey(e => new { e.EmpresaId, e.AperturaTcgId })
            .HasPrincipalKey(a => new { a.EmpresaId, a.Id })
            .OnDelete(DeleteBehavior.Cascade);

        detalle.HasOne(e => e.ProductoCarta)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoCartaId })
            .HasPrincipalKey(c => new { c.EmpresaId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);

        detalle.HasIndex(e => new { e.EmpresaId, e.AperturaTcgId })
            .HasDatabaseName("ix_apertura_tcg_detalles_empresa_apertura");
    }

    private static void ConfigureSubastaTcg(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SubastaTcg>();
        entity.ToTable("subastas_tcg");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Titulo).HasMaxLength(200).IsRequired();
        EnumAsString(entity.Property(e => e.Canal), 24);
        EnumAsString(entity.Property(e => e.Modo), 16);
        entity.Property(e => e.PrecioBase).HasPrecision(18, 2);
        entity.Property(e => e.IncrementoMinimo).HasPrecision(18, 2);
        entity.Property(e => e.PrecioReserva).HasPrecision(18, 2);
        EnumAsString(entity.Property(e => e.Estado), 16);
        entity.Property(e => e.Observacion).HasMaxLength(500);

        entity.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_subastas_tcg_empresa_id");

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Sede)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.SedeId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.Estado, e.SedeId })
            .HasDatabaseName("ix_subastas_tcg_empresa_estado_sede");

        entity.HasIndex(e => new { e.Estado, e.FechaCierre })
            .HasDatabaseName("ix_subastas_tcg_estado_fecha_cierre");

        var detalle = modelBuilder.Entity<SubastaDetalle>();
        detalle.ToTable("subasta_detalles");
        detalle.HasKey(e => e.Id);
        detalle.Property(e => e.Cantidad).HasPrecision(18, 3);
        detalle.Property(e => e.TituloPersonalizado).HasMaxLength(200);
        EnumAsString(detalle.Property(e => e.Estado), 16);

        detalle.HasOne(e => e.Subasta)
            .WithMany(s => s.Detalles)
            .HasForeignKey(e => new { e.EmpresaId, e.SubastaTcgId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Cascade);

        detalle.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        detalle.HasIndex(e => new { e.EmpresaId, e.SubastaTcgId, e.Orden })
            .HasDatabaseName("ix_subasta_detalles_empresa_subasta_orden");

        var puja = modelBuilder.Entity<Puja>();
        puja.ToTable("pujas");
        puja.HasKey(e => e.Id);
        puja.Property(e => e.Id).ValueGeneratedNever();
        puja.Property(e => e.NombrePostor).HasMaxLength(160).IsRequired();
        puja.Property(e => e.Monto).HasPrecision(18, 2);

        puja.HasOne(e => e.Subasta)
            .WithMany(s => s.Pujas)
            .HasForeignKey(e => new { e.EmpresaId, e.SubastaTcgId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Cascade);

        puja.HasOne(e => e.Detalle)
            .WithMany()
            .HasForeignKey(e => e.SubastaDetalleId)
            .OnDelete(DeleteBehavior.Restrict);

        puja.HasIndex(e => new { e.EmpresaId, e.SubastaTcgId, e.Fecha })
            .HasDatabaseName("ix_pujas_empresa_subasta_fecha");

        puja.HasIndex(e => new { e.EmpresaId, e.SubastaTcgId, e.SubastaDetalleId, e.Fecha })
            .HasDatabaseName("ix_pujas_empresa_subasta_detalle_fecha");
    }

    private static void ConfigurePedidoDigital(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PedidoDigital>();
        entity.ToTable("pedidos_digitales");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ClienteNombre).HasMaxLength(160);
        entity.Property(e => e.ClienteTelefono).HasMaxLength(32);
        EnumAsString(entity.Property(e => e.CanalPedido), 32);
        EnumAsString(entity.Property(e => e.Estado), 24);
        EnumAsString(entity.Property(e => e.IndicadorReserva), 16);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.Igv).HasPrecision(18, 2);
        entity.Property(e => e.Total).HasPrecision(18, 2);
        entity.Property(e => e.ReferenciaExterna).HasMaxLength(80);
        entity.Property(e => e.Observacion).HasMaxLength(500);
        entity.Property(e => e.DestinatarioNombre).HasMaxLength(160).IsRequired().HasDefaultValue("");
        entity.Property(e => e.DestinatarioTelefono).HasMaxLength(32);
        entity.Property(e => e.EntregaDireccion).HasMaxLength(300);
        entity.Property(e => e.EntregaDistrito).HasMaxLength(80);
        entity.Property(e => e.EntregaProvincia).HasMaxLength(80);
        entity.Property(e => e.EntregaDepartamento).HasMaxLength(80);
        entity.Property(e => e.Courier).HasMaxLength(80);
        entity.Property(e => e.EsRecojoTienda).HasDefaultValue(true);
        entity.Property(e => e.NumeroTracking).HasMaxLength(80);
        entity.Property(e => e.CostoEnvio).HasPrecision(18, 2);
        entity.Property(e => e.NotasEmpaque).HasMaxLength(500);
        entity.Property(e => e.Agencia).HasMaxLength(80);
        entity.Property(e => e.PuntoEntrega).HasMaxLength(80);
        EnumAsString(entity.Property(e => e.CanalContacto), 16);
        entity.Property(e => e.ContactoReferencia).HasMaxLength(160);
        entity.Property(e => e.Notificado).HasDefaultValue(false);

        entity.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_pedidos_digitales_empresa_id");

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Sede)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.SedeId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Subasta)
            .WithMany()
            .HasForeignKey(e => e.SubastaTcgId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasOne(e => e.Cliente)
            .WithMany()
            .HasForeignKey(e => e.ClienteId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasIndex(e => new { e.EmpresaId, e.Estado, e.SedeId })
            .HasDatabaseName("ix_pedidos_digitales_empresa_estado_sede");

        entity.HasIndex(e => new { e.EmpresaId, e.ReferenciaExterna })
            .IsUnique()
            .HasFilter("referencia_externa IS NOT NULL")
            .HasDatabaseName("ux_pedidos_digitales_empresa_referencia_externa");

        var detalle = modelBuilder.Entity<PedidoDigitalDetalle>();
        detalle.ToTable("pedido_digital_detalles");
        detalle.HasKey(e => e.Id);
        detalle.Property(e => e.Descripcion).HasMaxLength(200).IsRequired();
        detalle.Property(e => e.Cantidad).HasPrecision(18, 3);
        detalle.Property(e => e.PrecioUnitario).HasPrecision(18, 2);
        detalle.Property(e => e.Total).HasPrecision(18, 2);

        detalle.HasOne(e => e.Pedido)
            .WithMany(p => p.Detalles)
            .HasForeignKey(e => new { e.EmpresaId, e.PedidoDigitalId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Cascade);

        detalle.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        var historial = modelBuilder.Entity<PedidoDigitalHistorialEstado>();
        historial.ToTable("pedido_digital_historial_estados");
        historial.HasKey(e => e.Id);
        EnumAsString(historial.Property(e => e.EstadoAnterior), 24);
        EnumAsString(historial.Property(e => e.EstadoNuevo), 24);
        historial.Property(e => e.Observacion).HasMaxLength(500);

        historial.HasOne(e => e.Pedido)
            .WithMany(p => p.Historial)
            .HasForeignKey(e => new { e.EmpresaId, e.PedidoDigitalId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Cascade);

        historial.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureCliente(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Cliente>();
        entity.ToTable("clientes");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Nombre).HasMaxLength(160).IsRequired();
        entity.Property(e => e.Telefono).HasMaxLength(32);
        entity.Property(e => e.PuntoEntregaPreferido).HasMaxLength(80);
        EnumAsString(entity.Property(e => e.CanalContacto), 16);
        entity.Property(e => e.ContactoReferencia).HasMaxLength(160);
        EnumAsString(entity.Property(e => e.TipoDocumento), 16);
        entity.Property(e => e.NumeroDocumento).HasMaxLength(16);
        entity.Property(e => e.Activo).HasDefaultValue(true);

        entity.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_clientes_empresa_id");

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.NumeroDocumento })
            .HasDatabaseName("ix_clientes_empresa_id_numero_documento");

        entity.HasIndex(e => new { e.EmpresaId, e.Activo })
            .HasDatabaseName("ix_clientes_empresa_id_activo");
    }

    private static void ConfigureProveedor(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Proveedor>();
        entity.ToTable("proveedores");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Ruc).HasMaxLength(11).IsRequired();
        entity.Property(e => e.RazonSocial).HasMaxLength(200).IsRequired();
        entity.Property(e => e.NombreComercial).HasMaxLength(200);
        entity.Property(e => e.Telefono).HasMaxLength(32);

        entity.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_proveedores_empresa_id");

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.Ruc })
            .IsUnique()
            .HasDatabaseName("ux_proveedores_empresa_ruc");
    }

    private static void ConfigureCompra(ModelBuilder modelBuilder)
    {
        var compra = modelBuilder.Entity<Compra>();
        compra.ToTable("compras");
        compra.HasKey(e => e.Id);
        compra.Property(e => e.Observacion).HasMaxLength(500);
        compra.Property(e => e.Total).HasPrecision(18, 2);

        compra.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_compras_empresa_id");

        compra.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        compra.HasOne(e => e.Proveedor)
            .WithMany(p => p.Compras)
            .HasForeignKey(e => new { e.EmpresaId, e.ProveedorId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        compra.HasOne(e => e.Sede)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.SedeId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        compra.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        compra.HasIndex(e => new { e.EmpresaId, e.ProveedorId, e.Fecha })
            .HasDatabaseName("ix_compras_empresa_proveedor_fecha");

        var detalle = modelBuilder.Entity<CompraDetalle>();
        detalle.ToTable("compra_detalles");
        detalle.HasKey(e => e.Id);
        detalle.Property(e => e.Cantidad).HasPrecision(18, 3);
        detalle.Property(e => e.CostoUnitario).HasPrecision(18, 4);
        detalle.Property(e => e.Total).HasPrecision(18, 2);

        detalle.HasOne(e => e.Compra)
            .WithMany(c => c.Detalles)
            .HasForeignKey(e => new { e.EmpresaId, e.CompraId })
            .HasPrincipalKey(c => new { c.EmpresaId, c.Id })
            .OnDelete(DeleteBehavior.Cascade);

        detalle.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePago(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Pago>();
        entity.ToTable("pagos");
        entity.HasKey(e => e.Id);
        EnumAsString(entity.Property(e => e.Origen), 16);
        EnumAsString(entity.Property(e => e.Estado), 16);
        entity.Property(e => e.Monto).HasPrecision(18, 2);
        entity.Property(e => e.CodigoOperacion).HasMaxLength(80);
        entity.Property(e => e.ReferenciaExterna).HasMaxLength(120);
        entity.Property(e => e.ClienteNombre).HasMaxLength(160);
        entity.Property(e => e.Observacion).HasMaxLength(500);

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Pedido)
            .WithMany(p => p.Pagos)
            .HasForeignKey(e => e.PedidoDigitalId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasOne(e => e.UsuarioAsocio)
            .WithMany()
            .HasForeignKey(e => e.UsuarioAsocioId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.EmpresaId, e.Estado, e.Origen })
            .HasDatabaseName("ix_pagos_empresa_estado_origen");

        entity.HasIndex(e => new { e.EmpresaId, e.CodigoOperacion })
            .HasFilter("codigo_operacion IS NOT NULL AND estado <> 'RECHAZADO' AND estado <> 'ANULADO'")
            .IsUnique()
            .HasDatabaseName("ux_pagos_empresa_codigo_operacion");
    }

    private static void ConfigureEntrega(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Entrega>();
        entity.ToTable("entregas");
        entity.HasKey(e => e.Id);
        EnumAsString(entity.Property(e => e.MetodoEnvio), 16);
        EnumAsString(entity.Property(e => e.Estado), 16);
        entity.Property(e => e.DestinatarioNombre).HasMaxLength(160).IsRequired();
        entity.Property(e => e.DestinatarioTelefono).HasMaxLength(32);
        entity.Property(e => e.Direccion).HasMaxLength(300);
        entity.Property(e => e.Distrito).HasMaxLength(80);
        entity.Property(e => e.Provincia).HasMaxLength(80);
        entity.Property(e => e.Departamento).HasMaxLength(80);
        entity.Property(e => e.Agencia).HasMaxLength(80);
        entity.Property(e => e.PuntoEntrega).HasMaxLength(80);
        EnumAsString(entity.Property(e => e.CanalContacto), 16);
        entity.Property(e => e.ContactoReferencia).HasMaxLength(160);
        entity.Property(e => e.NumeroTracking).HasMaxLength(80);
        entity.Property(e => e.CostoEnvio).HasPrecision(18, 2);
        entity.Property(e => e.NotasEmpaque).HasMaxLength(500);
        entity.Property(e => e.Observacion).HasMaxLength(500);

        entity.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Pedido)
            .WithOne(p => p.Entrega)
            .HasForeignKey<Entrega>(e => new { e.EmpresaId, e.PedidoDigitalId })
            .HasPrincipalKey<PedidoDigital>(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.SedeOrigen)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.SedeOrigenId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.EmpresaId, e.PedidoDigitalId })
            .IsUnique()
            .HasDatabaseName("ux_entregas_empresa_pedido");

        entity.HasIndex(e => new { e.EmpresaId, e.Estado, e.SedeOrigenId })
            .HasDatabaseName("ix_entregas_empresa_estado_sede");
    }

    private static void ConfigureVenta(ModelBuilder modelBuilder)
    {
        var venta = modelBuilder.Entity<Venta>();
        venta.ToTable("ventas");
        venta.HasKey(e => e.Id);
        EnumAsString(venta.Property(e => e.Canal), 32);
        venta.Property(e => e.Subtotal).HasPrecision(18, 2);
        venta.Property(e => e.Igv).HasPrecision(18, 2);
        venta.Property(e => e.Total).HasPrecision(18, 2);
        venta.Property(e => e.EsConsolidacion).HasDefaultValue(false);

        venta.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_ventas_empresa_id");

        venta.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        venta.HasOne(e => e.Sede)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.SedeId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        venta.HasOne(e => e.Cliente)
            .WithMany(c => c.Ventas)
            .HasForeignKey(e => e.ClienteId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        venta.HasOne(e => e.Pedido)
            .WithMany()
            .HasForeignKey(e => e.PedidoDigitalId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        venta.HasIndex(e => new { e.EmpresaId, e.PedidoDigitalId })
            .IsUnique()
            .HasFilter("pedido_digital_id IS NOT NULL")
            .HasDatabaseName("ux_ventas_empresa_pedido");

        venta.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        venta.HasOne(e => e.CajaSesion)
            .WithMany(s => s.Ventas)
            .HasForeignKey(e => new { e.EmpresaId, e.CajaSesionId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        venta.HasIndex(e => new { e.EmpresaId, e.Fecha })
            .HasDatabaseName("ix_ventas_empresa_fecha");

        venta.HasIndex(e => new { e.EmpresaId, e.CajaSesionId })
            .HasDatabaseName("ix_ventas_empresa_caja_sesion");

        var detalle = modelBuilder.Entity<VentaDetalle>();
        detalle.ToTable("venta_detalles");
        detalle.HasKey(e => e.Id);
        detalle.Property(e => e.CodigoSku).HasMaxLength(64).IsRequired();
        detalle.Property(e => e.Descripcion).HasMaxLength(200).IsRequired();
        detalle.Property(e => e.Cantidad).HasPrecision(18, 3);
        detalle.Property(e => e.PrecioUnitario).HasPrecision(18, 2);
        detalle.Property(e => e.ValorUnitario).HasPrecision(18, 2);
        detalle.Property(e => e.Subtotal).HasPrecision(18, 2);
        detalle.Property(e => e.Igv).HasPrecision(18, 2);
        detalle.Property(e => e.Total).HasPrecision(18, 2);
        detalle.Property(e => e.CodigoAfectacionIgv).HasMaxLength(2).IsRequired();

        detalle.HasOne(e => e.Venta)
            .WithMany(v => v.Detalles)
            .HasForeignKey(e => new { e.EmpresaId, e.VentaId })
            .HasPrincipalKey(v => new { v.EmpresaId, v.Id })
            .OnDelete(DeleteBehavior.Cascade);

        detalle.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        var ventaPago = modelBuilder.Entity<VentaPago>();
        ventaPago.ToTable("venta_pagos");
        ventaPago.HasKey(e => e.Id);
        EnumAsString(ventaPago.Property(e => e.Origen), 16);
        ventaPago.Property(e => e.Monto).HasPrecision(18, 2);
        ventaPago.Property(e => e.CodigoOperacion).HasMaxLength(80);

        ventaPago.HasOne(e => e.Venta)
            .WithMany(v => v.Pagos)
            .HasForeignKey(e => new { e.EmpresaId, e.VentaId })
            .HasPrincipalKey(v => new { v.EmpresaId, v.Id })
            .OnDelete(DeleteBehavior.Cascade);

        ventaPago.HasOne(e => e.Pago)
            .WithMany()
            .HasForeignKey(e => e.PagoId)
            .OnDelete(DeleteBehavior.SetNull);

        var serie = modelBuilder.Entity<SerieComprobante>();
        serie.ToTable("series_comprobante");
        serie.HasKey(e => e.Id);
        EnumAsString(serie.Property(e => e.Tipo), 24);
        serie.Property(e => e.Serie).HasMaxLength(4).IsRequired();

        serie.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        serie.HasIndex(e => new { e.EmpresaId, e.Tipo, e.Serie })
            .IsUnique()
            .HasDatabaseName("ux_series_comprobante_empresa_tipo_serie");

        var comprobante = modelBuilder.Entity<Comprobante>();
        comprobante.ToTable("comprobantes");
        comprobante.HasKey(e => e.Id);
        EnumAsString(comprobante.Property(e => e.Tipo), 24);
        comprobante.Property(e => e.TipoSunat).HasMaxLength(2).IsRequired();
        comprobante.Property(e => e.Serie).HasMaxLength(4).IsRequired();
        EnumAsString(comprobante.Property(e => e.Estado), 24);
        comprobante.Property(e => e.Mensaje).HasMaxLength(500);
        comprobante.Property(e => e.HashFirma).HasMaxLength(128);
        comprobante.Property(e => e.CodigoMotivo).HasMaxLength(8);
        comprobante.Property(e => e.DescripcionMotivo).HasMaxLength(250);
        comprobante.Property(e => e.DocumentoReferencia).HasMaxLength(32);

        comprobante.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        comprobante.HasOne(e => e.Venta)
            .WithMany(v => v.Comprobantes)
            .HasForeignKey(e => new { e.EmpresaId, e.VentaId })
            .HasPrincipalKey(v => new { v.EmpresaId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        comprobante.HasOne(e => e.BoletaConsolidada)
            .WithMany(b => b.Notas)
            .HasForeignKey(e => e.BoletaConsolidadaId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        comprobante.HasIndex(e => new { e.EmpresaId, e.Serie, e.Correlativo })
            .IsUnique()
            .HasDatabaseName("ux_comprobantes_empresa_serie_correlativo");

        comprobante.HasIndex(e => new { e.EmpresaId, e.VentaId })
            .IsUnique()
            .HasFilter("tipo IN ('BOLETA', 'FACTURA', 'NOTA_VENTA')")
            .HasDatabaseName("ux_comprobantes_empresa_venta");

        comprobante.HasIndex(e => new { e.EmpresaId, e.Tipo, e.Estado, e.FechaEmision })
            .HasDatabaseName("ix_comprobantes_empresa_tipo_estado_fecha");

        var consolidada = modelBuilder.Entity<BoletaConsolidada>();
        consolidada.ToTable("boletas_consolidadas");
        consolidada.HasKey(e => e.Id);
        EnumAsString(consolidada.Property(e => e.Filtro), 24);
        consolidada.Property(e => e.Total).HasPrecision(18, 2);

        consolidada.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        consolidada.HasOne(e => e.Comprobante)
            .WithOne()
            .HasForeignKey<BoletaConsolidada>(e => e.ComprobanteId)
            .OnDelete(DeleteBehavior.Restrict);

        consolidada.HasOne(e => e.Venta)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.VentaId })
            .HasPrincipalKey(v => new { v.EmpresaId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        consolidada.HasIndex(e => e.ComprobanteId)
            .IsUnique()
            .HasDatabaseName("ux_boletas_consolidadas_comprobante_id");

        consolidada.HasIndex(e => new { e.EmpresaId, e.FechaOperacion })
            .HasDatabaseName("ix_boletas_consolidadas_empresa_fecha");
    }

    private static void ConfigureCaja(ModelBuilder modelBuilder)
    {
        var sesion = modelBuilder.Entity<CajaSesion>();
        sesion.ToTable("caja_sesiones");
        sesion.HasKey(e => e.Id);
        sesion.Property(e => e.MontoApertura).HasPrecision(18, 2);
        sesion.Property(e => e.MontoEfectivoTeorico).HasPrecision(18, 2);
        sesion.Property(e => e.MontoEfectivoReal).HasPrecision(18, 2);
        sesion.Property(e => e.Diferencia).HasPrecision(18, 2);
        EnumAsString(sesion.Property(e => e.Estado), 16);
        sesion.Property(e => e.ObservacionApertura).HasMaxLength(500);
        sesion.Property(e => e.ObservacionCierre).HasMaxLength(500);

        sesion.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_caja_sesiones_empresa_id");

        sesion.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        sesion.HasOne(e => e.Sede)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.SedeId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        sesion.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        sesion.HasOne(e => e.UsuarioCierre)
            .WithMany()
            .HasForeignKey(e => e.UsuarioCierreId)
            .OnDelete(DeleteBehavior.SetNull);

        sesion.HasIndex(e => new { e.EmpresaId, e.SedeId, e.FechaApertura })
            .HasDatabaseName("ix_caja_sesiones_empresa_sede_fecha");

        sesion.HasIndex(e => new { e.EmpresaId, e.SedeId })
            .IsUnique()
            .HasFilter("estado = 'ABIERTA'")
            .HasDatabaseName("ux_caja_sesiones_empresa_sede_abierta");

        var movimiento = modelBuilder.Entity<CajaMovimiento>();
        movimiento.ToTable("caja_movimientos");
        movimiento.HasKey(e => e.Id);
        EnumAsString(movimiento.Property(e => e.Tipo), 16);
        movimiento.Property(e => e.Monto).HasPrecision(18, 2);
        movimiento.Property(e => e.Concepto).HasMaxLength(200).IsRequired();

        movimiento.HasOne(e => e.Sesion)
            .WithMany(s => s.Movimientos)
            .HasForeignKey(e => new { e.EmpresaId, e.CajaSesionId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Cascade);

        movimiento.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        movimiento.HasIndex(e => new { e.EmpresaId, e.CajaSesionId, e.Fecha })
            .HasDatabaseName("ix_caja_movimientos_empresa_sesion_fecha");
    }

    private static void ConfigureWooCommerce(ModelBuilder modelBuilder)
    {
        var integracion = modelBuilder.Entity<IntegracionWooCommerce>();
        integracion.ToTable("integraciones_woocommerce");
        integracion.HasKey(e => e.Id);
        integracion.Property(e => e.UrlTienda).HasMaxLength(300).IsRequired();
        integracion.Property(e => e.ConsumerKeyCifrado).IsRequired();
        integracion.Property(e => e.ConsumerSecretCifrado).IsRequired();
        EnumAsString(integracion.Property(e => e.ModoSincronizacion), 16);
        EnumAsString(integracion.Property(e => e.ModoRecepcionPedidos), 16);
        EnumAsString(integracion.Property(e => e.EstadoConexion), 24);
        integracion.Property(e => e.MensajeConexion).HasMaxLength(500);

        integracion.HasAlternateKey(e => new { e.EmpresaId, e.Id })
            .HasName("ak_integraciones_woocommerce_empresa_id");

        integracion.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        integracion.HasOne(e => e.SedeOrigen)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.SedeOrigenId })
            .HasPrincipalKey(s => new { s.EmpresaId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        integracion.HasIndex(e => e.EmpresaId)
            .IsUnique()
            .HasDatabaseName("ux_integraciones_woocommerce_empresa_id");

        var mapeo = modelBuilder.Entity<WooCommerceMapeoProducto>();
        mapeo.ToTable("woocommerce_mapeos_producto");
        mapeo.HasKey(e => e.Id);
        mapeo.Property(e => e.PrecioNormalWoo).HasPrecision(18, 2);
        mapeo.Property(e => e.PrecioRebajadoWoo).HasPrecision(18, 2);
        mapeo.Property(e => e.StockWoo).HasPrecision(18, 3);
        EnumAsString(mapeo.Property(e => e.EstadoMapeo), 24);
        mapeo.Property(e => e.Mensaje).HasMaxLength(500);

        mapeo.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        mapeo.HasOne(e => e.Integracion)
            .WithMany(i => i.Mapeos)
            .HasForeignKey(e => new { e.EmpresaId, e.IntegracionId })
            .HasPrincipalKey(i => new { i.EmpresaId, i.Id })
            .OnDelete(DeleteBehavior.Cascade);

        mapeo.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => new { e.EmpresaId, e.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        mapeo.HasIndex(e => new { e.EmpresaId, e.ProductoId })
            .IsUnique()
            .HasDatabaseName("ux_woocommerce_mapeos_empresa_producto");

        mapeo.HasIndex(e => new { e.EmpresaId, e.WooProductId })
            .IsUnique()
            .HasFilter("woo_product_id IS NOT NULL AND woo_variation_id IS NULL")
            .HasDatabaseName("ux_woocommerce_mapeos_empresa_woo_simple");

        mapeo.HasIndex(e => new { e.EmpresaId, e.WooProductId, e.WooVariationId })
            .IsUnique()
            .HasFilter("woo_product_id IS NOT NULL AND woo_variation_id IS NOT NULL")
            .HasDatabaseName("ux_woocommerce_mapeos_empresa_woo_variation");

        var log = modelBuilder.Entity<WooCommerceSyncLog>();
        log.ToTable("woocommerce_sync_logs");
        log.HasKey(e => e.Id);
        EnumAsString(log.Property(e => e.Tipo), 16);
        EnumAsString(log.Property(e => e.Estado), 16);
        log.Property(e => e.PayloadResumen).HasMaxLength(2000).IsRequired();
        log.Property(e => e.PayloadJson);
        log.Property(e => e.MensajeError).HasMaxLength(500);
        log.Property(e => e.Intentos).HasDefaultValue(0);
        log.HasIndex(e => new { e.Estado, e.ProximoReintento })
            .HasFilter("estado = 'REINTENTO'")
            .HasDatabaseName("ix_woocommerce_sync_logs_reintento");

        log.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        log.HasOne(e => e.Integracion)
            .WithMany(i => i.Logs)
            .HasForeignKey(e => new { e.EmpresaId, e.IntegracionId })
            .HasPrincipalKey(i => new { i.EmpresaId, i.Id })
            .OnDelete(DeleteBehavior.Cascade);

        log.HasIndex(e => new { e.EmpresaId, e.Fecha })
            .HasDatabaseName("ix_woocommerce_sync_logs_empresa_fecha");
    }

    private static void ConfigureEcosistema(ModelBuilder modelBuilder)
    {
        var fiscal = modelBuilder.Entity<ConfiguracionFiscalEmpresa>();
        fiscal.ToTable("configuracion_fiscal_empresa");
        fiscal.HasKey(e => e.Id);
        fiscal.Property(e => e.Ruc).HasMaxLength(11).IsRequired();
        fiscal.Property(e => e.RazonSocial).HasMaxLength(200).IsRequired();
        fiscal.Property(e => e.NombreComercial).HasMaxLength(200).IsRequired();
        fiscal.Property(e => e.DireccionFiscal).HasMaxLength(300).IsRequired();
        fiscal.Property(e => e.Ubigeo).HasMaxLength(6).IsRequired();
        fiscal.Property(e => e.Departamento).HasMaxLength(80).IsRequired();
        fiscal.Property(e => e.Provincia).HasMaxLength(80).IsRequired();
        fiscal.Property(e => e.Distrito).HasMaxLength(80).IsRequired();

        fiscal.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        fiscal.HasIndex(e => e.EmpresaId)
            .IsUnique()
            .HasDatabaseName("ux_configuracion_fiscal_empresa_id");

        var conexion = modelBuilder.Entity<EcosistemaConexion>();
        conexion.ToTable("ecosistema_conexiones");
        conexion.HasKey(e => e.Id);
        EnumAsString(conexion.Property(e => e.Tipo), 16);
        conexion.Property(e => e.Nombre).HasMaxLength(80).IsRequired();
        EnumAsString(conexion.Property(e => e.EstadoSalud), 16);
        conexion.Property(e => e.UltimoError).HasMaxLength(500);

        conexion.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        conexion.HasIndex(e => new { e.EmpresaId, e.Tipo })
            .IsUnique()
            .HasDatabaseName("ux_ecosistema_conexiones_empresa_tipo");
    }

    private static void ConfigureIzipay(ModelBuilder modelBuilder)
    {
        var integracion = modelBuilder.Entity<IntegracionIzipay>();
        integracion.ToTable("integraciones_izipay");
        integracion.HasKey(e => e.Id);
        integracion.Property(e => e.ShopId).HasMaxLength(32).IsRequired();
        integracion.Property(e => e.HmacSha256Clave).IsRequired();
        EnumAsString(integracion.Property(e => e.Modo), 8);
        integracion.Property(e => e.Modo).HasDefaultValue(ModoIzipay.TEST);

        integracion.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        integracion.HasIndex(e => e.EmpresaId)
            .IsUnique()
            .HasDatabaseName("ux_integraciones_izipay_empresa_id");

        var log = modelBuilder.Entity<PasarelaWebhookLog>();
        log.ToTable("pasarela_webhook_logs");
        log.HasKey(e => e.Id);
        EnumAsString(log.Property(e => e.Proveedor), 16);
        log.Property(e => e.TransaccionUuid).HasMaxLength(80).IsRequired();
        log.Property(e => e.PayloadRaw).IsRequired();
        EnumAsString(log.Property(e => e.Estado), 24);

        log.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        log.HasOne(e => e.Pago)
            .WithMany()
            .HasForeignKey(e => e.PagoId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        log.HasIndex(e => new { e.EmpresaId, e.TransaccionUuid })
            .IsUnique()
            .HasDatabaseName("ux_pasarela_webhook_logs_empresa_transaccion");

        log.HasIndex(e => new { e.EmpresaId, e.CreatedAt })
            .HasDatabaseName("ix_pasarela_webhook_logs_empresa_created_at");
    }

    private static void EnumAsString<TEnum>(PropertyBuilder<TEnum> property, int maxLength)
        where TEnum : struct, Enum
    {
        property.HasConversion<string>().HasMaxLength(maxLength).IsUnicode(false);
    }

    private static void EnumAsString<TEnum>(PropertyBuilder<TEnum?> property, int maxLength)
        where TEnum : struct, Enum
    {
        property.HasConversion<string>().HasMaxLength(maxLength).IsUnicode(false);
    }
}
