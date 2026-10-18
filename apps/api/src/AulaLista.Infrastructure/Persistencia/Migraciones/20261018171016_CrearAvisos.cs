using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearAvisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aviso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    cuerpo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    urgente = table.Column<bool>(type: "boolean", nullable: false),
                    para_todos = table.Column<bool>(type: "boolean", nullable: false),
                    borrado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aviso", x => x.id);
                    table.ForeignKey(
                        name: "fk_aviso_grupos_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mensaje_whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    costo_estimado = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    id_meta = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensaje_whatsapp", x => x.id);
                    table.ForeignKey(
                        name: "fk_mensaje_whatsapp_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mensaje_whatsapp_tutor_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "suscripcion_push",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    p256dh = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    auth = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suscripcion_push", x => x.id);
                    table.ForeignKey(
                        name: "fk_suscripcion_push_tutor_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entrega_aviso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aviso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    push = table.Column<bool>(type: "boolean", nullable: false),
                    correo = table.Column<bool>(type: "boolean", nullable: false),
                    whatsapp = table.Column<bool>(type: "boolean", nullable: false),
                    sin_saldo_whatsapp = table.Column<bool>(type: "boolean", nullable: false),
                    compartido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    leido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entrega_aviso", x => x.id);
                    table.ForeignKey(
                        name: "fk_entrega_aviso_aviso_aviso_id",
                        column: x => x.aviso_id,
                        principalTable: "aviso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_entrega_aviso_tutor_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aviso_grupo_id_created_at",
                table: "aviso",
                columns: new[] { "grupo_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_entrega_aviso_aviso_id_tutor_id",
                table: "entrega_aviso",
                columns: new[] { "aviso_id", "tutor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entrega_aviso_tutor_id",
                table: "entrega_aviso",
                column: "tutor_id");

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_whatsapp_grupo_id_plantilla_tutor_id_created_at",
                table: "mensaje_whatsapp",
                columns: new[] { "grupo_id", "plantilla", "tutor_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_whatsapp_id_meta",
                table: "mensaje_whatsapp",
                column: "id_meta",
                unique: true,
                filter: "id_meta IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_whatsapp_referencia_id",
                table: "mensaje_whatsapp",
                column: "referencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_whatsapp_tutor_id",
                table: "mensaje_whatsapp",
                column: "tutor_id");

            migrationBuilder.CreateIndex(
                name: "ix_suscripcion_push_tutor_id_endpoint",
                table: "suscripcion_push",
                columns: new[] { "tutor_id", "endpoint" },
                unique: true);

            // SPEC-07: avisos, entregas y mensajes son del docente. El tutor lee sus entregas y los avisos no borrados
            // que le llegaron, y solo marca la lectura de las suyas (RN-075).
            const string Tutor = "NULLIF(current_setting('app.tutor_id', true), '')::uuid";
            const string Docente = "NULLIF(current_setting('app.docente_id', true), '')::uuid";
            migrationBuilder.HabilitarRlsPorDocente("aviso");
            migrationBuilder.HabilitarRlsPorDocente("entrega_aviso");
            migrationBuilder.HabilitarRlsPorDocente("mensaje_whatsapp");
            migrationBuilder.HabilitarLecturaDelTutor("entrega_aviso", $"tutor_id = {Tutor}");
            migrationBuilder.HabilitarLecturaDelTutor(
                "aviso", "borrado_en IS NULL AND EXISTS (SELECT 1 FROM entrega_aviso e WHERE e.aviso_id = aviso.id)");
            migrationBuilder.Sql($"""
                CREATE POLICY entrega_aviso_leida_por_tutor ON entrega_aviso FOR UPDATE
                    USING (tutor_id = {Tutor}) WITH CHECK (tutor_id = {Tutor});
                """);

            // RF-064: el navegador del tutor es suyo; el docente solo ve si tiene push y borra la que el servicio dio
            // por vencida, y nada más que de tutores con los que tiene un vínculo.
            migrationBuilder.Sql($"""
                ALTER TABLE suscripcion_push ENABLE ROW LEVEL SECURITY;
                ALTER TABLE suscripcion_push FORCE ROW LEVEL SECURITY;
                CREATE POLICY suscripcion_push_por_tutor ON suscripcion_push
                    USING (tutor_id = {Tutor}) WITH CHECK (tutor_id = {Tutor});
                CREATE POLICY suscripcion_push_lectura_docente ON suscripcion_push FOR SELECT
                    USING (EXISTS (SELECT 1 FROM vinculo_tutor v WHERE v.tutor_id = suscripcion_push.tutor_id AND v.docente_id = {Docente}));
                CREATE POLICY suscripcion_push_borrado_docente ON suscripcion_push FOR DELETE
                    USING (EXISTS (SELECT 1 FROM vinculo_tutor v WHERE v.tutor_id = suscripcion_push.tutor_id AND v.docente_id = {Docente}));
                """);

            // RF-064: el tutor guarda su propio correo. tutor no se escribe directo (ADR-0014).
            migrationBuilder.Sql("""
                CREATE FUNCTION fijar_correo_tutor(correo text) RETURNS void
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                DECLARE
                    yo uuid := NULLIF(current_setting('app.tutor_id', true), '')::uuid;
                BEGIN
                    IF yo IS NULL THEN
                        RAISE EXCEPTION 'fijar_correo_tutor requiere un tutor';
                    END IF;
                    UPDATE tutor AS t SET correo = fijar_correo_tutor.correo, updated_at = now() WHERE t.id = yo;
                END;
                $$;
                REVOKE ALL ON FUNCTION fijar_correo_tutor(text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION fijar_correo_tutor(text) TO app_runtime;
                """);

            // El webhook de Meta no tiene docente: solo puede tocar el mensaje con el id que trae, y el estado solo
            // avanza (pendiente, enviado, entregado, leído; fallido es final y solo antes de entregarse).
            migrationBuilder.Sql("""
                CREATE POLICY mensaje_whatsapp_por_meta ON mensaje_whatsapp
                    USING (id_meta = NULLIF(current_setting('app.id_meta', true), ''))
                    WITH CHECK (id_meta = NULLIF(current_setting('app.id_meta', true), ''));

                CREATE FUNCTION actualizar_mensaje_whatsapp(meta text, nuevo text) RETURNS boolean
                LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
                DECLARE
                    orden text[] := ARRAY['pendiente', 'enviado', 'entregado', 'leido'];
                    cambiados integer;
                BEGIN
                    PERFORM set_config('app.id_meta', meta, true);
                    UPDATE mensaje_whatsapp AS m SET estado = nuevo, updated_at = now()
                    WHERE m.id_meta = meta AND m.estado <> 'fallido'
                        AND ((nuevo = 'fallido' AND m.estado IN ('pendiente', 'enviado'))
                            OR array_position(orden, nuevo) > array_position(orden, m.estado));
                    GET DIAGNOSTICS cambiados = ROW_COUNT;
                    PERFORM set_config('app.id_meta', '', true);
                    RETURN cambiados > 0;
                END $$;
                REVOKE ALL ON FUNCTION actualizar_mensaje_whatsapp(text, text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION actualizar_mensaje_whatsapp(text, text) TO app_runtime;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS actualizar_mensaje_whatsapp(text, text);
                DROP FUNCTION IF EXISTS fijar_correo_tutor(text);
                """);

            migrationBuilder.DropTable(
                name: "entrega_aviso");

            migrationBuilder.DropTable(
                name: "mensaje_whatsapp");

            migrationBuilder.DropTable(
                name: "suscripcion_push");

            migrationBuilder.DropTable(
                name: "aviso");
        }
    }
}
