using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidoDigitalNotificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_notificacion",
                table: "pedidos_digitales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "notificado",
                table: "pedidos_digitales",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fecha_notificacion",
                table: "pedidos_digitales");

            migrationBuilder.DropColumn(
                name: "notificado",
                table: "pedidos_digitales");
        }
    }
}
