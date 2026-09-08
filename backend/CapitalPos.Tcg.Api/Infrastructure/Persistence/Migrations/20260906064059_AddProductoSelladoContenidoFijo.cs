using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductoSelladoContenidoFijo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "producto_sellado_contenido_fijo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_sellado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_componente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producto_sellado_contenido_fijo", x => x.id);
                    table.ForeignKey(
                        name: "FK_producto_sellado_contenido_fijo_productos_empresa_id_produc~",
                        columns: x => new { x.empresa_id, x.producto_componente_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_producto_sellado_contenido_fijo_productos_empresa_id_produ~1",
                        columns: x => new { x.empresa_id, x.producto_sellado_id },
                        principalTable: "productos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_producto_sellado_contenido_fijo_empresa_id_producto_compone~",
                table: "producto_sellado_contenido_fijo",
                columns: new[] { "empresa_id", "producto_componente_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prod_sellado_contenido_fijo_comp",
                table: "producto_sellado_contenido_fijo",
                columns: new[] { "empresa_id", "producto_sellado_id", "producto_componente_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "producto_sellado_contenido_fijo");
        }
    }
}
