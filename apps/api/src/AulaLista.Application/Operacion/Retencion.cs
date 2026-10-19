using AulaLista.Application.Boletas;
using AulaLista.Application.Comun;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Operacion;

/// <summary>
/// RNF-009: borra los grupos archivados cuyo ciclo terminó hace más de <see cref="OpcionesOperacion.AniosGruposArchivados"/>
/// años, con sus alumnos, calificaciones y boletas, y después los PDF de sus boletas.
/// </summary>
public sealed class RetencionGruposTrabajo(
    IRetencionGrupos retencion, IAlmacenArchivos almacen, IOptions<OpcionesOperacion> opciones, TimeProvider reloj)
{
    public async Task<int> EjecutarAsync(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var limite = Calendario.Hoy(ahora).AddYears(-opciones.Value.AniosGruposArchivados);
        var periodos = await retencion.EliminarArchivadosAsync(limite, ct);
        foreach (var periodo in periodos)
        {
            await almacen.BorrarAnterioresAsync(Path.Combine(PrepararBoletasTrabajo.Carpeta, periodo.ToString()), ahora.AddYears(1), ct);
        }

        return periodos.Count;
    }
}
