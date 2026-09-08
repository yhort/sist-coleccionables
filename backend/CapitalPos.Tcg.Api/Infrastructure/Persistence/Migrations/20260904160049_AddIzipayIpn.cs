using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIzipayIpn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integraciones_izipay",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    hmac_sha256_clave = table.Column<string>(type: "text", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integraciones_izipay", x => x.id);
                    table.ForeignKey(
                        name: "FK_integraciones_izipay_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pasarela_webhook_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor = table.Column<string>(type: "character varying(16)", unicode: false, maxLength: 16, nullable: false),
                    transaccion_uuid = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    firma_valida = table.Column<bool>(type: "boolean", nullable: false),
                    payload_raw = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "character varying(24)", unicode: false, maxLength: 24, nullable: false),
                    pago_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pasarela_webhook_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_pasarela_webhook_logs_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pasarela_webhook_logs_pagos_pago_id",
                        column: x => x.pago_id,
                        principalTable: "pagos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ux_integraciones_izipay_empresa_id",
                table: "integraciones_izipay",
                column: "empresa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pasarela_webhook_logs_empresa_created_at",
                table: "pasarela_webhook_logs",
                columns: new[] { "empresa_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_pasarela_webhook_logs_pago_id",
                table: "pasarela_webhook_logs",
                column: "pago_id");

            migrationBuilder.CreateIndex(
                name: "ux_pasarela_webhook_logs_empresa_transaccion",
                table: "pasarela_webhook_logs",
                columns: new[] { "empresa_id", "transaccion_uuid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integraciones_izipay");

            migrationBuilder.DropTable(
                name: "pasarela_webhook_logs");
        }
    }
}
