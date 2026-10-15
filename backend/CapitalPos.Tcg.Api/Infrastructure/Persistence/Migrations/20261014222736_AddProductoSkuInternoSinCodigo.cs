using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductoSkuInternoSinCodigo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_coleccionable_sin_codigo",
                table: "productos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "sku_interno",
                table: "productos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ubicacion_tienda",
                table: "productos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_productos_empresa_id_sku_interno",
                table: "productos",
                columns: new[] { "empresa_id", "sku_interno" },
                unique: true,
                filter: "sku_interno IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_productos_empresa_id_sku_interno",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "es_coleccionable_sin_codigo",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "sku_interno",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "ubicacion_tienda",
                table: "productos");
        }
    }
}
