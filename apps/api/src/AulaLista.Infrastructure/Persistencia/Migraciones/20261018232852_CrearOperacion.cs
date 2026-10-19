using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearOperacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rol",
                table: "cuenta",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "docente");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cierre_iniciado_en",
                table: "periodo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "auditoria_admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    antes = table.Column<string>(type: "jsonb", nullable: true),
                    despues = table.Column<string>(type: "jsonb", nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria_admin", x => x.id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_cuenta_rol",
                table: "cuenta",
                sql: "rol IN ('docente', 'admin')");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_admin_fecha",
                table: "auditoria_admin",
                column: "fecha");

            // RN-095: la auditoría del panel solo se inserta, y solo como el administrador de la solicitud.
            migrationBuilder.Sql("""
                ALTER TABLE auditoria_admin ENABLE ROW LEVEL SECURITY;
                ALTER TABLE auditoria_admin FORCE ROW LEVEL SECURITY;
                CREATE POLICY auditoria_admin_alta ON auditoria_admin FOR INSERT
                    WITH CHECK (admin_id = NULLIF(current_setting('app.admin_id', true), '')::uuid);
                CREATE POLICY auditoria_admin_panel ON auditoria_admin FOR SELECT TO app_migraciones USING (true);
                REVOKE SELECT, UPDATE, DELETE ON auditoria_admin FROM app_runtime;
                """);

            // RF-090: eventos de producto sin datos personales. La clave, cuando la hay, evita repetir un evento (una
            // captura por día y vista). Se borran con la cuenta del docente (RNF-009).
            // SPEC-09, Errores: errores de los trabajos en segundo plano, solo con el tipo de excepción.
            migrationBuilder.Sql("""
                CREATE TABLE evento_producto (
                    id uuid NOT NULL,
                    docente_id uuid NOT NULL REFERENCES docente (id) ON DELETE CASCADE,
                    nombre character varying(40) NOT NULL,
                    propiedades jsonb NOT NULL DEFAULT '{}',
                    clave character varying(80),
                    fecha timestamp with time zone NOT NULL,
                    CONSTRAINT pk_evento_producto PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX ux_evento_producto_clave ON evento_producto (docente_id, nombre, clave) WHERE clave IS NOT NULL;
                CREATE INDEX ix_evento_producto_nombre_fecha ON evento_producto (nombre, fecha);
                -- Solo de alta para la app: se registra desde solicitudes de docentes, de tutores y sin sesión (registro,
                -- webhooks, trabajos), y nadie de la app la lee. Las lecturas son de las funciones admin_* (dueño).
                REVOKE SELECT, UPDATE, DELETE ON evento_producto FROM app_runtime;
                ALTER TABLE evento_producto ENABLE ROW LEVEL SECURITY;
                ALTER TABLE evento_producto FORCE ROW LEVEL SECURITY;
                CREATE POLICY evento_producto_alta ON evento_producto FOR INSERT TO app_runtime WITH CHECK (true);
                CREATE POLICY evento_producto_panel ON evento_producto FOR SELECT TO app_migraciones USING (true);

                CREATE TABLE error_trabajo (
                    id uuid NOT NULL,
                    origen character varying(20) NOT NULL,
                    trabajo character varying(60) NOT NULL,
                    docente_id uuid REFERENCES docente (id) ON DELETE SET NULL,
                    referencia_id uuid,
                    tipo_error character varying(200) NOT NULL,
                    fecha timestamp with time zone NOT NULL,
                    CONSTRAINT pk_error_trabajo PRIMARY KEY (id),
                    CONSTRAINT ck_error_trabajo_origen CHECK (origen IN ('ia', 'whatsapp', 'correo', 'push', 'pagos', 'boletas', 'exportacion', 'retencion'))
                );
                CREATE INDEX ix_error_trabajo_fecha ON error_trabajo (fecha);
                REVOKE SELECT, UPDATE, DELETE ON error_trabajo FROM app_runtime;
                ALTER TABLE error_trabajo ENABLE ROW LEVEL SECURITY;
                ALTER TABLE error_trabajo FORCE ROW LEVEL SECURITY;
                CREATE POLICY error_trabajo_alta ON error_trabajo FOR INSERT TO app_runtime WITH CHECK (true);
                CREATE POLICY error_trabajo_panel ON error_trabajo FOR SELECT TO app_migraciones USING (true);
                """);

            // RF-092: el administrador crea y edita tipos de rubro (RLS con app.admin_id); nadie los borra (RN-094). Sin
            // FORCE, para que las migraciones sigan sembrando el catálogo.
            migrationBuilder.Sql("""
                GRANT INSERT, UPDATE ON tipo_rubro TO app_runtime;
                ALTER TABLE tipo_rubro ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tipo_rubro_lectura ON tipo_rubro FOR SELECT USING (true);
                CREATE POLICY tipo_rubro_alta_admin ON tipo_rubro FOR INSERT
                    WITH CHECK (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL);
                CREATE POLICY tipo_rubro_cambio_admin ON tipo_rubro FOR UPDATE
                    USING (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL)
                    WITH CHECK (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL);
                """);

            // SPEC-09: el panel lee entre docentes solo con estas funciones (SECURITY DEFINER, dueño app_migraciones), que
            // devuelven conteos, correos y nombres de docentes (RN-090) y exigen app.admin_id. Las políticas de solo
            // lectura para app_migraciones se suman a las de la revisión diaria (grupo, suscripcion).
            migrationBuilder.Sql("""
                CREATE POLICY docente_panel ON docente FOR SELECT TO app_migraciones USING (true);
                CREATE POLICY ciclo_escolar_panel ON ciclo_escolar FOR SELECT TO app_migraciones USING (true);
                CREATE POLICY consumo_mensual_panel ON consumo_mensual FOR SELECT TO app_migraciones USING (true);
                CREATE POLICY enlace_tutor_panel ON enlace_tutor FOR SELECT TO app_migraciones USING (true);
                CREATE POLICY rubro_panel ON rubro FOR SELECT TO app_migraciones USING (true);

                CREATE FUNCTION exigir_admin() RETURNS void
                LANGUAGE plpgsql STABLE SET search_path = public, pg_temp AS $$
                BEGIN
                    IF NULLIF(current_setting('app.admin_id', true), '') IS NULL THEN
                        RAISE EXCEPTION 'solo para administradores' USING ERRCODE = '42501';
                    END IF;
                END $$;

                -- Semanas de lunes a domingo, hora del centro de México. Activos: capturaron en los 14 días previos al
                -- fin de la semana. Cierre, tutores y conversión se acumulan hasta el fin de la semana.
                CREATE FUNCTION admin_metricas(desde date, semanas integer, umbral numeric)
                RETURNS TABLE (semana date, docentes_activos integer, docentes_cierran integer, minutos_cierre numeric,
                    observaciones integer, observaciones_menores integer, enlaces integer, aperturas integer,
                    con_prueba integer, pagan integer)
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
                    ),
                    activos AS (
                        SELECT l.inicio, e.docente_id
                        FROM limites l
                        JOIN evento_producto e ON e.nombre = 'calificacion_capturada'
                            AND e.fecha >= l.hasta_ts - interval '14 days' AND e.fecha < l.hasta_ts
                        GROUP BY l.inicio, e.docente_id
                    )
                    SELECT l.inicio,
                        (SELECT count(*) FROM activos a WHERE a.inicio = l.inicio)::integer,
                        (SELECT count(*) FROM activos a WHERE a.inicio = l.inicio AND EXISTS (
                            SELECT 1 FROM evento_producto b
                            WHERE b.docente_id = a.docente_id AND b.nombre = 'boletas_publicadas' AND b.fecha < l.hasta_ts))::integer,
                        (SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY (e.propiedades ->> 'minutos_desde_inicio_cierre')::numeric)
                            FROM evento_producto e
                            WHERE e.nombre = 'boletas_publicadas' AND e.fecha >= l.desde_ts AND e.fecha < l.hasta_ts)::numeric,
                        (SELECT count(*) FROM evento_producto e
                            WHERE e.nombre = 'observacion_aprobada' AND e.fecha >= l.desde_ts AND e.fecha < l.hasta_ts)::integer,
                        (SELECT count(*) FROM evento_producto e
                            WHERE e.nombre = 'observacion_aprobada' AND e.fecha >= l.desde_ts AND e.fecha < l.hasta_ts
                                AND (e.propiedades ->> 'distancia_edicion')::numeric < umbral)::integer,
                        (SELECT count(*) FROM enlace_tutor t WHERE t.created_at < l.hasta_ts)::integer,
                        (SELECT count(*) FROM evento_producto e
                            WHERE e.nombre = 'enlace_tutor_abierto' AND (e.propiedades ->> 'primera_vez')::boolean
                                AND e.fecha < l.hasta_ts)::integer,
                        (SELECT count(*) FROM suscripcion s WHERE s.prueba_inicio < l.hasta_ts)::integer,
                        (SELECT count(DISTINCT e.docente_id) FROM evento_producto e
                            JOIN suscripcion s ON s.docente_id = e.docente_id
                            WHERE e.nombre = 'suscripcion_activada' AND e.fecha < l.hasta_ts AND s.prueba_inicio <= e.fecha)::integer
                    FROM limites l
                    ORDER BY l.inicio;
                END $$;

                CREATE FUNCTION admin_docentes(mes date)
                RETURNS TABLE (docente_id uuid, correo text, nombre text, estado text, registrado_en timestamptz,
                    ultimo_acceso timestamptz, grupos integer, grupos_pro integer, estado_suscripcion text, modalidad text,
                    prueba_inicio timestamptz, llamadas_ia integer, tokens_entrada bigint, tokens_salida bigint,
                    tokens_cache bigint, mensajes_whatsapp integer, costo_whatsapp numeric)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY
                    SELECT d.id, c.correo::text, d.nombre::text, c.estado::text, c.created_at,
                        (SELECT max(s.created_at) FROM sesion s WHERE s.cuenta_id = c.id),
                        (SELECT count(*) FROM grupo g WHERE g.docente_id = d.id AND g.archivado_en IS NULL)::integer,
                        (SELECT count(*) FROM grupo g WHERE g.docente_id = d.id AND g.plan = 'pro'
                            AND (g.pro_hasta IS NULL OR g.pro_hasta > now()))::integer,
                        su.estado::text, su.modalidad::text, su.prueba_inicio,
                        coalesce(sum(cm.llamadas_ia), 0)::integer, coalesce(sum(cm.tokens_entrada), 0)::bigint,
                        coalesce(sum(cm.tokens_salida), 0)::bigint, coalesce(sum(cm.tokens_cache_leidos), 0)::bigint,
                        coalesce(sum(cm.mensajes_whatsapp), 0)::integer, coalesce(sum(cm.costo_whatsapp), 0)::numeric
                    FROM docente d
                    JOIN cuenta c ON c.id = d.id
                    LEFT JOIN suscripcion su ON su.docente_id = d.id
                    LEFT JOIN consumo_mensual cm ON cm.docente_id = d.id AND cm.mes = admin_docentes.mes
                    GROUP BY d.id, c.id, su.id
                    ORDER BY c.created_at DESC;
                END $$;

                -- Grupos con consumo en el mes, con Pro pagado o con la prueba en curso.
                CREATE FUNCTION admin_costos(mes date, dias_prueba integer)
                RETURNS TABLE (grupo_id uuid, docente_id uuid, correo text, nivel text, grado integer, nombre text, materia text,
                    pro_pagado boolean, en_prueba boolean, llamadas_ia integer, tokens_entrada bigint, tokens_salida bigint,
                    tokens_cache bigint, mensajes_whatsapp integer, costo_whatsapp numeric)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY
                    SELECT g.id, g.docente_id, c.correo::text, g.nivel::text, g.grado, g.nombre::text, g.materia::text,
                        (g.plan = 'pro' AND (g.pro_hasta IS NULL OR g.pro_hasta > now())),
                        coalesce(su.prueba_grupo_id = g.id AND su.prueba_inicio > now() - make_interval(days => dias_prueba), false),
                        coalesce(cm.llamadas_ia, 0), coalesce(cm.tokens_entrada, 0), coalesce(cm.tokens_salida, 0),
                        coalesce(cm.tokens_cache_leidos, 0), coalesce(cm.mensajes_whatsapp, 0), coalesce(cm.costo_whatsapp, 0)::numeric
                    FROM grupo g
                    JOIN cuenta c ON c.id = g.docente_id
                    LEFT JOIN consumo_mensual cm ON cm.grupo_id = g.id AND cm.mes = admin_costos.mes
                    LEFT JOIN suscripcion su ON su.docente_id = g.docente_id
                    WHERE cm.grupo_id IS NOT NULL
                        OR (g.plan = 'pro' AND (g.pro_hasta IS NULL OR g.pro_hasta > now()))
                        OR (su.prueba_grupo_id = g.id AND su.prueba_inicio > now() - make_interval(days => dias_prueba));
                END $$;

                CREATE FUNCTION admin_errores(limite integer)
                RETURNS TABLE (id uuid, origen text, trabajo text, docente_id uuid, correo text, referencia_id uuid,
                    tipo_error text, fecha timestamptz)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY
                    SELECT e.id, e.origen::text, e.trabajo::text, e.docente_id, c.correo::text, e.referencia_id, e.tipo_error::text, e.fecha
                    FROM error_trabajo e
                    LEFT JOIN cuenta c ON c.id = e.docente_id
                    ORDER BY e.fecha DESC
                    LIMIT limite;
                END $$;

                CREATE FUNCTION admin_auditoria(antes_de timestamptz, limite integer)
                RETURNS TABLE (id uuid, admin_id uuid, correo text, accion text, entidad text, entidad_id uuid, antes text,
                    despues text, fecha timestamptz)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY
                    SELECT a.id, a.admin_id, c.correo::text, a.accion::text, a.entidad::text, a.entidad_id, a.antes::text,
                        a.despues::text, a.fecha
                    FROM auditoria_admin a
                    LEFT JOIN cuenta c ON c.id = a.admin_id
                    WHERE antes_de IS NULL OR a.fecha < antes_de
                    ORDER BY a.fecha DESC
                    LIMIT limite;
                END $$;

                CREATE FUNCTION admin_rubros_por_tipo()
                RETURNS TABLE (tipo_rubro_id uuid, rubros bigint)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY SELECT r.tipo_rubro_id, count(*) FROM rubro r GROUP BY r.tipo_rubro_id;
                END $$;
                """);
            foreach (var funcion in FuncionesPanel)
            {
                migrationBuilder.Sql($"""
                    REVOKE ALL ON FUNCTION {funcion} FROM PUBLIC;
                    GRANT EXECUTE ON FUNCTION {funcion} TO app_runtime;
                    """);
            }

            // RNF-009: grupos archivados cuyo ciclo terminó antes del límite, como su docente para que RLS deje borrar.
            // Devuelve sus periodos para borrar los PDF; los tutores que se quedan sin vínculos también se borran.
            migrationBuilder.Sql("""
                CREATE FUNCTION eliminar_grupos_archivados_vencidos(limite date) RETURNS TABLE (periodo_id uuid)
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                DECLARE
                    r record;
                    tutores uuid[];
                    t uuid;
                BEGIN
                    FOR r IN SELECT g.id, g.docente_id FROM grupo g
                             JOIN ciclo_escolar ce ON ce.id = g.ciclo_escolar_id
                             WHERE g.archivado_en IS NOT NULL AND ce.fin < limite
                    LOOP
                        PERFORM set_config('app.docente_id', r.docente_id::text, true);
                        tutores := ARRAY(SELECT DISTINCT v.tutor_id FROM vinculo_tutor v JOIN alumno a ON a.id = v.alumno_id
                                         WHERE a.grupo_id = r.id);
                        RETURN QUERY SELECT p.id FROM periodo p WHERE p.grupo_id = r.id;
                        DELETE FROM grupo WHERE id = r.id;
                        FOREACH t IN ARRAY tutores LOOP
                            BEGIN
                                DELETE FROM tutor WHERE id = t;
                            EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN
                                NULL;
                            END;
                        END LOOP;
                    END LOOP;
                    PERFORM set_config('app.docente_id', '', true);
                END;
                $$;
                REVOKE ALL ON FUNCTION eliminar_grupos_archivados_vencidos(date) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION eliminar_grupos_archivados_vencidos(date) TO app_runtime;
                """);
        }

        private static readonly string[] FuncionesPanel =
        [
            "admin_metricas(date, integer, numeric)", "admin_docentes(date)", "admin_costos(date, integer)", "admin_errores(integer)",
            "admin_auditoria(timestamptz, integer)", "admin_rubros_por_tipo()",
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS eliminar_grupos_archivados_vencidos(date);
                DROP FUNCTION IF EXISTS admin_rubros_por_tipo();
                DROP FUNCTION IF EXISTS admin_auditoria(timestamptz, integer);
                DROP FUNCTION IF EXISTS admin_errores(integer);
                DROP FUNCTION IF EXISTS admin_costos(date, integer);
                DROP FUNCTION IF EXISTS admin_docentes(date);
                DROP FUNCTION IF EXISTS admin_metricas(date, integer, numeric);
                DROP FUNCTION IF EXISTS exigir_admin();
                DROP POLICY IF EXISTS rubro_panel ON rubro;
                DROP POLICY IF EXISTS enlace_tutor_panel ON enlace_tutor;
                DROP POLICY IF EXISTS consumo_mensual_panel ON consumo_mensual;
                DROP POLICY IF EXISTS ciclo_escolar_panel ON ciclo_escolar;
                DROP POLICY IF EXISTS docente_panel ON docente;
                DROP POLICY IF EXISTS tipo_rubro_cambio_admin ON tipo_rubro;
                DROP POLICY IF EXISTS tipo_rubro_alta_admin ON tipo_rubro;
                DROP POLICY IF EXISTS tipo_rubro_lectura ON tipo_rubro;
                ALTER TABLE tipo_rubro DISABLE ROW LEVEL SECURITY;
                REVOKE INSERT, UPDATE ON tipo_rubro FROM app_runtime;
                DROP TABLE IF EXISTS error_trabajo;
                DROP TABLE IF EXISTS evento_producto;
                """);

            migrationBuilder.DropTable(
                name: "auditoria_admin");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cuenta_rol",
                table: "cuenta");

            migrationBuilder.DropColumn(
                name: "cierre_iniciado_en",
                table: "periodo");

            migrationBuilder.DropColumn(
                name: "rol",
                table: "cuenta");
        }
    }
}
