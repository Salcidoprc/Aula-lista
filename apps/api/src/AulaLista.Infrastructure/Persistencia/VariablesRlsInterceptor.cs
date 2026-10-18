using System.Data.Common;
using AulaLista.Application.Comun;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AulaLista.Infrastructure.Persistencia;

/// <summary>
/// Al iniciar cada transacción fija <c>app.docente_id</c> y <c>app.tutor_id</c> con alcance local
/// (equivalente a <c>SET LOCAL</c>), para que las políticas de Row Level Security filtren por dueño.
/// Sin transacción las variables quedan vacías y las políticas no devuelven filas.
/// </summary>
internal sealed class VariablesRlsInterceptor(IContextoUsuario contexto) : DbTransactionInterceptor
{
    public override DbTransaction TransactionStarted(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result)
    {
        using var comando = CrearComando(connection, result);
        comando.ExecuteNonQuery();
        return result;
    }

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        await using var comando = CrearComando(connection, result);
        await comando.ExecuteNonQueryAsync(cancellationToken);
        return result;
    }

    /// <summary>Vuelve a fijar las variables en una transacción ya abierta (después de asumir a un usuario).</summary>
    public async Task FijarAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var comando = CrearComando(connection, transaction);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CrearComando(DbConnection connection, DbTransaction transaction)
    {
        var comando = connection.CreateCommand();
        comando.Transaction = transaction;
        comando.CommandText =
            "SELECT set_config('app.docente_id', @docente, true), set_config('app.tutor_id', @tutor, true)";
        AgregarParametro(comando, "docente", contexto.DocenteId);
        AgregarParametro(comando, "tutor", contexto.TutorId);
        return comando;
    }

    private static void AgregarParametro(DbCommand comando, string nombre, Guid? valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = valor?.ToString() ?? string.Empty;
        comando.Parameters.Add(parametro);
    }
}
