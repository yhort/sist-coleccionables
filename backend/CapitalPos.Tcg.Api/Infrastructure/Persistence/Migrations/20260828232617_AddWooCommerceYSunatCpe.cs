using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWooCommerceYSunatCpe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pedidos_digitales_empresa_referencia_externa",
                table: "pedidos_digitales");

            migrationBuilder.DropIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes");

            migrationBuilder.AddColumn<string>(
                name: "codigo_motivo",
                table: "comprobantes",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion_motivo",
                table: "comprobantes",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "documento_referencia",
                table: "comprobantes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_envio_sunat",
                table: "comprobantes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hash_firma",
                table: "comprobantes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "configuracion_fiscal_empresa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion_fiscal = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ubigeo = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    departamento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provincia = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    distrito = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_fiscal_empresa", x => x.id);
                    table.ForeignKey(
                        name: "FK_configuracion_fiscal_empresa_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ecosistema_conexiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    estado_salud = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    ultimo_ping = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    configuracion_json = table.Column<string>(type: "text", nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecosistema_conexiones", x => x.id);
                    table.ForeignKey(
                        name: "FK_ecosistema_conexiones_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integraciones_woocommerce",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url_tienda = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    consumer_key_cifrado = table.Column<string>(type: "text", nullable: false),
                    consumer_secret_cifrado = table.Column<string>(type: "text", nullable: false),
                    sede_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modo_sincronizacion = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    modo_recepcion_pedidos = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    estado_conexion = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: false),
                    mensaje_conexion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ultimo_intento_conexion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integraciones_woocommerce", x => x.id);
                    table.UniqueConstraint("ak_integraciones_woocommerce_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_integraciones_woocommerce_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_integraciones_woocommerce_sedes_empresa_id_sede_origen_id",
                        columns: x => new { x.empresa_id, x.sede_origen_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "woocommerce_mapeos_producto",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    integracion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    woo_product_id = table.Column<long>(type: "bigint", nullable: true),
                    woo_variation_id = table.Column<long>(type: "bigint", nullable: true),
                    precio_normal_woo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    precio_rebajado_woo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    stock_woo = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    estado_mapeo = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ultima_sincronizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_woocommerce_mapeos_producto", x => x.id);
                    table.ForeignKey(
                        name: "FK_woocommerce_mapeos_producto_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_woocommerce_mapeos_producto_integraciones_woocommerce_empre~",
                        columns: x => new { x.empresa_id, x.integracion_id },
                        principalTable: "integraciones_woocommerce",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_woocommerce_mapeos_producto_productos_empresa_id_producto_id",
                        columns: x => new { x.empresa_id, x.producto_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "woocommerce_sync_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    integracion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    payload_resumen = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    mensaje_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_woocommerce_sync_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_woocommerce_sync_logs_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_woocommerce_sync_logs_integraciones_woocommerce_empresa_id_~",
                        columns: x => new { x.empresa_id, x.integracion_id },
                        principalTable: "integraciones_woocommerce",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_digitales_empresa_referencia_externa",
                table: "pedidos_digitales",
                columns: new[] { "empresa_id", "referencia_externa" },
                unique: true,
                filter: "referencia_externa IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes",
                columns: new[] { "empresa_id", "venta_id" },
                unique: true,
                filter: "tipo IN ('BOLETA', 'FACTURA')");

            migrationBuilder.CreateIndex(
                name: "ux_configuracion_fiscal_empresa_id",
                table: "configuracion_fiscal_empresa",
                column: "empresa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ecosistema_conexiones_empresa_tipo",
                table: "ecosistema_conexiones",
                columns: new[] { "empresa_id", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integraciones_woocommerce_empresa_id_sede_origen_id",
                table: "integraciones_woocommerce",
                columns: new[] { "empresa_id", "sede_origen_id" });

            migrationBuilder.CreateIndex(
                name: "ux_integraciones_woocommerce_empresa_id",
                table: "integraciones_woocommerce",
                column: "empresa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_woocommerce_mapeos_producto_empresa_id_integracion_id",
                table: "woocommerce_mapeos_producto",
                columns: new[] { "empresa_id", "integracion_id" });

            migrationBuilder.CreateIndex(
                name: "ux_woocommerce_mapeos_empresa_producto",
                table: "woocommerce_mapeos_producto",
                columns: new[] { "empresa_id", "producto_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_woocommerce_mapeos_empresa_woo_product",
                table: "woocommerce_mapeos_producto",
                columns: new[] { "empresa_id", "woo_product_id" },
                unique: true,
                filter: "woo_product_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_woocommerce_sync_logs_empresa_fecha",
                table: "woocommerce_sync_logs",
                columns: new[] { "empresa_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_woocommerce_sync_logs_empresa_id_integracion_id",
                table: "woocommerce_sync_logs",
                columns: new[] { "empresa_id", "integracion_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracion_fiscal_empresa");

            migrationBuilder.DropTable(
                name: "ecosistema_conexiones");

            migrationBuilder.DropTable(
                name: "woocommerce_mapeos_producto");

            migrationBuilder.DropTable(
                name: "woocommerce_sync_logs");

            migrationBuilder.DropTable(
                name: "integraciones_woocommerce");

            migrationBuilder.DropIndex(
                name: "ux_pedidos_digitales_empresa_referencia_externa",
                table: "pedidos_digitales");

            migrationBuilder.DropIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes");

            migrationBuilder.DropColumn(
                name: "codigo_motivo",
                table: "comprobantes");

            migrationBuilder.DropColumn(
                name: "descripcion_motivo",
                table: "comprobantes");

            migrationBuilder.DropColumn(
                name: "documento_referencia",
                table: "comprobantes");

            migrationBuilder.DropColumn(
                name: "fecha_envio_sunat",
                table: "comprobantes");

            migrationBuilder.DropColumn(
                name: "hash_firma",
                table: "comprobantes");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_digitales_empresa_referencia_externa",
                table: "pedidos_digitales",
                columns: new[] { "empresa_id", "referencia_externa" });

            migrationBuilder.CreateIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes",
                columns: new[] { "empresa_id", "venta_id" },
                unique: true);
        }
    }
}
