using AulaLista.Application.Comun;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Aislamiento de datos (RNF-001, reglas/03-base-de-datos.md).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class RlsPruebas(BaseDeDatosFixture baseDeDatos)
{
    [Fact]
    public async Task RNF001_TodaTablaConDatosPersonales_TieneRlsForzada()
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT DISTINCT c.relname
            FROM information_schema.columns col
            JOIN pg_class c ON c.relname = col.table_name
            JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = col.table_schema
            WHERE col.table_schema = 'public'
              AND col.column_name IN ('docente_id', 'alumno_id', 'tutor_id')
              AND c.relkind = 'r'
              AND NOT (c.relrowsecurity AND c.relforcerowsecurity)
            """,
            conexion);

        var sinRls = new List<string>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            sinRls.Add(lector.GetString(0));
        }

        Assert.Empty(sinRls);
    }

    [Fact]
    public async Task RNF001_RolRuntime_NoPuedeSaltarseRlsNiCrearTablas()
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionRuntime);
        await conexion.OpenAsync();

        await using var rol = new NpgsqlCommand(
            "SELECT rolbypassrls, rolsuper FROM pg_roles WHERE rolname = current_user", conexion);
        await using (var lector = await rol.ExecuteReaderAsync())
        {
            Assert.True(await lector.ReadAsync());
            Assert.False(lector.GetBoolean(0));
            Assert.False(lector.GetBoolean(1));
        }

        await using var ddl = new NpgsqlCommand("CREATE TABLE intento (id int)", conexion);
        await Assert.ThrowsAsync<PostgresException>(() => ddl.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task RNF001_Transaccion_FijaVariablesDelDocente()
    {
        var docenteId = Guid.CreateVersion7();
        var opciones = new DbContextOptionsBuilder<AulaListaDbContext>()
            .UseNpgsql(baseDeDatos.ConexionRuntime)
            .AddInterceptors(new VariablesRlsInterceptor(new ContextoFijo(docenteId)))
            .Options;
        await using var db = new AulaListaDbContext(opciones);

        await using (await db.Database.BeginTransactionAsync())
        {
            var valor = await db.Database
                .SqlQueryRaw<string>("SELECT current_setting('app.docente_id', true) AS \"Value\"")
                .SingleAsync();
            Assert.Equal(docenteId.ToString(), valor);
        }

        var fuera = await db.Database
            .SqlQueryRaw<string>("SELECT coalesce(current_setting('app.docente_id', true), '') AS \"Value\"")
            .SingleAsync();
        Assert.Equal(string.Empty, fuera);
    }

    private sealed class ContextoFijo(Guid docenteId) : IContextoUsuario
    {
        public Guid? DocenteId => docenteId;

        public Guid? TutorId => null;

        public string? DireccionIp => null;

        public Guid? FamiliaSesionId => null;

        public void AsumirDocente(Guid docenteId) => throw new NotSupportedException();
    }
}
