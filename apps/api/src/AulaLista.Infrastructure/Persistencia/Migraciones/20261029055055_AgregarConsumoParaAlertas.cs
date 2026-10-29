using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarConsumoParaAlertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SPEC-09: el trabajo que alimenta la alerta de costo corre sin administrador. Esta función solo da el consumo del
            // mes por grupo (IDs y números, sin correos ni nombres) y solo app_runtime puede llamarla.
            migrationBuilder.Sql("""
                CREATE FUNCTION consumo_para_alertas(mes date)
                RETURNS TABLE (grupo_id uuid, tokens_entrada bigint, tokens_salida bigint, tokens_cache bigint, costo_whatsapp numeric)
                LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                    SELECT cm.grupo_id, cm.tokens_entrada, cm.tokens_salida, cm.tokens_cache_leidos, cm.costo_whatsapp::numeric
                    FROM consumo_mensual cm
                    WHERE cm.mes = $1
                $$;
                REVOKE ALL ON FUNCTION consumo_para_alertas(date) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION consumo_para_alertas(date) TO app_runtime;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS consumo_para_alertas(date);");
        }
    }
}
