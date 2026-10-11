using System;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearCuentasDocente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cuenta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    hash_contrasena = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    correo_verificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    google_sub = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    eliminacion_solicitada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuenta", x => x.id);
                    table.CheckConstraint("ck_cuenta_estado", "estado IN ('activa', 'eliminacion_solicitada')");
                });

            migrationBuilder.CreateTable(
                name: "docente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    escuela_nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    escuela_cct = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    entidad_federativa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    zona_horaria = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prueba_pro_usada = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_docente", x => x.id);
                    table.CheckConstraint("ck_docente_nivel", "nivel IS NULL OR nivel IN ('primaria', 'secundaria')");
                    table.ForeignKey(
                        name: "fk_docente_cuenta_id",
                        column: x => x.id,
                        principalTable: "cuenta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    metodo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    autenticada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sesion", x => x.id);
                    table.CheckConstraint("ck_sesion_metodo", "metodo IN ('contrasena', 'google')");
                    table.ForeignKey(
                        name: "fk_sesion_cuenta_cuenta_id",
                        column: x => x.cuenta_id,
                        principalTable: "cuenta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "token_cuenta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposito = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_cuenta", x => x.id);
                    table.CheckConstraint("ck_token_cuenta_proposito", "proposito IN ('verificar_correo', 'restablecer_contrasena')");
                    table.ForeignKey(
                        name: "fk_token_cuenta_cuenta_cuenta_id",
                        column: x => x.cuenta_id,
                        principalTable: "cuenta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aceptacion_legal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aceptacion_legal", x => x.id);
                    table.CheckConstraint("ck_aceptacion_legal_documento", "documento IN ('terminos', 'privacidad', 'declaracion_datos')");
                    table.ForeignKey(
                        name: "fk_aceptacion_legal_docentes_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exportacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ruta_excel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ruta_pdf = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exportacion", x => x.id);
                    table.CheckConstraint("ck_exportacion_estado", "estado IN ('pendiente', 'lista', 'fallida')");
                    table.ForeignKey(
                        name: "fk_exportacion_docente_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aceptacion_legal_docente_id_documento_version",
                table: "aceptacion_legal",
                columns: new[] { "docente_id", "documento", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_actor_id_fecha",
                table: "auditoria",
                columns: new[] { "actor_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_cuenta_correo",
                table: "cuenta",
                column: "correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cuenta_google_sub",
                table: "cuenta",
                column: "google_sub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exportacion_docente_id",
                table: "exportacion",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_sesion_cuenta_id",
                table: "sesion",
                column: "cuenta_id");

            migrationBuilder.CreateIndex(
                name: "ix_sesion_familia_id",
                table: "sesion",
                column: "familia_id");

            migrationBuilder.CreateIndex(
                name: "ix_sesion_hash",
                table: "sesion",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_cuenta_cuenta_id_proposito_created_at",
                table: "token_cuenta",
                columns: new[] { "cuenta_id", "proposito", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_token_cuenta_hash",
                table: "token_cuenta",
                column: "hash",
                unique: true);

            // RNF-001: el perfil y lo que cuelga de él solo lo ve su docente.
            migrationBuilder.Sql("""
                ALTER TABLE docente ENABLE ROW LEVEL SECURITY;
                ALTER TABLE docente FORCE ROW LEVEL SECURITY;
                CREATE POLICY docente_propio ON docente
                    USING (id = NULLIF(current_setting('app.docente_id', true), '')::uuid)
                    WITH CHECK (id = NULLIF(current_setting('app.docente_id', true), '')::uuid);
                """);
            migrationBuilder.HabilitarRlsPorDocente("aceptacion_legal");
            migrationBuilder.HabilitarRlsPorDocente("exportacion");

            // reglas/03: aceptacion_legal y auditoria solo admiten INSERT para app_runtime.
            migrationBuilder.Sql("""
                REVOKE UPDATE, DELETE ON aceptacion_legal FROM app_runtime;
                REVOKE SELECT, UPDATE, DELETE ON auditoria FROM app_runtime;
                """);

            // RN-006 y RNF-009: el borrado a los 30 días corre con permisos del dueño del esquema,
            // fijando el docente de cada cuenta para que RLS permita borrar sus filas.
            migrationBuilder.Sql("""
                CREATE FUNCTION eliminar_cuentas_vencidas(limite timestamptz) RETURNS integer
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
                REVOKE ALL ON FUNCTION eliminar_cuentas_vencidas(timestamptz) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION eliminar_cuentas_vencidas(timestamptz) TO app_runtime;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS eliminar_cuentas_vencidas(timestamptz);");

            migrationBuilder.DropTable(
                name: "aceptacion_legal");

            migrationBuilder.DropTable(
                name: "auditoria");

            migrationBuilder.DropTable(
                name: "exportacion");

            migrationBuilder.DropTable(
                name: "sesion");

            migrationBuilder.DropTable(
                name: "token_cuenta");

            migrationBuilder.DropTable(
                name: "docente");

            migrationBuilder.DropTable(
                name: "cuenta");
        }
    }
}
