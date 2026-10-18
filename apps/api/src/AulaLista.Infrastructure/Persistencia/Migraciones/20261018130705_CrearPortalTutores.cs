using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearPortalTutores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "enlace_tutor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_por_docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revocado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_uso_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sospechoso_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enlace_tutor", x => x.id);
                    table.ForeignKey(
                        name: "fk_enlace_tutor_tutor_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesion_tutor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enlace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revocada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_uso_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sesion_tutor", x => x.id);
                    table.ForeignKey(
                        name: "fk_sesion_tutor_enlace_tutor_enlace_id",
                        column: x => x.enlace_id,
                        principalTable: "enlace_tutor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sesion_tutor_tutor_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_enlace_tutor_token_hash",
                table: "enlace_tutor",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enlace_tutor_tutor_id",
                table: "enlace_tutor",
                column: "tutor_id");

            migrationBuilder.CreateIndex(
                name: "ix_sesion_tutor_enlace_id_created_at",
                table: "sesion_tutor",
                columns: new[] { "enlace_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sesion_tutor_token_hash",
                table: "sesion_tutor",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sesion_tutor_tutor_id",
                table: "sesion_tutor",
                column: "tutor_id");

            // RN-060, RN-063: enlaces y sesiones los ve su tutor y el docente vinculado con él. Antes de saber quién
            // es el tutor solo se pregunta de quién es un hash conocido: la política por token deja ver esa fila y
            // nada más, y las funciones la usan sin dejar la variable puesta.
            const string Tutor = "NULLIF(current_setting('app.tutor_id', true), '')::uuid";
            const string Docente = "NULLIF(current_setting('app.docente_id', true), '')::uuid";
            foreach (var tabla in new[] { "enlace_tutor", "sesion_tutor" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY {tabla}_por_tutor ON {tabla}
                        USING (tutor_id = {Tutor}) WITH CHECK (tutor_id = {Tutor});
                    CREATE POLICY {tabla}_por_docente ON {tabla}
                        USING (EXISTS (SELECT 1 FROM vinculo_tutor v WHERE v.tutor_id = {tabla}.tutor_id AND v.docente_id = {Docente}))
                        WITH CHECK (EXISTS (SELECT 1 FROM vinculo_tutor v WHERE v.tutor_id = {tabla}.tutor_id AND v.docente_id = {Docente}));
                    CREATE POLICY {tabla}_por_token ON {tabla} FOR SELECT
                        USING (token_hash = NULLIF(current_setting('app.token_hash', true), ''));
                    """);
            }

            foreach (var (funcion, tabla) in new[] { ("tutor_del_enlace", "enlace_tutor"), ("tutor_de_la_sesion", "sesion_tutor") })
            {
                migrationBuilder.Sql($"""
                    CREATE FUNCTION {funcion}(hash text) RETURNS uuid
                    LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
                    DECLARE
                        encontrado uuid;
                    BEGIN
                        PERFORM set_config('app.token_hash', hash, true);
                        SELECT t.tutor_id INTO encontrado FROM {tabla} t WHERE t.token_hash = hash;
                        PERFORM set_config('app.token_hash', '', true);
                        RETURN encontrado;
                    END $$;
                    REVOKE ALL ON FUNCTION {funcion}(text) FROM PUBLIC;
                    GRANT EXECUTE ON FUNCTION {funcion}(text) TO app_runtime;
                    """);
            }

            // RN-061, RN-064: el tutor lee sus vínculos, a sus hijos con su grupo, docente y unidades, y solo las
            // boletas publicadas y vigentes.
            migrationBuilder.HabilitarLecturaDelTutor("vinculo_tutor", $"tutor_id = {Tutor}");
            migrationBuilder.HabilitarLecturaDelTutor(
                "alumno", "EXISTS (SELECT 1 FROM vinculo_tutor v WHERE v.alumno_id = alumno.id)");
            migrationBuilder.HabilitarLecturaDelTutor(
                "grupo", "EXISTS (SELECT 1 FROM alumno a WHERE a.grupo_id = grupo.id)");
            migrationBuilder.HabilitarLecturaDelTutor(
                "docente", "EXISTS (SELECT 1 FROM vinculo_tutor v WHERE v.docente_id = docente.id)");
            migrationBuilder.HabilitarLecturaDelTutor(
                "boleta",
                "publicada_en IS NOT NULL AND reemplazada_por IS NULL "
                + "AND EXISTS (SELECT 1 FROM vinculo_tutor v WHERE v.alumno_id = boleta.alumno_id)");
            migrationBuilder.HabilitarLecturaDelTutor(
                "periodo", "EXISTS (SELECT 1 FROM boleta b WHERE b.periodo_id = periodo.id)");
            migrationBuilder.HabilitarLecturaDelTutor(
                "unidad", "EXISTS (SELECT 1 FROM periodo p WHERE p.unidad_id = unidad.id)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tabla in new[] { "unidad", "periodo", "boleta", "docente", "grupo", "alumno", "vinculo_tutor" })
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {tabla}_del_tutor ON {tabla};");
            }

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS tutor_del_enlace(text); DROP FUNCTION IF EXISTS tutor_de_la_sesion(text);");

            migrationBuilder.DropTable(
                name: "sesion_tutor");

            migrationBuilder.DropTable(
                name: "enlace_tutor");
        }
    }
}
