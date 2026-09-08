using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotasVentaYBoletaConsolidada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes");

            migrationBuilder.AddColumn<bool>(
                name: "es_consolidacion",
                table: "ventas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "tipo",
                table: "series_comprobante",
                type: "character varying(24)",
                unicode: false,
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldUnicode: false,
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "tipo",
                table: "comprobantes",
                type: "character varying(24)",
                unicode: false,
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldUnicode: false,
                oldMaxLength: 16);

            migrationBuilder.AddColumn<Guid>(
                name: "boleta_consolidada_id",
                table: "comprobantes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "boletas_consolidadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comprobante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    filtro = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: false),
                    fecha_operacion = table.Column<DateOnly>(type: "date", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidad_notas = table.Column<int>(type: "integer", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boletas_consolidadas", x => x.id);
                    table.ForeignKey(
                        name: "FK_boletas_consolidadas_comprobantes_comprobante_id",
                        column: x => x.comprobante_id,
                        principalTable: "comprobantes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_boletas_consolidadas_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_boletas_consolidadas_ventas_empresa_id_venta_id",
                        columns: x => new { x.empresa_id, x.venta_id },
                        principalTable: "ventas",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comprobantes_boleta_consolidada_id",
                table: "comprobantes",
                column: "boleta_consolidada_id");

            migrationBuilder.CreateIndex(
                name: "ix_comprobantes_empresa_tipo_estado_fecha",
                table: "comprobantes",
                columns: new[] { "empresa_id", "tipo", "estado", "fecha_emision" });

            migrationBuilder.CreateIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes",
                columns: new[] { "empresa_id", "venta_id" },
                unique: true,
                filter: "tipo IN ('BOLETA', 'FACTURA', 'NOTA_VENTA')");

            migrationBuilder.CreateIndex(
                name: "ix_boletas_consolidadas_empresa_fecha",
                table: "boletas_consolidadas",
                columns: new[] { "empresa_id", "fecha_operacion" });

            migrationBuilder.CreateIndex(
                name: "IX_boletas_consolidadas_empresa_id_venta_id",
                table: "boletas_consolidadas",
                columns: new[] { "empresa_id", "venta_id" });

            migrationBuilder.CreateIndex(
                name: "ux_boletas_consolidadas_comprobante_id",
                table: "boletas_consolidadas",
                column: "comprobante_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_comprobantes_boletas_consolidadas_boleta_consolidada_id",
                table: "comprobantes",
                column: "boleta_consolidada_id",
                principalTable: "boletas_consolidadas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comprobantes_boletas_consolidadas_boleta_consolidada_id",
                table: "comprobantes");

            migrationBuilder.DropTable(
                name: "boletas_consolidadas");

            migrationBuilder.DropIndex(
                name: "IX_comprobantes_boleta_consolidada_id",
                table: "comprobantes");

            migrationBuilder.DropIndex(
                name: "ix_comprobantes_empresa_tipo_estado_fecha",
                table: "comprobantes");

            migrationBuilder.DropIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes");

            migrationBuilder.DropColumn(
                name: "es_consolidacion",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "boleta_consolidada_id",
                table: "comprobantes");

            migrationBuilder.AlterColumn<string>(
                name: "tipo",
                table: "series_comprobante",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldUnicode: false,
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<string>(
                name: "tipo",
                table: "comprobantes",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldUnicode: false,
                oldMaxLength: 24);

            migrationBuilder.CreateIndex(
                name: "ux_comprobantes_empresa_venta",
                table: "comprobantes",
                columns: new[] { "empresa_id", "venta_id" },
                unique: true,
                filter: "tipo IN ('BOLETA', 'FACTURA')");
        }
    }
}
