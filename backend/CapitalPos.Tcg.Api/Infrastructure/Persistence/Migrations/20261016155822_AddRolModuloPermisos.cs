using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolModuloPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rol_modulo_permisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    modulo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    lectura = table.Column<bool>(type: "boolean", nullable: false),
                    escritura = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rol_modulo_permisos", x => x.id);
                    table.ForeignKey(
                        name: "FK_rol_modulo_permisos_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_rol_modulo_permisos_empresa_rol_modulo",
                table: "rol_modulo_permisos",
                columns: new[] { "empresa_id", "rol", "modulo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rol_modulo_permisos");
        }
    }
}
