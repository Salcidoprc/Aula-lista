using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarImportacionPorFoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_importacion_origen",
                table: "importacion");

            migrationBuilder.AddCheckConstraint(
                name: "ck_importacion_origen",
                table: "importacion",
                sql: "origen IN ('excel', 'csv', 'texto', 'foto')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_importacion_origen",
                table: "importacion");

            migrationBuilder.AddCheckConstraint(
                name: "ck_importacion_origen",
                table: "importacion",
                sql: "origen IN ('excel', 'csv', 'texto')");
        }
    }
}
