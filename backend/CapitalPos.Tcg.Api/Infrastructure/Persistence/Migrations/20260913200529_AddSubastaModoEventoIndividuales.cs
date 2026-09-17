using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubastaModoEventoIndividuales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "modo",
                table: "subastas_tcg",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                defaultValue: "COMBO");

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "subasta_detalles",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                defaultValue: "PENDIENTE");

            migrationBuilder.AddColumn<Guid>(
                name: "pedido_digital_id",
                table: "subasta_detalles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "puja_ganadora_id",
                table: "subasta_detalles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "subasta_detalle_id",
                table: "pujas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pujas_empresa_subasta_detalle_fecha",
                table: "pujas",
                columns: new[] { "empresa_id", "subasta_tcg_id", "subasta_detalle_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_pujas_subasta_detalle_id",
                table: "pujas",
                column: "subasta_detalle_id");

            migrationBuilder.AddForeignKey(
                name: "FK_pujas_subasta_detalles_subasta_detalle_id",
                table: "pujas",
                column: "subasta_detalle_id",
                principalTable: "subasta_detalles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pujas_subasta_detalles_subasta_detalle_id",
                table: "pujas");

            migrationBuilder.DropIndex(
                name: "ix_pujas_empresa_subasta_detalle_fecha",
                table: "pujas");

            migrationBuilder.DropIndex(
                name: "IX_pujas_subasta_detalle_id",
                table: "pujas");

            migrationBuilder.DropColumn(
                name: "modo",
                table: "subastas_tcg");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "subasta_detalles");

            migrationBuilder.DropColumn(
                name: "pedido_digital_id",
                table: "subasta_detalles");

            migrationBuilder.DropColumn(
                name: "puja_ganadora_id",
                table: "subasta_detalles");

            migrationBuilder.DropColumn(
                name: "subasta_detalle_id",
                table: "pujas");
        }
    }
}
