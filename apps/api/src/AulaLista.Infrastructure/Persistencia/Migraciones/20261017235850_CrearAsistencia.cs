using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearAsistencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "valor_retardo",
                table: "grupo",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.CreateTable(
                name: "toma_asistencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_toma_asistencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_toma_asistencia_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asistencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toma_asistencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asistencia", x => x.id);
                    table.CheckConstraint("ck_asistencia_estado", "estado IN ('presente', 'falta', 'retardo', 'justificada')");
                    table.ForeignKey(
                        name: "fk_asistencia_alumno_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asistencia_tomas_asistencia_toma_asistencia_id",
                        column: x => x.toma_asistencia_id,
                        principalTable: "toma_asistencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_grupo_valor_retardo",
                table: "grupo",
                sql: "valor_retardo IN (0, 0.5, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_asistencia_alumno_id",
                table: "asistencia",
                column: "alumno_id");

            migrationBuilder.CreateIndex(
                name: "ix_asistencia_docente_id",
                table: "asistencia",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_asistencia_toma_asistencia_id_alumno_id",
                table: "asistencia",
                columns: new[] { "toma_asistencia_id", "alumno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toma_asistencia_docente_id",
                table: "toma_asistencia",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_toma_asistencia_grupo_id_fecha",
                table: "toma_asistencia",
                columns: new[] { "grupo_id", "fecha" },
                unique: true);

            migrationBuilder.HabilitarRlsPorDocente("toma_asistencia");
            migrationBuilder.HabilitarRlsPorDocente("asistencia");

            // ADR-0012: el tipo de rubro de asistencia (RN-104). El rol de la app no escribe el catálogo.
            migrationBuilder.Sql("""
                INSERT INTO tipo_rubro (id, nombre, descripcion, icono, comportamiento, forma_sugerida, nombres_sugeridos, orden, activo, created_at, updated_at) VALUES
                ('0199f2a0-0000-7000-8000-000000000008', 'Asistencia', 'Se calcula con las listas que tomes en la unidad.', 'asistencia', 'asistencia', 'numerica', ARRAY['Asistencia'], 8, true, now(), now());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM tipo_rubro WHERE id = '0199f2a0-0000-7000-8000-000000000008';");

            migrationBuilder.DropTable(
                name: "asistencia");

            migrationBuilder.DropTable(
                name: "toma_asistencia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_grupo_valor_retardo",
                table: "grupo");

            migrationBuilder.DropColumn(
                name: "valor_retardo",
                table: "grupo");
        }
    }
}
