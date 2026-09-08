using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubastaDetalles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subasta_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subasta_tcg_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subasta_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_subasta_detalles_productos_empresa_id_producto_id",
                        columns: x => new { x.empresa_id, x.producto_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subasta_detalles_subastas_tcg_empresa_id_subasta_tcg_id",
                        columns: x => new { x.empresa_id, x.subasta_tcg_id },
                        principalTable: "subastas_tcg",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subasta_detalles_empresa_id_producto_id",
                table: "subasta_detalles",
                columns: new[] { "empresa_id", "producto_id" });

            migrationBuilder.CreateIndex(
                name: "ix_subasta_detalles_empresa_subasta_orden",
                table: "subasta_detalles",
                columns: new[] { "empresa_id", "subasta_tcg_id", "orden" });

            // Backfill: cada subasta mono-SKU existente pasa a 1 línea cantidad 1.
            migrationBuilder.Sql("""
                INSERT INTO subasta_detalles (id, subasta_tcg_id, empresa_id, producto_id, cantidad, orden)
                SELECT gen_random_uuid(), s.id, s.empresa_id, s.producto_id, 1, 1
                FROM subastas_tcg s
                WHERE NOT EXISTS (
                    SELECT 1 FROM subasta_detalles d
                    WHERE d.empresa_id = s.empresa_id AND d.subasta_tcg_id = s.id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subasta_detalles");
        }
    }
}
