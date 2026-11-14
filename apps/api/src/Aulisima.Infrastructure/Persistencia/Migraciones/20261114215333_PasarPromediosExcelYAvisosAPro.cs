using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aulisima.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PasarPromediosExcelYAvisosAPro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RN-097: promedios, Excel y avisos pasan a Pro. Todo producto que ya existe los gana, para que quien paga o
            // está en prueba no pierda nada; el administrador puede quitarlos de un borrador.
            migrationBuilder.Sql("""
                ALTER TABLE producto DISABLE ROW LEVEL SECURITY;
                UPDATE producto
                SET funciones = ARRAY['promedios', 'exportar_excel', 'avisos']
                        || ARRAY(SELECT f FROM unnest(funciones) AS f WHERE f NOT IN ('promedios', 'exportar_excel', 'avisos')),
                    updated_at = now();
                ALTER TABLE producto ENABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE producto DISABLE ROW LEVEL SECURITY;
                UPDATE producto
                SET funciones = ARRAY(SELECT f FROM unnest(funciones) AS f WHERE f NOT IN ('promedios', 'exportar_excel', 'avisos'));
                ALTER TABLE producto ENABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
