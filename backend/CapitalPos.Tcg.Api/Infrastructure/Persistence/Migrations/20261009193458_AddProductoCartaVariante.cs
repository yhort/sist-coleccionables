using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductoCartaVariante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "variante",
                table: "producto_cartas",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Básica");

            // Backfill: foil histórico → Holo; resto se queda en Básica (default).
            migrationBuilder.Sql(
                """
                UPDATE producto_cartas
                SET variante = 'Holo'
                WHERE es_foil = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "variante",
                table: "producto_cartas");
        }
    }
}
