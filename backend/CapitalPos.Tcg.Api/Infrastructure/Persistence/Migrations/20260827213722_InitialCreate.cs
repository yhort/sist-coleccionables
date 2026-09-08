using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "empresas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_producto = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo_sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    codigo_barras = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    precio_venta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    costo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    marca_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productos", x => x.id);
                    table.UniqueConstraint("ak_productos_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_productos_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sedes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    distrito = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    provincia = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    departamento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ubigeo = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    es_punto_partida_gre = table.Column<bool>(type: "boolean", nullable: false),
                    es_punto_llegada_gre = table.Column<bool>(type: "boolean", nullable: false),
                    es_almacen_principal = table.Column<bool>(type: "boolean", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sedes", x => x.id);
                    table.UniqueConstraint("ak_sedes_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_sedes_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rol = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuarios_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "producto_cartas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    juego = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    set_codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    set_nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    numero_carta = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rareza = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    idioma = table.Column<string>(type: "character varying(8)", unicode: false, maxLength: 8, nullable: false),
                    condicion = table.Column<string>(type: "character varying(8)", unicode: false, maxLength: 8, nullable: false),
                    es_foil = table.Column<bool>(type: "boolean", nullable: false),
                    artista = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producto_cartas", x => x.id);
                    table.ForeignKey(
                        name: "FK_producto_cartas_productos_id",
                        column: x => x.id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "producto_sellados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    juego = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    edicion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo_sellado = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    cartas_esperadas = table.Column<int>(type: "integer", nullable: false),
                    permite_apertura = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producto_sellados", x => x.id);
                    table.ForeignKey(
                        name: "FK_producto_sellados_productos_id",
                        column: x => x.id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stocks_productos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_disponible = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    cantidad_libre = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false, computedColumnSql: "cantidad_disponible - cantidad_reservada", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocks_productos", x => x.id);
                    table.ForeignKey(
                        name: "FK_stocks_productos_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stocks_productos_productos_empresa_id_producto_id",
                        columns: x => new { x.empresa_id, x.producto_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stocks_productos_sedes_empresa_id_sede_id",
                        columns: x => new { x.empresa_id, x.sede_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_movimiento = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    stock_anterior = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    stock_posterior = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    reservado_anterior = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    reservado_posterior = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    referencia_tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos_inventario", x => x.id);
                    table.ForeignKey(
                        name: "FK_movimientos_inventario_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_inventario_productos_empresa_id_producto_id",
                        columns: x => new { x.empresa_id, x.producto_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_inventario_sedes_empresa_id_sede_id",
                        columns: x => new { x.empresa_id, x.sede_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_inventario_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ux_empresas_ruc",
                table: "empresas",
                column: "ruc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_inventario_empresa_id_producto_id",
                table: "movimientos_inventario",
                columns: new[] { "empresa_id", "producto_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_inventario_empresa_referencia",
                table: "movimientos_inventario",
                columns: new[] { "empresa_id", "referencia_tipo", "referencia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_inventario_empresa_sede_producto_fecha",
                table: "movimientos_inventario",
                columns: new[] { "empresa_id", "sede_id", "producto_id", "fecha_creacion" });

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_inventario_usuario_id",
                table: "movimientos_inventario",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_productos_empresa_id_codigo_sku",
                table: "productos",
                columns: new[] { "empresa_id", "codigo_sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sedes_empresa_id_nombre",
                table: "sedes",
                columns: new[] { "empresa_id", "nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_stocks_productos_empresa_id_producto_id",
                table: "stocks_productos",
                columns: new[] { "empresa_id", "producto_id" });

            migrationBuilder.CreateIndex(
                name: "ux_stocks_productos_empresa_sede_producto",
                table: "stocks_productos",
                columns: new[] { "empresa_id", "sede_id", "producto_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_empresa_id_email",
                table: "usuarios",
                columns: new[] { "empresa_id", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimientos_inventario");

            migrationBuilder.DropTable(
                name: "producto_cartas");

            migrationBuilder.DropTable(
                name: "producto_sellados");

            migrationBuilder.DropTable(
                name: "stocks_productos");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "productos");

            migrationBuilder.DropTable(
                name: "sedes");

            migrationBuilder.DropTable(
                name: "empresas");
        }
    }
}
