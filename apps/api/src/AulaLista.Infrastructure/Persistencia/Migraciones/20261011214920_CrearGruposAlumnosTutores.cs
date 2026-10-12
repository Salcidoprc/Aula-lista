using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearGruposAlumnosTutores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-0014: el ciclo deja de ser catálogo global y pasa a ser del docente. No tenía datos fuera de local.
            migrationBuilder.Sql("DELETE FROM ciclo_escolar;");

            migrationBuilder.DropIndex(
                name: "ix_ciclo_escolar_nombre",
                table: "ciclo_escolar");

            migrationBuilder.AddColumn<Guid>(
                name: "docente_id",
                table: "ciclo_escolar",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "tipo_unidad",
                table: "ciclo_escolar",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "grupo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo_escolar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    grado = table.Column<int>(type: "integer", nullable: false),
                    materia = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    plan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    calif_minima = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    calif_aprobatoria = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    decimales_final = table.Column<int>(type: "integer", nullable: false),
                    redondeo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    archivado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupo", x => x.id);
                    table.CheckConstraint("ck_grupo_calificaciones", "calif_minima BETWEEN 0 AND 10 AND calif_aprobatoria BETWEEN calif_minima AND 10");
                    table.CheckConstraint("ck_grupo_decimales_final", "decimales_final BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_grupo_grado", "grado BETWEEN 1 AND 6");
                    table.CheckConstraint("ck_grupo_nivel", "nivel IN ('primaria', 'secundaria')");
                    table.CheckConstraint("ck_grupo_plan", "plan IN ('gratis', 'pro')");
                    table.CheckConstraint("ck_grupo_redondeo", "redondeo IN ('arriba', 'abajo', 'cercano')");
                    table.ForeignKey(
                        name: "fk_grupo_ciclo_escolar_ciclo_escolar_id",
                        column: x => x.ciclo_escolar_id,
                        principalTable: "ciclo_escolar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grupo_docente_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tutor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefono_e164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo_escolar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fin = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidad", x => x.id);
                    table.CheckConstraint("ck_unidad_fechas", "fin >= inicio");
                    table.ForeignKey(
                        name: "fk_unidad_ciclo_escolar_ciclo_escolar_id",
                        column: x => x.ciclo_escolar_id,
                        principalTable: "ciclo_escolar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alumno",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombres = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    apellidos = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    numero_lista = table.Column<int>(type: "integer", nullable: false),
                    trato = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    seudonimo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    baja_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alumno", x => x.id);
                    table.CheckConstraint("ck_alumno_trato", "trato IN ('neutro', 'el', 'ella')");
                    table.ForeignKey(
                        name: "fk_alumno_grupos_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "importacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    confirmada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importacion", x => x.id);
                    table.CheckConstraint("ck_importacion_origen", "origen IN ('excel', 'csv', 'texto')");
                    table.ForeignKey(
                        name: "fk_importacion_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "periodo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_periodo", x => x.id);
                    table.CheckConstraint("ck_periodo_estado", "estado IN ('abierto', 'en_cierre', 'cerrado')");
                    table.ForeignKey(
                        name: "fk_periodo_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_periodo_unidades_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vinculo_tutor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefono_e164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    parentesco = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculo_tutor", x => x.id);
                    table.ForeignKey(
                        name: "fk_vinculo_tutor_alumno_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vinculo_tutor_tutor_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ciclo_escolar_docente_id_nombre",
                table: "ciclo_escolar",
                columns: new[] { "docente_id", "nombre" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ciclo_escolar_tipo_unidad",
                table: "ciclo_escolar",
                sql: "tipo_unidad IN ('bimestre', 'trimestre', 'cuatrimestre', 'semestre', 'mensual', 'personalizada')");

            migrationBuilder.CreateIndex(
                name: "ix_alumno_docente_id",
                table: "alumno",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_alumno_grupo_id",
                table: "alumno",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "ix_alumno_grupo_id_seudonimo",
                table: "alumno",
                columns: new[] { "grupo_id", "seudonimo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grupo_ciclo_escolar_id",
                table: "grupo",
                column: "ciclo_escolar_id");

            migrationBuilder.CreateIndex(
                name: "ix_grupo_docente_id",
                table: "grupo",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_importacion_docente_id",
                table: "importacion",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_importacion_grupo_id",
                table: "importacion",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "ix_periodo_docente_id",
                table: "periodo",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_periodo_grupo_id_unidad_id",
                table: "periodo",
                columns: new[] { "grupo_id", "unidad_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_periodo_unidad_id",
                table: "periodo",
                column: "unidad_id");

            migrationBuilder.CreateIndex(
                name: "ix_tutor_telefono_e164",
                table: "tutor",
                column: "telefono_e164",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unidad_ciclo_escolar_id",
                table: "unidad",
                column: "ciclo_escolar_id");

            migrationBuilder.CreateIndex(
                name: "ix_unidad_docente_id",
                table: "unidad",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_tutor_alumno_id_tutor_id",
                table: "vinculo_tutor",
                columns: new[] { "alumno_id", "tutor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_tutor_docente_id",
                table: "vinculo_tutor",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_tutor_tutor_id",
                table: "vinculo_tutor",
                column: "tutor_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ciclo_escolar_docentes_docente_id",
                table: "ciclo_escolar",
                column: "docente_id",
                principalTable: "docente",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // RN-023: unidades sin traslape dentro del ciclo. RN-013: número de lista único entre activos.
            // Diferibles para permitir reacomodos e intercambios dentro de una misma transacción.
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS btree_gist;
                ALTER TABLE unidad ADD CONSTRAINT ex_unidad_sin_traslape
                    EXCLUDE USING gist (ciclo_escolar_id WITH =, daterange(inicio, fin, '[]') WITH &&)
                    DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE alumno ADD CONSTRAINT ex_alumno_numero_lista
                    EXCLUDE USING btree (grupo_id WITH =, numero_lista WITH =) WHERE (baja_en IS NULL)
                    DEFERRABLE INITIALLY DEFERRED;
                CREATE UNIQUE INDEX ux_grupo_ciclo_grado_nombre_materia
                    ON grupo (ciclo_escolar_id, grado, lower(nombre), lower(coalesce(materia, '')));
                """);

            // RNF-001: todo lo del docente con RLS forzada.
            migrationBuilder.HabilitarRlsPorDocente("ciclo_escolar");
            migrationBuilder.HabilitarRlsPorDocente("unidad");
            migrationBuilder.HabilitarRlsPorDocente("grupo");
            migrationBuilder.HabilitarRlsPorDocente("periodo");
            migrationBuilder.HabilitarRlsPorDocente("alumno");
            migrationBuilder.HabilitarRlsPorDocente("vinculo_tutor");
            migrationBuilder.HabilitarRlsPorDocente("importacion");

            // ADR-0014: el tutor es compartido. app_runtime solo lo lee si es el propio tutor o si el docente
            // tiene un vínculo con él, y nunca lo escribe directo: usa vincular_tutor. Sin FORCE para que
            // las funciones del dueño del esquema lo vean completo.
            migrationBuilder.Sql("""
                ALTER TABLE tutor ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tutor_visible ON tutor FOR SELECT
                    USING (id = NULLIF(current_setting('app.tutor_id', true), '')::uuid
                        OR EXISTS (SELECT 1 FROM vinculo_tutor v WHERE v.tutor_id = tutor.id));
                REVOKE INSERT, UPDATE, DELETE ON tutor FROM app_runtime;

                CREATE FUNCTION vincular_tutor(telefono text, nombre text, correo text)
                RETURNS TABLE (tutor_id uuid, ya_existia boolean)
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                DECLARE
                    encontrado uuid;
                BEGIN
                    IF NULLIF(current_setting('app.docente_id', true), '') IS NULL THEN
                        RAISE EXCEPTION 'vincular_tutor requiere un docente';
                    END IF;
                    INSERT INTO tutor AS t (id, telefono_e164, nombre, correo, created_at, updated_at)
                    VALUES (uuidv7(), vincular_tutor.telefono, vincular_tutor.nombre, vincular_tutor.correo, now(), now())
                    ON CONFLICT (telefono_e164) DO NOTHING
                    RETURNING t.id INTO encontrado;
                    IF encontrado IS NOT NULL THEN
                        RETURN QUERY SELECT encontrado, false;
                        RETURN;
                    END IF;
                    UPDATE tutor AS t SET correo = vincular_tutor.correo, updated_at = now()
                    WHERE t.telefono_e164 = vincular_tutor.telefono AND t.correo IS NULL AND vincular_tutor.correo IS NOT NULL;
                    RETURN QUERY SELECT t.id, true FROM tutor AS t WHERE t.telefono_e164 = vincular_tutor.telefono;
                END;
                $$;
                REVOKE ALL ON FUNCTION vincular_tutor(text, text, text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION vincular_tutor(text, text, text) TO app_runtime;
                """);

            // RN-006 y RNF-009: el borrado de la cuenta incluye ciclos, grupos y alumnos (en cascada) y los
            // tutores que se quedan sin vínculos. Un tutor que sigue vinculado con otro docente falla por su
            // llave foránea (que no pasa por RLS) y se conserva.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION eliminar_cuentas_vencidas(limite timestamptz) RETURNS integer
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                DECLARE
                    r record;
                    tutores uuid[];
                    t uuid;
                    total integer := 0;
                BEGIN
                    FOR r IN SELECT id FROM cuenta
                             WHERE estado = 'eliminacion_solicitada' AND eliminacion_solicitada_en <= limite
                    LOOP
                        PERFORM set_config('app.docente_id', r.id::text, true);
                        tutores := ARRAY(SELECT DISTINCT v.tutor_id FROM vinculo_tutor v WHERE v.docente_id = r.id);
                        DELETE FROM grupo WHERE docente_id = r.id;
                        DELETE FROM ciclo_escolar WHERE docente_id = r.id;
                        FOREACH t IN ARRAY tutores LOOP
                            BEGIN
                                DELETE FROM tutor WHERE id = t;
                            EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN
                                NULL;
                            END;
                        END LOOP;
                        DELETE FROM exportacion WHERE docente_id = r.id;
                        DELETE FROM aceptacion_legal WHERE docente_id = r.id;
                        DELETE FROM docente WHERE id = r.id;
                        DELETE FROM cuenta WHERE id = r.id;
                        UPDATE auditoria SET ip = NULL WHERE actor_id = r.id;
                        total := total + 1;
                    END LOOP;
                    PERFORM set_config('app.docente_id', '', true);
                    RETURN total;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS vincular_tutor(text, text, text);");
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION eliminar_cuentas_vencidas(limite timestamptz) RETURNS integer
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                DECLARE
                    r record;
                    total integer := 0;
                BEGIN
                    FOR r IN SELECT id FROM cuenta
                             WHERE estado = 'eliminacion_solicitada' AND eliminacion_solicitada_en <= limite
                    LOOP
                        PERFORM set_config('app.docente_id', r.id::text, true);
                        DELETE FROM exportacion WHERE docente_id = r.id;
                        DELETE FROM aceptacion_legal WHERE docente_id = r.id;
                        DELETE FROM docente WHERE id = r.id;
                        DELETE FROM cuenta WHERE id = r.id;
                        UPDATE auditoria SET ip = NULL WHERE actor_id = r.id;
                        total := total + 1;
                    END LOOP;
                    PERFORM set_config('app.docente_id', '', true);
                    RETURN total;
                END;
                $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_ciclo_escolar_docentes_docente_id",
                table: "ciclo_escolar");

            migrationBuilder.DropTable(
                name: "importacion");

            migrationBuilder.DropTable(
                name: "periodo");

            migrationBuilder.DropTable(
                name: "vinculo_tutor");

            migrationBuilder.DropTable(
                name: "unidad");

            migrationBuilder.DropTable(
                name: "alumno");

            migrationBuilder.DropTable(
                name: "tutor");

            migrationBuilder.DropTable(
                name: "grupo");

            migrationBuilder.DropIndex(
                name: "ix_ciclo_escolar_docente_id_nombre",
                table: "ciclo_escolar");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ciclo_escolar_tipo_unidad",
                table: "ciclo_escolar");

            migrationBuilder.DropColumn(
                name: "docente_id",
                table: "ciclo_escolar");

            migrationBuilder.DropColumn(
                name: "tipo_unidad",
                table: "ciclo_escolar");

            migrationBuilder.CreateIndex(
                name: "ix_ciclo_escolar_nombre",
                table: "ciclo_escolar",
                column: "nombre",
                unique: true);
        }
    }
}
