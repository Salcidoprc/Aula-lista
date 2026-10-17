using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearEvaluacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cambio_sincronizado",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cambio_sincronizado", x => x.id);
                    table.ForeignKey(
                        name: "fk_cambio_sincronizado_docentes_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tipo_rubro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icono = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    comportamiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    forma_sugerida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombres_sugeridos = table.Column<string[]>(type: "text[]", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_rubro", x => x.id);
                    table.CheckConstraint("ck_tipo_rubro_comportamiento", "comportamiento IN ('actividades', 'asistencia')");
                    table.CheckConstraint("ck_tipo_rubro_forma_sugerida", "forma_sugerida IN ('numerica', 'cumplimiento')");
                });

            migrationBuilder.CreateTable(
                name: "rubro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tipo_rubro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peso = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    forma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    calif_minima = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rubro", x => x.id);
                    table.CheckConstraint("ck_rubro_calif_minima", "calif_minima BETWEEN 0 AND 10");
                    table.CheckConstraint("ck_rubro_forma", "forma IN ('numerica', 'cumplimiento')");
                    table.CheckConstraint("ck_rubro_peso", "peso BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_rubro_periodo_periodo_id",
                        column: x => x.periodo_id,
                        principalTable: "periodo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rubro_tipos_rubro_tipo_rubro_id",
                        column: x => x.tipo_rubro_id,
                        principalTable: "tipo_rubro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "actividad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_actividad", x => x.id);
                    table.CheckConstraint("ck_actividad_modo", "modo IN ('individual', 'por_equipo')");
                    table.ForeignKey(
                        name: "fk_actividad_rubros_rubro_id",
                        column: x => x.rubro_id,
                        principalTable: "rubro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "criterio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    peso = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    forma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    calif_minima = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_criterio", x => x.id);
                    table.CheckConstraint("ck_criterio_calif_minima", "calif_minima BETWEEN 0 AND 10");
                    table.CheckConstraint("ck_criterio_forma", "forma IN ('numerica', 'cumplimiento')");
                    table.CheckConstraint("ck_criterio_peso", "peso BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_criterio_rubros_rubro_id",
                        column: x => x.rubro_id,
                        principalTable: "rubro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calificacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actividad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    marca = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calificacion", x => x.id);
                    table.CheckConstraint("ck_calificacion_marca", "marca IN ('ninguna', 'cumplio', 'no_cumplio', 'no_entrego', 'exento')");
                    table.CheckConstraint("ck_calificacion_origen", "origen IN ('individual', 'equipo', 'ajustada')");
                    table.CheckConstraint("ck_calificacion_valor", "valor IS NULL OR valor BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "fk_calificacion_actividad_actividad_id",
                        column: x => x.actividad_id,
                        principalTable: "actividad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calificacion_alumno_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calificacion_criterio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    calificacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    marca = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calificacion_criterio", x => x.id);
                    table.CheckConstraint("ck_calificacion_criterio_marca", "marca IN ('ninguna', 'cumplio', 'no_cumplio')");
                    table.CheckConstraint("ck_calificacion_criterio_valor", "valor IS NULL OR valor BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "fk_calificacion_criterio_calificacion_calificacion_id",
                        column: x => x.calificacion_id,
                        principalTable: "calificacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calificacion_criterio_criterios_criterio_id",
                        column: x => x.criterio_id,
                        principalTable: "criterio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_actividad_docente_id",
                table: "actividad",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_actividad_rubro_id",
                table: "actividad",
                column: "rubro_id");

            migrationBuilder.CreateIndex(
                name: "ix_calificacion_actividad_id_alumno_id",
                table: "calificacion",
                columns: new[] { "actividad_id", "alumno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calificacion_alumno_id",
                table: "calificacion",
                column: "alumno_id");

            migrationBuilder.CreateIndex(
                name: "ix_calificacion_docente_id",
                table: "calificacion",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_calificacion_criterio_calificacion_id_criterio_id",
                table: "calificacion_criterio",
                columns: new[] { "calificacion_id", "criterio_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calificacion_criterio_criterio_id",
                table: "calificacion_criterio",
                column: "criterio_id");

            migrationBuilder.CreateIndex(
                name: "ix_calificacion_criterio_docente_id",
                table: "calificacion_criterio",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_cambio_sincronizado_docente_id",
                table: "cambio_sincronizado",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_criterio_docente_id",
                table: "criterio",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_criterio_rubro_id",
                table: "criterio",
                column: "rubro_id");

            migrationBuilder.CreateIndex(
                name: "ix_rubro_docente_id",
                table: "rubro",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_rubro_periodo_id",
                table: "rubro",
                column: "periodo_id");

            migrationBuilder.CreateIndex(
                name: "ix_rubro_tipo_rubro_id",
                table: "rubro",
                column: "tipo_rubro_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_rubro_nombre",
                table: "tipo_rubro",
                column: "nombre",
                unique: true);

            migrationBuilder.HabilitarRlsPorDocente("rubro");
            migrationBuilder.HabilitarRlsPorDocente("criterio");
            migrationBuilder.HabilitarRlsPorDocente("actividad");
            migrationBuilder.HabilitarRlsPorDocente("calificacion");
            migrationBuilder.HabilitarRlsPorDocente("calificacion_criterio");
            migrationBuilder.HabilitarRlsPorDocente("cambio_sincronizado");

            // ADR-0012: catálogo global de tipos de rubro. La app solo lo lee; lo edita el panel de administración
            // (H6). Asistencia llega con SPEC-10 (H3.3). Ids fijos para que todos los ambientes coincidan.
            migrationBuilder.Sql("""
                INSERT INTO tipo_rubro (id, nombre, descripcion, icono, comportamiento, forma_sugerida, nombres_sugeridos, orden, activo, created_at, updated_at) VALUES
                ('0199f2a0-0000-7000-8000-000000000001', 'Tareas', 'Trabajos para la casa.', 'tarea', 'actividades', 'cumplimiento', ARRAY['Tareas'], 1, true, now(), now()),
                ('0199f2a0-0000-7000-8000-000000000002', 'Actividades', 'Trabajo en clase.', 'actividad', 'actividades', 'numerica', ARRAY['Actividades', 'Trabajos en clase'], 2, true, now(), now()),
                ('0199f2a0-0000-7000-8000-000000000003', 'Prácticas', 'Prácticas y laboratorios.', 'practica', 'actividades', 'numerica', ARRAY['Prácticas', 'Laboratorio'], 3, true, now(), now()),
                ('0199f2a0-0000-7000-8000-000000000004', 'Participaciones', 'Participación en clase.', 'participacion', 'actividades', 'cumplimiento', ARRAY['Participaciones'], 4, true, now(), now()),
                ('0199f2a0-0000-7000-8000-000000000005', 'Exámenes', 'Exámenes y evaluaciones escritas.', 'examen', 'actividades', 'numerica', ARRAY['Examen', 'Exámenes'], 5, true, now(), now()),
                ('0199f2a0-0000-7000-8000-000000000006', 'Proyecto', 'Proyectos de la unidad.', 'proyecto', 'actividades', 'numerica', ARRAY['Proyecto'], 6, true, now(), now()),
                ('0199f2a0-0000-7000-8000-000000000007', 'Exposición', 'Exposiciones, con o sin criterios.', 'exposicion', 'actividades', 'numerica', ARRAY['Exposición'], 7, true, now(), now());
                REVOKE INSERT, UPDATE, DELETE ON tipo_rubro FROM app_runtime;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calificacion_criterio");

            migrationBuilder.DropTable(
                name: "cambio_sincronizado");

            migrationBuilder.DropTable(
                name: "calificacion");

            migrationBuilder.DropTable(
                name: "criterio");

            migrationBuilder.DropTable(
                name: "actividad");

            migrationBuilder.DropTable(
                name: "rubro");

            migrationBuilder.DropTable(
                name: "tipo_rubro");
        }
    }
}
