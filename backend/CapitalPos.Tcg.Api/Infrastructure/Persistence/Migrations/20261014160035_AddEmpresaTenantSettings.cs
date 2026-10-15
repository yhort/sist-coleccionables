using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaTenantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "permitir_cromos_sin_codigo",
                table: "empresas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "permitir_pos_mostrador",
                table: "empresas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "permitir_singles_tcg",
                table: "empresas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "permitir_subastas_lote_f_b",
                table: "empresas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "permitir_woo_commerce_sync",
                table: "empresas",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "permitir_cromos_sin_codigo",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "permitir_pos_mostrador",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "permitir_singles_tcg",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "permitir_subastas_lote_f_b",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "permitir_woo_commerce_sync",
                table: "empresas");
        }
    }
}
