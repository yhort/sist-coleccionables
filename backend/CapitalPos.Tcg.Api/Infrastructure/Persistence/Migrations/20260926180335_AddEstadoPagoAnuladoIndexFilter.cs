using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoPagoAnuladoIndexFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pagos_empresa_codigo_operacion",
                table: "pagos");

            migrationBuilder.CreateIndex(
                name: "ux_pagos_empresa_codigo_operacion",
                table: "pagos",
                columns: new[] { "empresa_id", "codigo_operacion" },
                unique: true,
                filter: "codigo_operacion IS NOT NULL AND estado <> 'RECHAZADO' AND estado <> 'ANULADO'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pagos_empresa_codigo_operacion",
                table: "pagos");

            migrationBuilder.CreateIndex(
                name: "ux_pagos_empresa_codigo_operacion",
                table: "pagos",
                columns: new[] { "empresa_id", "codigo_operacion" },
                unique: true,
                filter: "codigo_operacion IS NOT NULL AND estado <> 'RECHAZADO'");
        }
    }
}
