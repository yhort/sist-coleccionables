using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteActivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "activo",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_clientes_empresa_id_activo",
                table: "clientes",
                columns: new[] { "empresa_id", "activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clientes_empresa_id_activo",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "activo",
                table: "clientes");
        }
    }
}
