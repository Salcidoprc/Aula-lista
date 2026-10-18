using System.Text.RegularExpressions;
using AulaLista.Application.Comun;
using AulaLista.Application.Evaluacion;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Cierre;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;

namespace AulaLista.Application.Cierre;

/// <summary>
/// Una observación por redactar: lo que viaja a la IA y lo que se necesita para terminarla. El nombre visible
/// nunca sale del servidor; solo reemplaza al seudónimo si el modelo lo escribe.
/// </summary>
public sealed record EncargoObservacion(
    Guid ObservacionId, Guid GrupoId, EntradaObservacion Entrada, string Seudonimo, string NombreVisible, string Longitud);

/// <summary>El texto listo para guardar, o por qué no se pudo.</summary>
public sealed record TextoTerminado(string? Texto, string? Fallo);

/// <summary>
/// RF-041, SPEC-04 (Contrato con la IA): prepara las entradas seudonimizadas, termina el texto que devuelve la IA y
/// guarda el resultado. El trabajo de segundo plano reparte las llamadas con el límite de simultáneas por docente.
/// </summary>
public sealed partial class RedactarObservacionesTrabajo(
    IRepositorioGrupos grupos,
    IRepositorioEvaluacion evaluacion,
    IRepositorioCierre repositorio,
    MatrizCalificacionesCasoUso matriz,
    IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    /// <summary>Las observaciones pedidas que siguen en generación, con su entrada ya seudonimizada.</summary>
    public async Task<IReadOnlyList<EncargoObservacion>> PrepararAsync(
        Guid periodoId, IReadOnlyCollection<Guid> observacionIds, CancellationToken ct)
    {
        var observaciones = (await repositorio.ObservacionesPorIdAsync(observacionIds, ct))
            .Where(o => o.PeriodoId == periodoId && o.Estado == EstadoObservacion.Generando)
            .ToList();
        if (observaciones.Count == 0
            || await grupos.PeriodoAsync(periodoId, ct) is not { } periodo
            || await matriz.EjecutarAsync(periodoId, ct) is not { } datos)
        {
            return [];
        }

        var grupo = periodo.Grupo;
        var alumnos = await grupos.AlumnosAsync(grupo.Id, ct);
        var tutores = (await grupos.VinculosAsync(alumnos.Select(a => a.Id).ToList(), ct)).Select(v => v.Nombre).ToList();
        var notas = (await evaluacion.NotasDelPeriodoAsync(periodoId, ct)).ToLookup(n => n.AlumnoId);
        var estilo = await repositorio.EstiloAsync(grupo.DocenteId, ct) ?? EstiloObservaciones.PorDefecto(grupo.DocenteId, reloj.GetUtcNow());
        var resultados = datos.Resultados.ToDictionary(r => r.AlumnoId);
        var rubroDeActividad = datos.Rubros.SelectMany(r => r.Actividades.Select(a => (a.Id, Rubro: r.Rubro.Id))).ToDictionary(x => x.Id, x => x.Rubro);
        var noEntrego = datos.Celdas
            .Where(c => c.Marca == MarcaCalificacion.NoEntrego && rubroDeActividad.ContainsKey(c.ActividadId))
            .CountBy(c => (c.AlumnoId, rubroDeActividad[c.ActividadId]))
            .ToDictionary();

        var encargos = new List<EncargoObservacion>();
        foreach (var observacion in observaciones)
        {
            if (alumnos.FirstOrDefault(a => a.Id == observacion.AlumnoId) is not { } alumno)
            {
                continue;
            }

            var limpiador = new Seudonimizador(alumno, alumno.Seudonimo, alumnos, tutores);
            var resultado = resultados.GetValueOrDefault(alumno.Id);
            var promedios = resultado?.Rubros.ToDictionary(r => r.RubroId, r => r.Promedio) ?? [];
            var entrada = new EntradaObservacion(
                alumno.Seudonimo,
                alumno.Trato,
                grupo.Nivel,
                grupo.Grado,
                grupo.Materia,
                periodo.Unidad.Nombre,
                resultado?.Periodo,
                grupo.CalifAprobatoria,
                datos.Rubros.Select(r => new RubroParaIa(
                    limpiador.Limpiar(r.Rubro.Nombre), r.Rubro.Peso, promedios.GetValueOrDefault(r.Rubro.Id),
                    noEntrego.GetValueOrDefault((alumno.Id, r.Rubro.Id)))).ToList(),
                notas[alumno.Id].Select(n => new NotaParaIa(n.Fecha, n.Etiqueta, limpiador.Limpiar(n.Texto))).ToList(),
                new EstiloParaIa(estilo.Longitud, estilo.Persona, estilo.Ejemplos.Select(limpiador.Limpiar).ToList()),
                observacion.Instruccion is { } instruccion ? limpiador.Limpiar(instruccion) : null);
            encargos.Add(new EncargoObservacion(observacion.Id, grupo.Id, entrada, alumno.Seudonimo, PrimerNombre(alumno), estilo.Longitud));
        }

        return encargos;
    }

    /// <summary>
    /// SPEC-04, Salida: quita adornos, cambia el seudónimo por el nombre si el modelo lo escribió y valida la longitud.
    /// </summary>
    public static TextoTerminado Terminar(EncargoObservacion encargo, ObservacionRedactada redactada)
    {
        if (redactada.Fallo is { } fallo || string.IsNullOrWhiteSpace(redactada.Texto))
        {
            return new TextoTerminado(null, redactada.Fallo ?? FalloGeneracion.Error);
        }

        var texto = Adornos().Replace(redactada.Texto, string.Empty);
        texto = Espacios().Replace(texto, " ").Trim().Trim('"', '“', '”', '«', '»').Trim();
        var codigo = encargo.Seudonimo.Split(' ').Last();
        texto = Regex.Replace(texto, $@"(?:\b(?:el|la)\s+)?(?:\b(?:alumn[oa]|estudiante)\s+)?\b{Regex.Escape(codigo)}\b", encargo.NombreVisible, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        texto = texto.Length > 0 ? char.ToUpper(texto[0], System.Globalization.CultureInfo.GetCultureInfo("es-MX")) + texto[1..] : texto;

        if (texto.Length > Observacion.LongitudTexto || !LongitudObservacion.Aceptable(encargo.Longitud, LongitudObservacion.ContarPalabras(texto)))
        {
            return new TextoTerminado(null, FalloGeneracion.Longitud);
        }

        return new TextoTerminado(texto, null);
    }

    /// <summary>Guarda el borrador o la falla y registra la llamada (reglas/05-ia: sin el contenido).</summary>
    public async Task GuardarAsync(EncargoObservacion encargo, ObservacionRedactada redactada, TextoTerminado terminado, CancellationToken ct)
    {
        if (await repositorio.ObservacionAsync(encargo.ObservacionId, ct) is not { } observacion)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        if (terminado.Texto is { } texto)
        {
            observacion.RegistrarBorrador(texto, redactada.Modelo, redactada.VersionPrompt, redactada.TokensEntrada, redactada.TokensSalida, ahora);
        }
        else
        {
            observacion.RegistrarFallo(terminado.Fallo ?? FalloGeneracion.Error, redactada.Modelo, redactada.VersionPrompt, redactada.TokensEntrada, redactada.TokensSalida, ahora);
        }

        repositorio.Agregar(new LlamadaIa(
            observacion.DocenteId, encargo.GrupoId, redactada.Modelo, redactada.VersionPrompt, redactada.TokensEntrada,
            redactada.TokensSalida, redactada.TokensCacheLeidos, redactada.DuracionMs, terminado.Fallo ?? "ok", ahora));
        await unidad.GuardarAsync(ct);
    }

    /// <summary>Si el trabajo se cae antes de llamar a la IA, las observaciones no se quedan en generación.</summary>
    public async Task MarcarFallidasAsync(IReadOnlyCollection<Guid> observacionIds, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        foreach (var observacion in await repositorio.ObservacionesPorIdAsync(observacionIds, ct))
        {
            observacion.RegistrarFallo(FalloGeneracion.Error, null, null, 0, 0, ahora);
        }

        await unidad.GuardarAsync(ct);
    }

    private static string PrimerNombre(Alumno alumno) =>
        alumno.Nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? alumno.Nombres;

    [GeneratedRegex(@"[*_#`]+")]
    private static partial Regex Adornos();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
