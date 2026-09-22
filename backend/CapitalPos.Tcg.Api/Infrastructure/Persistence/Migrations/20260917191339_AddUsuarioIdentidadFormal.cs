using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioIdentidadFormal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "apellidos",
                table: "usuarios",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "dni",
                table: "usuarios",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "nombres",
                table: "usuarios",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            // Backfill identidad formal desde el nombre de display previo.
            migrationBuilder.Sql(
                """
                UPDATE usuarios AS u
                SET
                    nombres = CASE
                        WHEN TRIM(u.nombres) <> '' THEN u.nombres
                        WHEN POSITION(' ' IN TRIM(u.nombre)) > 0
                            THEN LEFT(TRIM(SPLIT_PART(TRIM(u.nombre), ' ', 1)), 80)
                        ELSE LEFT(TRIM(u.nombre), 80)
                    END,
                    apellidos = CASE
                        WHEN TRIM(u.apellidos) <> '' THEN u.apellidos
                        WHEN POSITION(' ' IN TRIM(u.nombre)) > 0
                            THEN LEFT(TRIM(SUBSTRING(TRIM(u.nombre) FROM POSITION(' ' IN TRIM(u.nombre)) + 1)), 80)
                        ELSE 'Usuario'
                    END,
                    dni = CASE
                        WHEN TRIM(u.dni) <> '' THEN u.dni
                        ELSE LPAD(sub.rn::text, 8, '0')
                    END
                FROM (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY empresa_id ORDER BY fecha_creacion, id) AS rn
                    FROM usuarios
                ) AS sub
                WHERE u.id = sub.id;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_empresa_id_dni",
                table: "usuarios",
                columns: new[] { "empresa_id", "dni" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_usuarios_empresa_id_dni",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "apellidos",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "dni",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "nombres",
                table: "usuarios");
        }
    }
}
