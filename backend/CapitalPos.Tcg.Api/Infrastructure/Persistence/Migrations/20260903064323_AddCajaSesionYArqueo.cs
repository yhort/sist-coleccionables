using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCajaSesionYArqueo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "caja_sesion_id",
                table: "ventas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "caja_sesiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sede_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_cierre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    monto_apertura = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_apertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estado = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    monto_efectivo_teorico = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    monto_efectivo_real = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    diferencia = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    observacion_apertura = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    observacion_cierre = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caja_sesiones", x => x.id);
                    table.UniqueConstraint("ak_caja_sesiones_empresa_id", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_caja_sesiones_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_caja_sesiones_sedes_empresa_id_sede_id",
                        columns: x => new { x.empresa_id, x.sede_id },
                        principalTable: "sedes",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_caja_sesiones_usuarios_usuario_cierre_id",
                        column: x => x.usuario_cierre_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_caja_sesiones_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "caja_movimientos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caja_sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caja_movimientos", x => x.id);
                    table.ForeignKey(
                        name: "FK_caja_movimientos_caja_sesiones_empresa_id_caja_sesion_id",
                        columns: x => new { x.empresa_id, x.caja_sesion_id },
                        principalTable: "caja_sesiones",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_caja_movimientos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ventas_empresa_caja_sesion",
                table: "ventas",
                columns: new[] { "empresa_id", "caja_sesion_id" });

            migrationBuilder.CreateIndex(
                name: "ix_caja_movimientos_empresa_sesion_fecha",
                table: "caja_movimientos",
                columns: new[] { "empresa_id", "caja_sesion_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_caja_movimientos_usuario_id",
                table: "caja_movimientos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_caja_sesiones_empresa_sede_fecha",
                table: "caja_sesiones",
                columns: new[] { "empresa_id", "sede_id", "fecha_apertura" });

            migrationBuilder.CreateIndex(
                name: "IX_caja_sesiones_usuario_cierre_id",
                table: "caja_sesiones",
                column: "usuario_cierre_id");

            migrationBuilder.CreateIndex(
                name: "IX_caja_sesiones_usuario_id",
                table: "caja_sesiones",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_caja_sesiones_empresa_sede_abierta",
                table: "caja_sesiones",
                columns: new[] { "empresa_id", "sede_id" },
                unique: true,
                filter: "estado = 'ABIERTA'");

            migrationBuilder.AddForeignKey(
                name: "FK_ventas_caja_sesiones_empresa_id_caja_sesion_id",
                table: "ventas",
                columns: new[] { "empresa_id", "caja_sesion_id" },
                principalTable: "caja_sesiones",
                principalColumns: new[] { "empresa_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ventas_caja_sesiones_empresa_id_caja_sesion_id",
                table: "ventas");

            migrationBuilder.DropTable(
                name: "caja_movimientos");

            migrationBuilder.DropTable(
                name: "caja_sesiones");

            migrationBuilder.DropIndex(
                name: "ix_ventas_empresa_caja_sesion",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "caja_sesion_id",
                table: "ventas");
        }
    }
}
