using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFechaCierreRealSubastaTcg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_cierre_real",
                table: "subastas_tcg",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_subastas_tcg_estado_fecha_cierre",
                table: "subastas_tcg",
                columns: new[] { "estado", "fecha_cierre" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_subastas_tcg_estado_fecha_cierre",
                table: "subastas_tcg");

            migrationBuilder.DropColumn(
                name: "fecha_cierre_real",
                table: "subastas_tcg");
        }
    }
}
