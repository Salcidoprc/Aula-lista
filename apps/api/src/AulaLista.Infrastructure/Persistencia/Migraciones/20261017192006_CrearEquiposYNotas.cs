using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearEquiposYNotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "actividad_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actividad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_equipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_actividad_equipo", x => x.id);
                    table.ForeignKey(
                        name: "fk_actividad_equipo_actividad_actividad_id",
                        column: x => x.actividad_id,
                        principalTable: "actividad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_actividad_equipo_alumnos_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipo", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipo_grupos_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nota",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    etiqueta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nota", x => x.id);
                    table.CheckConstraint("ck_nota_etiqueta", "etiqueta IN ('ninguna', 'fortaleza', 'mejora', 'conducta')");
                    table.ForeignKey(
                        name: "fk_nota_alumno_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_nota_periodos_periodo_id",
                        column: x => x.periodo_id,
                        principalTable: "periodo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipo_alumno",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipo_alumno", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipo_alumno_alumno_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_equipo_alumno_equipos_equipo_id",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_actividad_equipo_actividad_id_alumno_id",
                table: "actividad_equipo",
                columns: new[] { "actividad_id", "alumno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_actividad_equipo_alumno_id",
                table: "actividad_equipo",
                column: "alumno_id");

            migrationBuilder.CreateIndex(
                name: "ix_actividad_equipo_docente_id",
                table: "actividad_equipo",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_docente_id",
                table: "equipo",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_grupo_id",
                table: "equipo",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_alumno_alumno_id",
                table: "equipo_alumno",
                column: "alumno_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipo_alumno_docente_id",
                table: "equipo_alumno",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_alumno_equipo_id",
                table: "equipo_alumno",
                column: "equipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_nota_alumno_id_fecha",
                table: "nota",
                columns: new[] { "alumno_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_nota_docente_id",
                table: "nota",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_nota_periodo_id",
                table: "nota",
                column: "periodo_id");

            migrationBuilder.HabilitarRlsPorDocente("equipo");
            migrationBuilder.HabilitarRlsPorDocente("equipo_alumno");
            migrationBuilder.HabilitarRlsPorDocente("actividad_equipo");
            migrationBuilder.HabilitarRlsPorDocente("nota");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actividad_equipo");

            migrationBuilder.DropTable(
                name: "equipo_alumno");

            migrationBuilder.DropTable(
                name: "nota");

            migrationBuilder.DropTable(
                name: "equipo");
        }
    }
}
