using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidosPagosYVentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agencia",
                table: "pedidos_digitales",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cliente_telefono",
                table: "pedidos_digitales",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "costo_envio",
                table: "pedidos_digitales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "courier",
                table: "pedidos_digitales",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "destinatario_nombre",
                table: "pedidos_digitales",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "destinatario_telefono",
                table: "pedidos_digitales",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_departamento",
                table: "pedidos_digitales",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_direccion",
                table: "pedidos_digitales",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_distrito",
                table: "pedidos_digitales",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_provincia",
                table: "pedidos_digitales",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_recojo_tienda",
                table: "pedidos_digitales",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "notas_empaque",
                table: "pedidos_digitales",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_tracking",
                table: "pedidos_digitales",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "venta_id",
                table: "pedidos_digitales",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE pedidos_digitales
                SET destinatario_nombre = COALESCE(NULLIF(cliente_nombre, ''), 'Cliente'),
                    es_recojo_tienda = TRUE,
                    courier = COALESCE(courier, 'Recojo en tienda')
                WHERE destinatario_nombre IS NULL OR destinatario_nombre = '';
                """);

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    telefono = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tipo_documento = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    numero_documento = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.id);
                    table.UniqueConstraint("ak_clientes_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_clientes_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entregas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_digital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metodo_envio = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    destinatario_nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    destinatario_telefono = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    distrito = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    provincia = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    departamento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    agencia = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    numero_tracking = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    costo_envio = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    notas_empaque = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_programada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_despacho = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_entrega = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entregas", x => x.id);
                    table.ForeignKey(
                        name: "FK_entregas_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entregas_pedidos_digitales_empresa_id_pedido_digital_id",
                        columns: x => new { x.empresa_id, x.pedido_digital_id },
                        principalTable: "pedidos_digitales",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entregas_sedes_empresa_id_sede_origen_id",
                        columns: x => new { x.empresa_id, x.sede_origen_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "series_comprobante",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    serie = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    correlativo = table.Column<int>(type: "integer", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series_comprobante", x => x.id);
                    table.ForeignKey(
                        name: "FK_series_comprobante_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ventas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pedido_digital_id = table.Column<Guid>(type: "uuid", nullable: true),
                    canal = table.Column<string>(type: "character varying(32)", unicode: false, maxLength: 32, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    igv = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas", x => x.id);
                    table.UniqueConstraint("ak_ventas_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_ventas_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_pedidos_digitales_pedido_digital_id",
                        column: x => x.pedido_digital_id,
                        principalTable: "pedidos_digitales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_sedes_empresa_id_sede_id",
                        columns: x => new { x.empresa_id, x.sede_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "comprobantes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    tipo_sunat = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    serie = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    correlativo = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true),
                    cdr = table.Column<string>(type: "text", nullable: true),
                    mensaje = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_emision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comprobantes", x => x.id);
                    table.ForeignKey(
                        name: "FK_comprobantes_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_comprobantes_ventas_empresa_id_venta_id",
                        columns: x => new { x.empresa_id, x.venta_id },
                        principalTable: "ventas",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pagos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    codigo_operacion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    referencia_externa = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    pedido_digital_id = table.Column<Guid>(type: "uuid", nullable: true),
                    venta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cliente_nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    fecha_notificacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_confirmacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    usuario_asocio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagos_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pagos_pedidos_digitales_pedido_digital_id",
                        column: x => x.pedido_digital_id,
                        principalTable: "pedidos_digitales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pagos_usuarios_usuario_asocio_id",
                        column: x => x.usuario_asocio_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pagos_ventas_venta_id",
                        column: x => x.venta_id,
                        principalTable: "ventas",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "venta_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valor_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    igv = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    codigo_afectacion_igv = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venta_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_venta_detalles_productos_empresa_id_producto_id",
                        columns: x => new { x.empresa_id, x.producto_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_venta_detalles_ventas_empresa_id_venta_id",
                        columns: x => new { x.empresa_id, x.venta_id },
                        principalTable: "ventas",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venta_pagos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pago_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origen = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    codigo_operacion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venta_pagos", x => x.id);
                    table.ForeignKey(
                        name: "FK_venta_pagos_pagos_pago_id",
                        column: x => x.pago_id,
                        principalTable: "pagos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_venta_pagos_ventas_empresa_id_venta_id",
                        columns: x => new { x.empresa_id, x.venta_id },
                        principalTable: "ventas",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_digitales_cliente_id",
                table: "pedidos_digitales",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_empresa_id_numero_documento",
                table: "clientes",
                columns: new[] { "empresa_id", "numero_documento" });

            migrationBuilder.CreateIndex(
                name: "ux_comprobantes_empresa_serie_correlativo",
                table: "comprobantes",
                columns: new[] { "empresa_id", "serie", "correlativo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes",
                columns: new[] { "empresa_id", "venta_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entregas_empresa_estado_sede",
                table: "entregas",
                columns: new[] { "empresa_id", "estado", "sede_origen_id" });

            migrationBuilder.CreateIndex(
                name: "IX_entregas_empresa_id_sede_origen_id",
                table: "entregas",
                columns: new[] { "empresa_id", "sede_origen_id" });

            migrationBuilder.CreateIndex(
                name: "ux_entregas_empresa_pedido",
                table: "entregas",
                columns: new[] { "empresa_id", "pedido_digital_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pagos_empresa_estado_origen",
                table: "pagos",
                columns: new[] { "empresa_id", "estado", "origen" });

            migrationBuilder.CreateIndex(
                name: "IX_pagos_pedido_digital_id",
                table: "pagos",
                column: "pedido_digital_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_usuario_asocio_id",
                table: "pagos",
                column: "usuario_asocio_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_venta_id",
                table: "pagos",
                column: "venta_id");

            migrationBuilder.CreateIndex(
                name: "ux_pagos_empresa_codigo_operacion",
                table: "pagos",
                columns: new[] { "empresa_id", "codigo_operacion" },
                unique: true,
                filter: "codigo_operacion IS NOT NULL AND estado <> 'RECHAZADO'");

            migrationBuilder.CreateIndex(
                name: "ux_series_comprobante_empresa_tipo_serie",
                table: "series_comprobante",
                columns: new[] { "empresa_id", "tipo", "serie" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venta_detalles_empresa_id_producto_id",
                table: "venta_detalles",
                columns: new[] { "empresa_id", "producto_id" });

            migrationBuilder.CreateIndex(
                name: "IX_venta_detalles_empresa_id_venta_id",
                table: "venta_detalles",
                columns: new[] { "empresa_id", "venta_id" });

            migrationBuilder.CreateIndex(
                name: "IX_venta_pagos_empresa_id_venta_id",
                table: "venta_pagos",
                columns: new[] { "empresa_id", "venta_id" });

            migrationBuilder.CreateIndex(
                name: "IX_venta_pagos_pago_id",
                table: "venta_pagos",
                column: "pago_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_cliente_id",
                table: "ventas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_empresa_fecha",
                table: "ventas",
                columns: new[] { "empresa_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_ventas_empresa_id_sede_id",
                table: "ventas",
                columns: new[] { "empresa_id", "sede_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ventas_pedido_digital_id",
                table: "ventas",
                column: "pedido_digital_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_usuario_id",
                table: "ventas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_ventas_empresa_pedido",
                table: "ventas",
                columns: new[] { "empresa_id", "pedido_digital_id" },
                unique: true,
                filter: "pedido_digital_id IS NOT NULL");

            migrationBuilder.Sql("""
                UPDATE pedidos_digitales
                SET cliente_id = NULL
                WHERE cliente_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM clientes c WHERE c.id = pedidos_digitales.cliente_id);
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_pedidos_digitales_clientes_cliente_id",
                table: "pedidos_digitales",
                column: "cliente_id",
                principalTable: "clientes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pedidos_digitales_clientes_cliente_id",
                table: "pedidos_digitales");

            migrationBuilder.DropTable(
                name: "comprobantes");

            migrationBuilder.DropTable(
                name: "entregas");

            migrationBuilder.DropTable(
                name: "series_comprobante");

            migrationBuilder.DropTable(
                name: "venta_detalles");

            migrationBuilder.DropTable(
                name: "venta_pagos");

            migrationBuilder.DropTable(
                name: "pagos");

            migrationBuilder.DropTable(
                name: "ventas");

            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropIndex(
                name: "IX_pedidos_digitales_cliente_id",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "agencia",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "cliente_telefono",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "costo_envio",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "courier",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "destinatario_nombre",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "destinatario_telefono",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "entrega_departamento",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "entrega_direccion",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "entrega_distrito",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "entrega_provincia",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "es_recojo_tienda",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "notas_empaque",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "numero_tracking",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "venta_id",
                table: "pedidos_digitales");
        }
    }
}
