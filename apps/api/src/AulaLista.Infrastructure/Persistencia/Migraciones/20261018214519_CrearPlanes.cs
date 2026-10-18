using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearPlanes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pro_hasta",
                table: "grupo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evento_pago",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_externo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evento_pago", x => x.id);
                    table.ForeignKey(
                        name: "fk_evento_pago_docente_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pago",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    meses = table.Column<int>(type: "integer", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    grupos = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    sesion_externa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url_ficha = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vence_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pagado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pago", x => x.id);
                    table.CheckConstraint("ck_pago_estado", "estado IN ('pendiente', 'pagado', 'vencido')");
                    table.CheckConstraint("ck_pago_monto", "monto > 0");
                    table.ForeignKey(
                        name: "fk_pago_docente_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "suscripcion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    modalidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cliente_externo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    id_externo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    grupos_pro = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    periodo_actual_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pagado_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    gracia_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aviso_vencimiento_dias = table.Column<int>(type: "integer", nullable: true),
                    prueba_grupo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prueba_periodo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prueba_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    prueba_publicada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suscripcion", x => x.id);
                    table.CheckConstraint("ck_suscripcion_estado", "estado IN ('ninguna', 'activa', 'en_gracia', 'cancelada')");
                    table.CheckConstraint("ck_suscripcion_modalidad", "modalidad IS NULL OR modalidad IN ('tarjeta', 'prepago_oxxo')");
                    table.ForeignKey(
                        name: "fk_suscripcion_docente_docente_id",
                        column: x => x.docente_id,
                        principalTable: "docente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evento_pago_docente_id",
                table: "evento_pago",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_evento_pago_id_externo",
                table: "evento_pago",
                column: "id_externo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pago_docente_id_created_at",
                table: "pago",
                columns: new[] { "docente_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_pago_sesion_externa",
                table: "pago",
                column: "sesion_externa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suscripcion_docente_id",
                table: "suscripcion",
                column: "docente_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suscripcion_id_externo",
                table: "suscripcion",
                column: "id_externo",
                unique: true,
                filter: "id_externo IS NOT NULL");

            // SPEC-08: suscripción, pagos y eventos son del docente. Los webhooks corren como el docente del evento.
            migrationBuilder.HabilitarRlsPorDocente("suscripcion");
            migrationBuilder.HabilitarRlsPorDocente("pago");
            migrationBuilder.HabilitarRlsPorDocente("evento_pago");

            // RN-085, RN-089: la revisión diaria no tiene docente; solo obtiene a quién revisar. Lee como el dueño del
            // esquema (SECURITY DEFINER), al que estas políticas de solo lectura le dejan ver todas las filas; app_runtime
            // no puede asumir ese rol.
            migrationBuilder.Sql("""
                CREATE POLICY suscripcion_revision_diaria ON suscripcion FOR SELECT TO app_migraciones USING (true);
                CREATE POLICY grupo_revision_diaria ON grupo FOR SELECT TO app_migraciones USING (true);
                CREATE FUNCTION docentes_con_plan() RETURNS TABLE (docente_id uuid)
                LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                    SELECT s.docente_id FROM suscripcion s WHERE s.estado IN ('activa', 'en_gracia')
                    UNION
                    SELECT g.docente_id FROM grupo g WHERE g.pro_hasta IS NOT NULL;
                $$;
                REVOKE ALL ON FUNCTION docentes_con_plan() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION docentes_con_plan() TO app_runtime;
                """);

            // RN-088 y RNF-022: consumo de IA y de WhatsApp por grupo y mes (hora del centro de México), sumado por
            // disparadores para que ningún camino lo olvide. El costo de la IA lo calcula el panel con los precios
            // vigentes; el de WhatsApp es el estimado de cada mensaje.
            migrationBuilder.Sql("""
                CREATE TABLE consumo_mensual (
                    docente_id uuid NOT NULL REFERENCES docente (id) ON DELETE CASCADE,
                    grupo_id uuid NOT NULL REFERENCES grupo (id) ON DELETE CASCADE,
                    mes date NOT NULL,
                    llamadas_ia integer NOT NULL DEFAULT 0,
                    tokens_entrada bigint NOT NULL DEFAULT 0,
                    tokens_salida bigint NOT NULL DEFAULT 0,
                    tokens_cache_leidos bigint NOT NULL DEFAULT 0,
                    mensajes_whatsapp integer NOT NULL DEFAULT 0,
                    costo_whatsapp numeric(12,4) NOT NULL DEFAULT 0,
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT pk_consumo_mensual PRIMARY KEY (grupo_id, mes)
                );
                CREATE INDEX ix_consumo_mensual_docente_id_mes ON consumo_mensual (docente_id, mes);

                CREATE FUNCTION sumar_consumo_ia() RETURNS trigger
                LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
                BEGIN
                    INSERT INTO consumo_mensual AS c (docente_id, grupo_id, mes, llamadas_ia, tokens_entrada, tokens_salida, tokens_cache_leidos)
                    VALUES (NEW.docente_id, NEW.grupo_id, date_trunc('month', NEW.created_at AT TIME ZONE 'America/Mexico_City')::date, 1,
                        NEW.tokens_entrada, NEW.tokens_salida, NEW.tokens_cache_leidos)
                    ON CONFLICT (grupo_id, mes) DO UPDATE SET
                        llamadas_ia = c.llamadas_ia + 1,
                        tokens_entrada = c.tokens_entrada + EXCLUDED.tokens_entrada,
                        tokens_salida = c.tokens_salida + EXCLUDED.tokens_salida,
                        tokens_cache_leidos = c.tokens_cache_leidos + EXCLUDED.tokens_cache_leidos,
                        updated_at = now();
                    RETURN NEW;
                END $$;
                CREATE TRIGGER llamada_ia_consumo AFTER INSERT ON llamada_ia FOR EACH ROW EXECUTE FUNCTION sumar_consumo_ia();

                CREATE FUNCTION sumar_consumo_whatsapp() RETURNS trigger
                LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
                BEGIN
                    INSERT INTO consumo_mensual AS c (docente_id, grupo_id, mes, mensajes_whatsapp, costo_whatsapp)
                    VALUES (NEW.docente_id, NEW.grupo_id, date_trunc('month', NEW.created_at AT TIME ZONE 'America/Mexico_City')::date, 1,
                        NEW.costo_estimado)
                    ON CONFLICT (grupo_id, mes) DO UPDATE SET
                        mensajes_whatsapp = c.mensajes_whatsapp + 1,
                        costo_whatsapp = c.costo_whatsapp + EXCLUDED.costo_whatsapp,
                        updated_at = now();
                    RETURN NEW;
                END $$;
                CREATE TRIGGER mensaje_whatsapp_consumo AFTER INSERT ON mensaje_whatsapp FOR EACH ROW EXECUTE FUNCTION sumar_consumo_whatsapp();
                """);
            migrationBuilder.HabilitarRlsPorDocente("consumo_mensual");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS mensaje_whatsapp_consumo ON mensaje_whatsapp;
                DROP TRIGGER IF EXISTS llamada_ia_consumo ON llamada_ia;
                DROP FUNCTION IF EXISTS sumar_consumo_whatsapp();
                DROP FUNCTION IF EXISTS sumar_consumo_ia();
                DROP TABLE IF EXISTS consumo_mensual;
                DROP FUNCTION IF EXISTS docentes_con_plan();
                DROP POLICY IF EXISTS grupo_revision_diaria ON grupo;
                """);

            migrationBuilder.DropTable(
                name: "evento_pago");

            migrationBuilder.DropTable(
                name: "pago");

            migrationBuilder.DropTable(
                name: "suscripcion");

            migrationBuilder.DropColumn(
                name: "pro_hasta",
                table: "grupo");
        }
    }
}
