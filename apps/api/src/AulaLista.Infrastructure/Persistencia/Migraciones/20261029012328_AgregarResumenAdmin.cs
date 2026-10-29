using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarResumenAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RF-094: el resumen del panel cuenta alumnos, avisos y boletas entre docentes. Igual que en CrearOperacion, el rol
            // de migraciones solo gana lectura para las funciones admin_*, que devuelven conteos y nunca filas (RN-090).
            // Un docente activo es el que abrió la app: cada inicio o renovación de sesión deja una fila en sesion.
            migrationBuilder.Sql("""
                CREATE POLICY alumno_panel ON alumno FOR SELECT TO app_migraciones USING (true);
                CREATE POLICY aviso_panel ON aviso FOR SELECT TO app_migraciones USING (true);
                CREATE POLICY entrega_aviso_panel ON entrega_aviso FOR SELECT TO app_migraciones USING (true);
                CREATE POLICY boleta_panel ON boleta FOR SELECT TO app_migraciones USING (true);

                -- Ventanas móviles que terminan en "ahora": 1, 7 y 30 días.
                CREATE FUNCTION admin_resumen(ahora timestamptz)
                RETURNS TABLE (docentes integer, activos_dia integer, activos_semana integer, activos_mes integer,
                    registros_semana integer, registros_mes integer, grupos integer, alumnos integer, bajas_mes integer,
                    boletas_mes integer, observaciones_mes integer, avisos_mes integer, entregas_mes integer,
                    entregas_leidas_mes integer)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY
                    WITH activas AS (
                        SELECT c.id, c.created_at FROM cuenta c JOIN docente d ON d.id = c.id WHERE c.estado = 'activa'
                    ),
                    usos AS (
                        SELECT s.cuenta_id, max(s.created_at) AS ultimo
                        FROM sesion s JOIN activas a ON a.id = s.cuenta_id
                        WHERE s.created_at > ahora - interval '30 days' AND s.created_at <= ahora
                        GROUP BY s.cuenta_id
                    ),
                    avisos AS (
                        SELECT a.id FROM aviso a
                        WHERE a.borrado_en IS NULL AND a.created_at > ahora - interval '30 days' AND a.created_at <= ahora
                    )
                    SELECT
                        (SELECT count(*) FROM activas)::integer,
                        (SELECT count(*) FROM usos u WHERE u.ultimo > ahora - interval '1 day')::integer,
                        (SELECT count(*) FROM usos u WHERE u.ultimo > ahora - interval '7 days')::integer,
                        (SELECT count(*) FROM usos)::integer,
                        (SELECT count(*) FROM activas a WHERE a.created_at > ahora - interval '7 days' AND a.created_at <= ahora)::integer,
                        (SELECT count(*) FROM activas a WHERE a.created_at > ahora - interval '30 days' AND a.created_at <= ahora)::integer,
                        (SELECT count(*) FROM grupo g WHERE g.archivado_en IS NULL)::integer,
                        (SELECT count(*) FROM alumno al JOIN grupo g ON g.id = al.grupo_id
                            WHERE al.baja_en IS NULL AND g.archivado_en IS NULL)::integer,
                        (SELECT count(*) FROM suscripcion su WHERE su.estado = 'cancelada'
                            AND su.updated_at > ahora - interval '30 days' AND su.updated_at <= ahora)::integer,
                        (SELECT count(*) FROM boleta b
                            WHERE b.publicada_en > ahora - interval '30 days' AND b.publicada_en <= ahora)::integer,
                        (SELECT count(*) FROM evento_producto e WHERE e.nombre = 'observacion_aprobada'
                            AND e.fecha > ahora - interval '30 days' AND e.fecha <= ahora)::integer,
                        (SELECT count(*) FROM avisos)::integer,
                        (SELECT count(*) FROM entrega_aviso en JOIN avisos a ON a.id = en.aviso_id)::integer,
                        (SELECT count(*) FROM entrega_aviso en JOIN avisos a ON a.id = en.aviso_id WHERE en.leido_en IS NOT NULL)::integer;
                END $$;

                -- Semanas de lunes a domingo, hora del centro de México, como admin_metricas. "Pagan" es al cierre de la
                -- semana: activaron su suscripción antes y no la tenían cancelada.
                CREATE FUNCTION admin_resumen_semanas(desde date, semanas integer)
                RETURNS TABLE (semana date, activos integer, registros integer, pagan integer, altas_pago integer, bajas integer,
                    boletas integer, observaciones integer, avisos integer)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY
                    WITH limites AS (
                        SELECT s::date AS inicio,
                            (s AT TIME ZONE 'America/Mexico_City') AS desde_ts,
                            ((s + interval '7 days') AT TIME ZONE 'America/Mexico_City') AS hasta_ts
                        FROM generate_series(desde::timestamp, desde::timestamp + (semanas - 1) * interval '7 days', interval '7 days') s
                    )
                    SELECT l.inicio,
                        (SELECT count(DISTINCT s.cuenta_id) FROM sesion s JOIN docente d ON d.id = s.cuenta_id
                            WHERE s.created_at >= l.desde_ts AND s.created_at < l.hasta_ts)::integer,
                        (SELECT count(*) FROM cuenta c JOIN docente d ON d.id = c.id
                            WHERE c.created_at >= l.desde_ts AND c.created_at < l.hasta_ts)::integer,
                        (SELECT count(DISTINCT e.docente_id) FROM evento_producto e
                            LEFT JOIN suscripcion su ON su.docente_id = e.docente_id
                            WHERE e.nombre = 'suscripcion_activada' AND e.fecha < l.hasta_ts
                                AND NOT coalesce(su.estado = 'cancelada' AND su.updated_at < l.hasta_ts, false))::integer,
                        (SELECT count(*) FROM evento_producto e WHERE e.nombre = 'suscripcion_activada'
                            AND e.fecha >= l.desde_ts AND e.fecha < l.hasta_ts)::integer,
                        (SELECT count(*) FROM suscripcion su WHERE su.estado = 'cancelada'
                            AND su.updated_at >= l.desde_ts AND su.updated_at < l.hasta_ts)::integer,
                        (SELECT count(*) FROM boleta b WHERE b.publicada_en >= l.desde_ts AND b.publicada_en < l.hasta_ts)::integer,
                        (SELECT count(*) FROM evento_producto e WHERE e.nombre = 'observacion_aprobada'
                            AND e.fecha >= l.desde_ts AND e.fecha < l.hasta_ts)::integer,
                        (SELECT count(*) FROM aviso a WHERE a.borrado_en IS NULL
                            AND a.created_at >= l.desde_ts AND a.created_at < l.hasta_ts)::integer
                    FROM limites l
                    ORDER BY l.inicio;
                END $$;

                -- Hasta dónde llegó cada docente con la cuenta activa, de todos los registrados.
                CREATE FUNCTION admin_embudo()
                RETURNS TABLE (registrados integer, con_grupo integer, con_alumnos integer, con_calificacion integer,
                    con_cierre integer, con_boletas integer)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY
                    WITH activas AS (
                        SELECT c.id FROM cuenta c JOIN docente d ON d.id = c.id WHERE c.estado = 'activa'
                    )
                    SELECT
                        count(*)::integer,
                        count(*) FILTER (WHERE EXISTS (SELECT 1 FROM grupo g WHERE g.docente_id = a.id))::integer,
                        count(*) FILTER (WHERE EXISTS (SELECT 1 FROM alumno al WHERE al.docente_id = a.id))::integer,
                        count(*) FILTER (WHERE EXISTS (SELECT 1 FROM evento_producto e
                            WHERE e.docente_id = a.id AND e.nombre = 'calificacion_capturada'))::integer,
                        count(*) FILTER (WHERE EXISTS (SELECT 1 FROM evento_producto e
                            WHERE e.docente_id = a.id AND e.nombre = 'cierre_iniciado'))::integer,
                        count(*) FILTER (WHERE EXISTS (SELECT 1 FROM evento_producto e
                            WHERE e.docente_id = a.id AND e.nombre = 'boletas_publicadas'))::integer
                    FROM activas a;
                END $$;

                -- Cohortes por semana de registro: cuántos volvieron a abrir la app en la semana 1, 2 y 4 después de
                -- registrarse (días 7 a 13, 14 a 20 y 28 a 34 desde su registro).
                CREATE FUNCTION admin_retencion(desde date, semanas integer)
                RETURNS TABLE (cohorte date, docentes integer, semana1 integer, semana2 integer, semana4 integer)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY
                    WITH registros AS (
                        SELECT c.id, c.created_at, date_trunc('week', c.created_at AT TIME ZONE 'America/Mexico_City')::date AS cohorte
                        FROM cuenta c JOIN docente d ON d.id = c.id
                    ),
                    volvio AS (
                        SELECT r.id, r.cohorte,
                            EXISTS (SELECT 1 FROM sesion s WHERE s.cuenta_id = r.id
                                AND s.created_at >= r.created_at + interval '7 days' AND s.created_at < r.created_at + interval '14 days') AS s1,
                            EXISTS (SELECT 1 FROM sesion s WHERE s.cuenta_id = r.id
                                AND s.created_at >= r.created_at + interval '14 days' AND s.created_at < r.created_at + interval '21 days') AS s2,
                            EXISTS (SELECT 1 FROM sesion s WHERE s.cuenta_id = r.id
                                AND s.created_at >= r.created_at + interval '28 days' AND s.created_at < r.created_at + interval '35 days') AS s4
                        FROM registros r
                        WHERE r.cohorte >= desde AND r.cohorte < desde + semanas * 7
                    )
                    SELECT c::date,
                        count(v.id)::integer,
                        count(v.id) FILTER (WHERE v.s1)::integer,
                        count(v.id) FILTER (WHERE v.s2)::integer,
                        count(v.id) FILTER (WHERE v.s4)::integer
                    FROM generate_series(desde::timestamp, desde::timestamp + (semanas - 1) * interval '7 days', interval '7 days') c
                    LEFT JOIN volvio v ON v.cohorte = c::date
                    GROUP BY c
                    ORDER BY c;
                END $$;
                """);
            foreach (var funcion in FuncionesResumen)
            {
                migrationBuilder.Sql($"""
                    REVOKE ALL ON FUNCTION {funcion} FROM PUBLIC;
                    GRANT EXECUTE ON FUNCTION {funcion} TO app_runtime;
                    """);
            }
        }

        private static readonly string[] FuncionesResumen =
        [
            "admin_resumen(timestamptz)", "admin_resumen_semanas(date, integer)", "admin_embudo()", "admin_retencion(date, integer)",
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS admin_retencion(date, integer);
                DROP FUNCTION IF EXISTS admin_embudo();
                DROP FUNCTION IF EXISTS admin_resumen_semanas(date, integer);
                DROP FUNCTION IF EXISTS admin_resumen(timestamptz);
                DROP POLICY IF EXISTS boleta_panel ON boleta;
                DROP POLICY IF EXISTS entrega_aviso_panel ON entrega_aviso;
                DROP POLICY IF EXISTS aviso_panel ON aviso;
                DROP POLICY IF EXISTS alumno_panel ON alumno;
                """);
        }
    }
}
