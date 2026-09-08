using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAperturasYSubastas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aperturas_tcg",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_sellado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_sellados = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_confirmacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aperturas_tcg", x => x.id);
                    table.UniqueConstraint("ak_aperturas_tcg_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_aperturas_tcg_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_aperturas_tcg_productos_empresa_id_producto_sellado_id",
                        columns: x => new { x.empresa_id, x.producto_sellado_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_aperturas_tcg_sedes_empresa_id_sede_id",
                        columns: x => new { x.empresa_id, x.sede_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_aperturas_tcg_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subastas_tcg",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    canal = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: false),
                    precio_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    incremento_minimo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    precio_reserva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    puja_ganadora_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pedido_digital_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subastas_tcg", x => x.id);
                    table.UniqueConstraint("ak_subastas_tcg_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_subastas_tcg_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subastas_tcg_productos_empresa_id_producto_id",
                        columns: x => new { x.empresa_id, x.producto_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subastas_tcg_sedes_empresa_id_sede_id",
                        columns: x => new { x.empresa_id, x.sede_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "apertura_tcg_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    apertura_tcg_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_carta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    costo_unitario_asignado = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    estado = table.Column<string>(type: "character varying(8)", unicode: false, maxLength: 8, nullable: false),
                    es_foil = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apertura_tcg_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_apertura_tcg_detalles_aperturas_tcg_empresa_id_apertura_tcg~",
                        columns: x => new { x.empresa_id, x.apertura_tcg_id },
                        principalTable: "aperturas_tcg",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_apertura_tcg_detalles_productos_empresa_id_producto_carta_id",
                        columns: x => new { x.empresa_id, x.producto_carta_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedidos_digitales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cliente_nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal_pedido = table.Column<string>(type: "character varying(32)", unicode: false, maxLength: 32, nullable: false),
                    estado = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: false),
                    indicador_reserva = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    fecha_pedido = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    igv = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    referencia_externa = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subasta_tcg_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos_digitales", x => x.id);
                    table.UniqueConstraint("ak_pedidos_digitales_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_pedidos_digitales_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pedidos_digitales_sedes_empresa_id_sede_id",
                        columns: x => new { x.empresa_id, x.sede_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pedidos_digitales_subastas_tcg_subasta_tcg_id",
                        column: x => x.subasta_tcg_id,
                        principalTable: "subastas_tcg",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pujas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subasta_tcg_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre_postor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    es_ganadora = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pujas", x => x.id);
                    table.ForeignKey(
                        name: "FK_pujas_subastas_tcg_empresa_id_subasta_tcg_id",
                        columns: x => new { x.empresa_id, x.subasta_tcg_id },
                        principalTable: "subastas_tcg",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pedido_digital_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_digital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido_digital_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_pedido_digital_detalles_pedidos_digitales_empresa_id_pedido~",
                        columns: x => new { x.empresa_id, x.pedido_digital_id },
                        principalTable: "pedidos_digitales",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pedido_digital_detalles_productos_empresa_id_producto_id",
                        columns: x => new { x.empresa_id, x.producto_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido_digital_historial_estados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_digital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: true),
                    estado_nuevo = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido_digital_historial_estados", x => x.id);
                    table.ForeignKey(
                        name: "FK_pedido_digital_historial_estados_pedidos_digitales_empresa_~",
                        columns: x => new { x.empresa_id, x.pedido_digital_id },
                        principalTable: "pedidos_digitales",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pedido_digital_historial_estados_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_apertura_tcg_detalles_empresa_apertura",
                table: "apertura_tcg_detalles",
                columns: new[] { "empresa_id", "apertura_tcg_id" });

            migrationBuilder.CreateIndex(
                name: "IX_apertura_tcg_detalles_empresa_id_producto_carta_id",
                table: "apertura_tcg_detalles",
                columns: new[] { "empresa_id", "producto_carta_id" });

            migrationBuilder.CreateIndex(
                name: "IX_aperturas_tcg_empresa_id_producto_sellado_id",
                table: "aperturas_tcg",
                columns: new[] { "empresa_id", "producto_sellado_id" });

            migrationBuilder.CreateIndex(
                name: "ix_aperturas_tcg_empresa_sede_fecha",
                table: "aperturas_tcg",
                columns: new[] { "empresa_id", "sede_id", "fecha_creacion" });

            migrationBuilder.CreateIndex(
                name: "IX_aperturas_tcg_usuario_id",
                table: "aperturas_tcg",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_digital_detalles_empresa_id_pedido_digital_id",
                table: "pedido_digital_detalles",
                columns: new[] { "empresa_id", "pedido_digital_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pedido_digital_detalles_empresa_id_producto_id",
                table: "pedido_digital_detalles",
                columns: new[] { "empresa_id", "producto_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pedido_digital_historial_estados_empresa_id_pedido_digital_~",
                table: "pedido_digital_historial_estados",
                columns: new[] { "empresa_id", "pedido_digital_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pedido_digital_historial_estados_usuario_id",
                table: "pedido_digital_historial_estados",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_digitales_empresa_estado_sede",
                table: "pedidos_digitales",
                columns: new[] { "empresa_id", "estado", "sede_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_digitales_empresa_id_sede_id",
                table: "pedidos_digitales",
                columns: new[] { "empresa_id", "sede_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_digitales_empresa_referencia_externa",
                table: "pedidos_digitales",
                columns: new[] { "empresa_id", "referencia_externa" });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_digitales_subasta_tcg_id",
                table: "pedidos_digitales",
                column: "subasta_tcg_id");

            migrationBuilder.CreateIndex(
                name: "ix_pujas_empresa_subasta_fecha",
                table: "pujas",
                columns: new[] { "empresa_id", "subasta_tcg_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_subastas_tcg_empresa_estado_sede",
                table: "subastas_tcg",
                columns: new[] { "empresa_id", "estado", "sede_id" });

            migrationBuilder.CreateIndex(
                name: "IX_subastas_tcg_empresa_id_producto_id",
                table: "subastas_tcg",
                columns: new[] { "empresa_id", "producto_id" });

            migrationBuilder.CreateIndex(
                name: "IX_subastas_tcg_empresa_id_sede_id",
                table: "subastas_tcg",
                columns: new[] { "empresa_id", "sede_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apertura_tcg_detalles");

            migrationBuilder.DropTable(
                name: "pedido_digital_detalles");

            migrationBuilder.DropTable(
                name: "pedido_digital_historial_estados");

            migrationBuilder.DropTable(
                name: "pujas");

            migrationBuilder.DropTable(
                name: "aperturas_tcg");

            migrationBuilder.DropTable(
                name: "pedidos_digitales");

            migrationBuilder.DropTable(
                name: "subastas_tcg");
        }
    }
}
