using Microsoft.EntityFrameworkCore.Migrations;

namespace AulaLista.Infrastructure.Persistencia;

/// <summary>
/// Ayudas para que toda tabla con datos personales llegue con su política RLS en la misma migración
/// (reglas/03-base-de-datos.md). Las variables se fijan por transacción en <see cref="VariablesRlsInterceptor"/>.
/// </summary>
public static class MigracionesRls
{
    /// <summary>Activa y fuerza RLS en una tabla con columna <c>docente_id</c> y la limita al docente de la solicitud.</summary>
    public static void HabilitarRlsPorDocente(this MigrationBuilder migrationBuilder, string tabla)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"""
            ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
            CREATE POLICY {tabla}_por_docente ON {tabla}
                USING (docente_id = NULLIF(current_setting('app.docente_id', true), '')::uuid)
                WITH CHECK (docente_id = NULLIF(current_setting('app.docente_id', true), '')::uuid);
            """);
    }

    /// <summary>
    /// SPEC-06: política de solo lectura para el tutor de la solicitud, sumada a la del docente. Sin
    /// <c>app.tutor_id</c> no se evalúa la condición, para no cargar las consultas del docente.
    /// </summary>
    public static void HabilitarLecturaDelTutor(this MigrationBuilder migrationBuilder, string tabla, string condicion)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"""
            CREATE POLICY {tabla}_del_tutor ON {tabla} FOR SELECT
                USING (NULLIF(current_setting('app.tutor_id', true), '') IS NOT NULL AND ({condicion}));
            """);
    }
}
