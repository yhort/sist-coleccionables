using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefineWooCommerceSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_woocommerce_mapeos_empresa_woo_product",
                table: "woocommerce_mapeos_producto");

            migrationBuilder.AddColumn<int>(
                name: "intentos",
                table: "woocommerce_sync_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "payload_json",
                table: "woocommerce_sync_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "proximo_reintento",
                table: "woocommerce_sync_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_woocommerce_sync_logs_reintento",
                table: "woocommerce_sync_logs",
                columns: new[] { "estado", "proximo_reintento" },
                filter: "estado = 'REINTENTO'");

            migrationBuilder.CreateIndex(
                name: "ux_woocommerce_mapeos_empresa_woo_simple",
                table: "woocommerce_mapeos_producto",
                columns: new[] { "empresa_id", "woo_product_id" },
                unique: true,
                filter: "woo_product_id IS NOT NULL AND woo_variation_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_woocommerce_mapeos_empresa_woo_variation",
                table: "woocommerce_mapeos_producto",
                columns: new[] { "empresa_id", "woo_product_id", "woo_variation_id" },
                unique: true,
                filter: "woo_product_id IS NOT NULL AND woo_variation_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_woocommerce_sync_logs_reintento",
                table: "woocommerce_sync_logs");

            migrationBuilder.DropIndex(
                name: "ux_woocommerce_mapeos_empresa_woo_simple",
                table: "woocommerce_mapeos_producto");

            migrationBuilder.DropIndex(
                name: "ux_woocommerce_mapeos_empresa_woo_variation",
                table: "woocommerce_mapeos_producto");

            migrationBuilder.DropColumn(
                name: "intentos",
                table: "woocommerce_sync_logs");

            migrationBuilder.DropColumn(
                name: "payload_json",
                table: "woocommerce_sync_logs");

            migrationBuilder.DropColumn(
                name: "proximo_reintento",
                table: "woocommerce_sync_logs");

            migrationBuilder.CreateIndex(
                name: "ux_woocommerce_mapeos_empresa_woo_product",
                table: "woocommerce_mapeos_producto",
                columns: new[] { "empresa_id", "woo_product_id" },
                unique: true,
                filter: "woo_product_id IS NOT NULL");
        }
    }
}
