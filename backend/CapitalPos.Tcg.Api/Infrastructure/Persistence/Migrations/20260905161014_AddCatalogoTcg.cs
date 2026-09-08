using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogoTcg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "rareza",
                table: "producto_cartas",
                type: "character varying(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldUnicode: false,
                oldMaxLength: 16);

            migrationBuilder.AddColumn<Guid>(
                name: "carta_catalogo_id",
                table: "producto_cartas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tcg_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    juego = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tcg_series", x => x.id);
                    table.UniqueConstraint("ak_tcg_series_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_tcg_series_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tcg_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre_en = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    codigo_impresion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    total_cartas = table.Column<int>(type: "integer", nullable: false),
                    fecha_lanzamiento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tcg_sets", x => x.id);
                    table.UniqueConstraint("ak_tcg_sets_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_tcg_sets_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tcg_sets_tcg_series_empresa_id_serie_id",
                        columns: x => new { x.empresa_id, x.serie_id },
                        principalTable: "tcg_series",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tcg_cartas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_carta = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    rareza = table.Column<string>(type: "character varying(32)", unicode: false, maxLength: 32, nullable: false),
                    artista = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    imagen_oficial_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tcg_cartas", x => x.id);
                    table.UniqueConstraint("ak_tcg_cartas_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_tcg_cartas_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tcg_cartas_tcg_sets_empresa_id_set_id",
                        columns: x => new { x.empresa_id, x.set_id },
                        principalTable: "tcg_sets",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_producto_cartas_carta_catalogo_id",
                table: "producto_cartas",
                column: "carta_catalogo_id");

            migrationBuilder.CreateIndex(
                name: "ux_tcg_cartas_empresa_set_numero_rareza",
                table: "tcg_cartas",
                columns: new[] { "empresa_id", "set_id", "numero", "rareza" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tcg_series_empresa_juego_codigo",
                table: "tcg_series",
                columns: new[] { "empresa_id", "juego", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tcg_sets_empresa_serie",
                table: "tcg_sets",
                columns: new[] { "empresa_id", "serie_id" });

            migrationBuilder.CreateIndex(
                name: "ux_tcg_sets_empresa_codigo",
                table: "tcg_sets",
                columns: new[] { "empresa_id", "codigo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_producto_cartas_tcg_cartas_carta_catalogo_id",
                table: "producto_cartas",
                column: "carta_catalogo_id",
                principalTable: "tcg_cartas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_producto_cartas_tcg_cartas_carta_catalogo_id",
                table: "producto_cartas");

            migrationBuilder.DropTable(
                name: "tcg_cartas");

            migrationBuilder.DropTable(
                name: "tcg_sets");

            migrationBuilder.DropTable(
                name: "tcg_series");

            migrationBuilder.DropIndex(
                name: "ix_producto_cartas_carta_catalogo_id",
                table: "producto_cartas");

            migrationBuilder.DropColumn(
                name: "carta_catalogo_id",
                table: "producto_cartas");

            migrationBuilder.AlterColumn<string>(
                name: "rareza",
                table: "producto_cartas",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldUnicode: false,
                oldMaxLength: 32);
        }
    }
}
