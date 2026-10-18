using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearCierreYObservaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "estilo_observaciones",
                columns: table => new
                {
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    longitud = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    persona = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ejemplos = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estilo_observaciones", x => x.docente_id);
                    table.CheckConstraint("ck_estilo_observaciones_ejemplos", "cardinality(ejemplos) <= 3");
                    table.CheckConstraint("ck_estilo_observaciones_longitud", "longitud IN ('breve', 'normal', 'detallada')");
                    table.CheckConstraint("ck_estilo_observaciones_persona", "persona IN ('segunda', 'tercera')");
                    table.ForeignKey(
                        name: "fk_estilo_observaciones_docente_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "llamada_ia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    version_prompt = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tokens_entrada = table.Column<int>(type: "integer", nullable: false),
                    tokens_salida = table.Column<int>(type: "integer", nullable: false),
                    tokens_cache_leidos = table.Column<int>(type: "integer", nullable: false),
                    duracion_ms = table.Column<int>(type: "integer", nullable: false),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_llamada_ia", x => x.id);
                    table.ForeignKey(
                        name: "fk_llamada_ia_docente_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_llamada_ia_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "observacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    texto = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    borrador_ia = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    texto_final = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    instruccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    regeneraciones = table.Column<int>(type: "integer", nullable: false),
                    modelo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    version_prompt = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    tokens_entrada = table.Column<int>(type: "integer", nullable: false),
                    tokens_salida = table.Column<int>(type: "integer", nullable: false),
                    fallo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    datos_cambiaron = table.Column<bool>(type: "boolean", nullable: false),
                    aprobada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_observacion", x => x.id);
                    table.CheckConstraint("ck_observacion_aprobada", "estado NOT IN ('aprobada', 'publicada') OR texto_final IS NOT NULL");
                    table.CheckConstraint("ck_observacion_estado", "estado IN ('sin_generar', 'generando', 'borrador', 'aprobada', 'publicada')");
                    table.CheckConstraint("ck_observacion_regeneraciones", "regeneraciones BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "fk_observacion_alumno_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_observacion_periodos_periodo_id",
                        column: x => x.periodo_id,
                        principalTable: "periodo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_llamada_ia_docente_id_created_at",
                table: "llamada_ia",
                columns: new[] { "docente_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_llamada_ia_grupo_id_created_at",
                table: "llamada_ia",
                columns: new[] { "grupo_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_observacion_alumno_id_periodo_id",
                table: "observacion",
                columns: new[] { "alumno_id", "periodo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_observacion_docente_id_estado",
                table: "observacion",
                columns: new[] { "docente_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_observacion_periodo_id",
                table: "observacion",
                column: "periodo_id");

            // reglas/03: toda tabla del docente llega con su política RLS en la misma migración.
            migrationBuilder.HabilitarRlsPorDocente("observacion");
            migrationBuilder.HabilitarRlsPorDocente("estilo_observaciones");
            migrationBuilder.HabilitarRlsPorDocente("llamada_ia");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estilo_observaciones");

            migrationBuilder.DropTable(
                name: "llamada_ia");

            migrationBuilder.DropTable(
                name: "observacion");
        }
    }
}
