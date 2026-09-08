using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientePuntoEntregaYCanalContacto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "canal_contacto",
                table: "pedidos_digitales",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "punto_entrega",
                table: "pedidos_digitales",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "canal_contacto",
                table: "entregas",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "punto_entrega",
                table: "entregas",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "canal_contacto",
                table: "clientes",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "punto_entrega_preferido",
                table: "clientes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "canal_contacto",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "punto_entrega",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "canal_contacto",
                table: "entregas");

            migrationBuilder.DropColumn(
                name: "punto_entrega",
                table: "entregas");

            migrationBuilder.DropColumn(
                name: "canal_contacto",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "punto_entrega_preferido",
                table: "clientes");
        }
    }
}
