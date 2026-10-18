using AulaLista.Application.Cierre;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Evaluacion;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Boletas;
using AulaLista.Domain.Cierre;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Operacion;

namespace AulaLista.Application.Boletas;

/// <summary>RF-044 en boletas: cuántos alumnos hay en cada estado y cuántas boletas no coinciden con los datos actuales.</summary>
public sealed record ConteosBoletasDto(int SinAprobar, int SinBoleta, int Preparando, int Lista, int Publicada, int Desactualizadas);

/// <summary>
/// La boleta vigente de un alumno activo. <see cref="Desactualizada"/>: sus datos de hoy ya no son los de la foto (sin
/// publicar se vuelve a generar; publicada se corrige con una versión nueva). <see cref="TutoresWhatsapp"/> y
/// <see cref="TutoresCorreo"/> alimentan el resumen previo a publicar (RN-054).
/// </summary>
public sealed record BoletaAlumnoDto(
    Guid AlumnoId,
    int NumeroLista,
    string Nombres,
    string Apellidos,
    decimal? Calificacion,
    string Estado,
    Guid? ObservacionId,
    Guid? BoletaId,
    int? Version,
    DateTimeOffset? PublicadaEn,
    bool Desactualizada,
    bool FalloPdf,
    int TutoresWhatsapp,
    int TutoresCorreo);

public sealed record BoletasDto(
    Guid PeriodoId,
    Guid GrupoId,
    string Estado,
    bool SoloLectura,
    int DecimalesFinal,
    ConteosBoletasDto Conteos,
    IReadOnlyList<BoletaAlumnoDto> Alumnos);

/// <summary>Para quiénes: todos o solo los indicados (RF-052: por grupo o por alumno).</summary>
public sealed record SeleccionAlumnos(IReadOnlyList<Guid>? AlumnoIds);

/// <summary>Un alumno activo con lo que diría su boleta hoy; <see cref="Datos"/> es nulo sin observación aprobada (RN-050).</summary>
public sealed record AlumnoArmado(Alumno Alumno, Observacion? Observacion, IReadOnlyList<RubroBoleta> Rubros, decimal? Calificacion, DatosBoleta? Datos);

public sealed record PeriodoArmado(PeriodoCompleto Periodo, IReadOnlyList<RubroBoleta> Rubros, string ZonaHoraria, IReadOnlyList<AlumnoArmado> Alumnos);

/// <summary>
/// Arma lo que dice la boleta de cada alumno con el mismo cálculo de la matriz (RN-033 a RN-035), para que boletas
/// y Excel coincidan siempre.
/// </summary>
public sealed class ArmadoBoletas(
    IRepositorioGrupos grupos, IRepositorioCierre cierre, IRepositorioDocentes docentes, MatrizCalificacionesCasoUso matriz)
{
    public async Task<PeriodoArmado?> ArmarAsync(Guid periodoId, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo || await matriz.EjecutarAsync(periodoId, ct) is not { } datos)
        {
            return null;
        }

        var grupo = periodo.Grupo;
        var ciclo = await grupos.CicloAsync(grupo.CicloEscolarId, ct);
        var docente = await docentes.PorIdAsync(grupo.DocenteId, ct);
        var alumnos = (await grupos.AlumnosAsync(grupo.Id, ct)).Where(a => a.EstaActivo).OrderBy(a => a.NumeroLista).ThenBy(a => a.Apellidos).ToList();
        var observaciones = (await cierre.ObservacionesAsync(periodoId, ct)).ToDictionary(o => o.AlumnoId);
        var resultados = datos.Resultados.ToDictionary(r => r.AlumnoId);
        var rubros = datos.Rubros.Select(r => new RubroBoleta(r.Rubro.Nombre, r.Rubro.Peso, null)).ToList();

        var armados = alumnos.Select(alumno =>
        {
            var resultado = resultados.GetValueOrDefault(alumno.Id);
            var promedios = resultado?.Rubros.ToDictionary(r => r.RubroId, r => r.Promedio) ?? [];
            var suyos = datos.Rubros
                .Select(r => new RubroBoleta(r.Rubro.Nombre, r.Rubro.Peso, promedios.GetValueOrDefault(r.Rubro.Id)))
                .ToList();
            var observacion = observaciones.GetValueOrDefault(alumno.Id);
            var aprobada = observacion is { EstaAprobada: true, TextoFinal: { } texto } ? texto : null;
            var foto = aprobada is null
                ? null
                : new DatosBoleta(
                    docente?.EscuelaNombre, docente?.EscuelaCct, ciclo?.Nombre ?? string.Empty, periodo.Unidad.Nombre,
                    $"{alumno.Apellidos} {alumno.Nombres}".Trim(), alumno.NumeroLista, grupo.Nivel, grupo.Grado, grupo.Nombre,
                    grupo.Materia, docente?.Nombre ?? string.Empty, suyos, resultado?.Final, grupo.DecimalesFinal, aprobada);
            return new AlumnoArmado(alumno, observacion, suyos, resultado?.Final, foto);
        }).ToList();

        return new PeriodoArmado(periodo, rubros, docente?.ZonaHoraria ?? Domain.Cuentas.Docente.ZonaHorariaPorDefecto, armados);
    }
}

/// <summary>Qué permite la pantalla de boletas según el periodo y su grupo.</summary>
internal static class ReglasBoletas
{
    /// <summary>Generar, publicar y corregir: con el periodo en cierre y el grupo sin archivar (RN-048, RN-038).</summary>
    public static ErrorNegocio? PuedeEmitir(PeriodoCompleto periodo) => periodo.Periodo.Estado switch
    {
        _ when periodo.Grupo.EstaArchivado => ErroresGrupos.GrupoArchivado,
        EstadoPeriodo.Abierto => ErroresCierre.PeriodoSinCierre,
        EstadoPeriodo.Cerrado => Domain.Evaluacion.ErroresEvaluacion.PeriodoCerrado,
        _ => null,
    };

    /// <summary>La versión vigente de cada alumno.</summary>
    public static Dictionary<Guid, Boleta> Vigentes(IEnumerable<Boleta> boletas) =>
        boletas.Where(b => b.EsVigente).GroupBy(b => b.AlumnoId).ToDictionary(g => g.Key, g => g.MaxBy(b => b.Version)!);

    public static bool Desactualizada(Boleta boleta, AlumnoArmado alumno) =>
        alumno.Datos is not { } datos || !datos.MismoContenido(boleta.Datos);

    public static DateOnly FechaLocal(DateTimeOffset fecha, string zona)
    {
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(zona, out var encontrada) ? encontrada : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fecha, tz).DateTime);
    }

    public static BoletaParaPdf ParaPdf(Boleta boleta, string zona) => new(
        boleta.Datos, boleta.Version, boleta.Codigo, FechaLocal(boleta.EmitidaEn, zona),
        boleta.EsCorreccion ? FechaLocal(boleta.EmitidaEn, zona) : null);
}

/// <summary>El estado de las boletas del periodo, alumno por alumno (pantalla "Boletas y envío").</summary>
public sealed class ConsultaBoletas(ArmadoBoletas armado, IRepositorioBoletas repositorio, IRepositorioGrupos grupos)
{
    public async Task<BoletasDto?> LeerAsync(Guid periodoId, CancellationToken ct)
    {
        if (await armado.ArmarAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        var vigentes = ReglasBoletas.Vigentes(await repositorio.BoletasAsync(periodoId, ct));
        var vinculos = (await grupos.VinculosAsync(periodo.Alumnos.Select(a => a.Alumno.Id).ToList(), ct)).ToLookup(v => v.AlumnoId);
        var alumnos = periodo.Alumnos.Select(a =>
        {
            var boleta = vigentes.GetValueOrDefault(a.Alumno.Id);
            var estado = boleta switch
            {
                null => a.Datos is null ? EstadoBoleta.SinAprobar : EstadoBoleta.SinBoleta,
                { EstaPublicada: true } => EstadoBoleta.Publicada,

                // Sin publicar y con la observación reabierta: primero se aprueba de nuevo.
                _ when a.Datos is null => EstadoBoleta.SinAprobar,
                { PdfRuta: null, FalloPdf: false } => EstadoBoleta.Preparando,

                // El PDF no salió tras los reintentos: vuelve a "sin boleta" con la marca de falla y generar lo reintenta.
                { PdfRuta: null } => EstadoBoleta.SinBoleta,
                _ => EstadoBoleta.Lista,
            };
            var tutores = vinculos[a.Alumno.Id].ToList();
            return new BoletaAlumnoDto(
                a.Alumno.Id, a.Alumno.NumeroLista, a.Alumno.Nombres, a.Alumno.Apellidos, a.Calificacion, estado, a.Observacion?.Id,
                boleta?.Id, boleta?.Version, boleta?.PublicadaEn, boleta is not null && ReglasBoletas.Desactualizada(boleta, a),
                boleta?.FalloPdf ?? false, tutores.Count, tutores.Count(t => t.Correo is not null));
        }).ToList();

        var cuenta = alumnos.CountBy(a => a.Estado).ToDictionary();
        var p = periodo.Periodo;
        return new BoletasDto(
            periodoId,
            p.Grupo.Id,
            p.Periodo.Estado,
            ReglasBoletas.PuedeEmitir(p) is not null,
            p.Grupo.DecimalesFinal,
            new ConteosBoletasDto(
                cuenta.GetValueOrDefault(EstadoBoleta.SinAprobar), cuenta.GetValueOrDefault(EstadoBoleta.SinBoleta),
                cuenta.GetValueOrDefault(EstadoBoleta.Preparando), cuenta.GetValueOrDefault(EstadoBoleta.Lista),
                cuenta.GetValueOrDefault(EstadoBoleta.Publicada), alumnos.Count(a => a.Desactualizada)),
            alumnos);
    }
}

public sealed class ObtenerBoletasCasoUso(ConsultaBoletas consulta)
{
    public Task<BoletasDto?> EjecutarAsync(Guid periodoId, CancellationToken ct) => consulta.LeerAsync(periodoId, ct);
}

/// <summary>
/// RF-050, RN-050: toma la foto de cada alumno con observación aprobada y encola su PDF. Una boleta sin publicar se
/// actualiza si sus datos cambiaron o si su PDF falló; una publicada no se toca (se corrige, RN-052).
/// </summary>
public sealed class GenerarBoletasCasoUso(
    ArmadoBoletas armado, IRepositorioBoletas repositorio, ConsultaBoletas consulta, IColaTrabajos cola, IContextoUsuario usuario,
    IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<BoletasDto>?> EjecutarAsync(Guid periodoId, SeleccionAlumnos comando, CancellationToken ct)
    {
        if (await armado.ArmarAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasBoletas.PuedeEmitir(periodo.Periodo) is { } bloqueo)
        {
            return Resultado.Fallo<BoletasDto>(bloqueo);
        }

        var ahora = reloj.GetUtcNow();
        var pedidos = comando.AlumnoIds?.ToHashSet();
        var vigentes = ReglasBoletas.Vigentes(await repositorio.BoletasAsync(periodoId, ct));
        var porPreparar = new List<Guid>();
        foreach (var alumno in periodo.Alumnos.Where(a => a.Datos is not null && (pedidos is null || pedidos.Contains(a.Alumno.Id))))
        {
            switch (vigentes.GetValueOrDefault(alumno.Alumno.Id))
            {
                case null:
                    var nueva = Boleta.Primera(alumno.Alumno.Id, alumno.Alumno.DocenteId, periodoId, alumno.Datos!, ahora);
                    repositorio.Agregar(nueva);
                    porPreparar.Add(nueva.Id);
                    break;
                case { EstaPublicada: false } boleta when boleta.FalloPdf || ReglasBoletas.Desactualizada(boleta, alumno):
                    boleta.Actualizar(alumno.Datos!, ahora);
                    porPreparar.Add(boleta.Id);
                    break;
            }
        }

        if (porPreparar.Count > 0)
        {
            await unidad.GuardarAsync(ct);
            cola.EncolarBoletas(usuario.DocenteId ?? throw new InvalidOperationException("Generar requiere un docente."), porPreparar);
        }

        return Resultado.Exito((await consulta.LeerAsync(periodoId, ct))!);
    }
}

/// <summary>
/// RF-052, RN-053: publica las boletas listas (todas o las elegidas), marca sus observaciones como publicadas y lo deja
/// en auditoría (RNF-005). Los avisos a tutores (RN-054) llegan con SPEC-07 en H5. RN-048: con todas publicadas, la
/// unidad se cierra.
/// </summary>
public sealed class PublicarBoletasCasoUso(
    ArmadoBoletas armado, IRepositorioBoletas repositorio, ConsultaBoletas consulta, IAuditoria auditoria, IContextoUsuario usuario,
    IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<BoletasDto>?> EjecutarAsync(Guid periodoId, SeleccionAlumnos comando, CancellationToken ct)
    {
        if (await armado.ArmarAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasBoletas.PuedeEmitir(periodo.Periodo) is { } bloqueo)
        {
            return Resultado.Fallo<BoletasDto>(bloqueo);
        }

        var pedidos = comando.AlumnoIds?.ToHashSet();
        var vigentes = ReglasBoletas.Vigentes(await repositorio.BoletasAsync(periodoId, ct));
        var porPublicar = periodo.Alumnos
            .Where(a => pedidos is null || pedidos.Contains(a.Alumno.Id))
            .Select(a => (Alumno: a, Boleta: vigentes.GetValueOrDefault(a.Alumno.Id)))
            .Where(x => x.Boleta is { EstaPublicada: false })
            .ToList();
        if (porPublicar.Count == 0)
        {
            return Resultado.Fallo<BoletasDto>(ErroresBoletas.SinBoletasPorPublicar);
        }

        if (porPublicar.Any(x => ReglasBoletas.Desactualizada(x.Boleta!, x.Alumno)))
        {
            return Resultado.Fallo<BoletasDto>(ErroresBoletas.BoletasDesactualizadas);
        }

        var ahora = reloj.GetUtcNow();
        foreach (var (alumno, boleta) in porPublicar)
        {
            if (boleta!.Publicar(ahora) is { EsFallo: true } fallo)
            {
                return Resultado.Fallo<BoletasDto>(fallo.Error);
            }

            alumno.Observacion!.MarcarPublicada(ahora);
        }

        var docente = usuario.DocenteId;
        auditoria.Registrar(AccionAuditoria.BoletasPublicadas, "periodo", periodoId, docente, porPublicar.Count);
        CerrarSiTodasPublicadas(periodo, vigentes, ahora, auditoria, docente);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito((await consulta.LeerAsync(periodoId, ct))!);
    }

    /// <summary>RN-048: cada alumno activo tiene su boleta vigente publicada y al día.</summary>
    internal static void CerrarSiTodasPublicadas(
        PeriodoArmado periodo, IReadOnlyDictionary<Guid, Boleta> vigentes, DateTimeOffset ahora, IAuditoria auditoria, Guid? docente)
    {
        var todas = periodo.Alumnos.Count > 0 && periodo.Alumnos.All(a =>
            vigentes.GetValueOrDefault(a.Alumno.Id) is { EstaPublicada: true } b && !ReglasBoletas.Desactualizada(b, a));
        if (todas)
        {
            periodo.Periodo.Periodo.Cerrar(ahora);
            auditoria.Registrar(AccionAuditoria.PeriodoCerrado, "periodo", periodo.Periodo.Periodo.Id, docente);
        }
    }
}

/// <summary>
/// RF-054, RN-052: emite la versión corregida de una boleta publicada con los datos de hoy. La anterior queda
/// reemplazada y se conserva; el tutor solo verá la nueva, con la leyenda de corrección.
/// </summary>
public sealed class CorregirBoletaCasoUso(
    ArmadoBoletas armado, IRepositorioBoletas repositorio, ConsultaBoletas consulta, IColaTrabajos cola, IAuditoria auditoria,
    IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<BoletasDto>?> EjecutarAsync(Guid boletaId, CancellationToken ct)
    {
        if (await repositorio.BoletaAsync(boletaId, ct) is not { } boleta || await armado.ArmarAsync(boleta.PeriodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasBoletas.PuedeEmitir(periodo.Periodo) is { } bloqueo)
        {
            return Resultado.Fallo<BoletasDto>(bloqueo);
        }

        if (periodo.Alumnos.SingleOrDefault(a => a.Alumno.Id == boleta.AlumnoId) is not { } alumno)
        {
            return null;
        }

        if (alumno.Datos is null)
        {
            return Resultado.Fallo<BoletasDto>(ErroresBoletas.ObservacionSinAprobar);
        }

        var ahora = reloj.GetUtcNow();
        var corregida = boleta.Corregir(alumno.Datos, ahora);
        if (corregida.EsFallo)
        {
            return Resultado.Fallo<BoletasDto>(corregida.Error);
        }

        repositorio.Agregar(corregida.Valor);
        alumno.Observacion!.MarcarPublicada(ahora);
        var docente = usuario.DocenteId;
        auditoria.Registrar(AccionAuditoria.BoletaCorregida, "boleta", corregida.Valor.Id, docente, 1);
        var vigentes = ReglasBoletas.Vigentes(await repositorio.BoletasAsync(boleta.PeriodoId, ct));
        vigentes[boleta.AlumnoId] = corregida.Valor;
        PublicarBoletasCasoUso.CerrarSiTodasPublicadas(periodo, vigentes, ahora, auditoria, docente);
        await unidad.GuardarAsync(ct);
        cola.EncolarBoletas(docente ?? throw new InvalidOperationException("Corregir requiere un docente."), [corregida.Valor.Id]);
        return Resultado.Exito((await consulta.LeerAsync(boleta.PeriodoId, ct))!);
    }
}

public sealed record ArchivoBoleta(byte[]? Contenido, string? Ruta, string Nombre);

/// <summary>El PDF de una boleta, de cualquier versión (docente dueño; el acceso del tutor llega con SPEC-06).</summary>
public sealed class ObtenerPdfBoletaCasoUso(IRepositorioBoletas repositorio)
{
    public async Task<Resultado<ArchivoBoleta>?> EjecutarAsync(Guid boletaId, CancellationToken ct)
    {
        if (await repositorio.BoletaAsync(boletaId, ct) is not { } boleta)
        {
            return null;
        }

        return boleta.PdfRuta is { } ruta
            ? Resultado.Exito(new ArchivoBoleta(null, ruta, $"boleta-{boleta.Datos.NumeroLista:00}-v{boleta.Version}.pdf"))
            : Resultado.Fallo<ArchivoBoleta>(ErroresBoletas.BoletaPreparando);
    }
}

/// <summary>RF-051: un solo PDF con la boleta vigente de cada alumno que ya la tiene, en el orden de la lista.</summary>
public sealed class PdfDelGrupoCasoUso(ArmadoBoletas armado, IRepositorioBoletas repositorio, IGeneradorBoletas generador)
{
    public async Task<Resultado<ArchivoBoleta>?> EjecutarAsync(Guid periodoId, CancellationToken ct)
    {
        if (await armado.ArmarAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        var vigentes = ReglasBoletas.Vigentes(await repositorio.BoletasAsync(periodoId, ct));
        var boletas = periodo.Alumnos
            .Select(a => vigentes.GetValueOrDefault(a.Alumno.Id))
            .OfType<Boleta>()
            .Select(b => ReglasBoletas.ParaPdf(b, periodo.ZonaHoraria))
            .ToList();
        if (boletas.Count == 0)
        {
            return Resultado.Fallo<ArchivoBoleta>(ErroresBoletas.SinBoletas);
        }

        var grupo = periodo.Periodo.Grupo;
        return Resultado.Exito(new ArchivoBoleta(generador.Pdf(boletas), null, $"boletas-{grupo.Grado}{grupo.Nombre}.pdf".Replace(' ', '-')));
    }
}

/// <summary>RF-055, RN-056: Excel con número, nombre, promedio por rubro, final y observación aprobada, en el orden de la lista.</summary>
public sealed class ExportarPeriodoCasoUso(ArmadoBoletas armado, IGeneradorBoletas generador, IAuditoria auditoria, IContextoUsuario usuario, IUnidadDeTrabajo unidad)
{
    public async Task<ArchivoBoleta?> EjecutarAsync(Guid periodoId, CancellationToken ct)
    {
        if (await armado.ArmarAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        var p = periodo.Periodo;
        var filas = periodo.Alumnos.Select(a => new FilaExportacion(
            a.Alumno.NumeroLista, $"{a.Alumno.Apellidos} {a.Alumno.Nombres}".Trim(), a.Rubros.Select(r => r.Promedio).ToList(), a.Calificacion,
            a.Observacion is { EstaAprobada: true } o ? o.TextoFinal : null)).ToList();
        var titulo = $"{p.Grupo.Grado}° {p.Grupo.Nombre}{(p.Grupo.Materia is { } m ? $" {m}" : string.Empty)}, {p.Unidad.Nombre}";
        var contenido = generador.Excel(new ExportacionPeriodo(titulo, periodo.Rubros, p.Grupo.DecimalesFinal, filas));
        auditoria.Registrar(AccionAuditoria.CalificacionesExportadas, "periodo", periodoId, usuario.DocenteId, filas.Count);
        await unidad.GuardarAsync(ct);
        return new ArchivoBoleta(contenido, null, $"calificaciones-{p.Grupo.Grado}{p.Grupo.Nombre}.xlsx".Replace(' ', '-'));
    }
}

/// <summary>RF-050: dibuja y guarda el PDF de cada boleta (job en segundo plano, 02-arquitectura).</summary>
public sealed class PrepararBoletasTrabajo(
    IRepositorioBoletas repositorio, IRepositorioDocentes docentes, IGeneradorBoletas generador, IAlmacenArchivos almacen,
    IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public const string Carpeta = "boletas";

    public async Task EjecutarAsync(IReadOnlyCollection<Guid> boletaIds, CancellationToken ct)
    {
        var zona = (usuario.DocenteId is { } id ? await docentes.PorIdAsync(id, ct) : null)?.ZonaHoraria ?? Domain.Cuentas.Docente.ZonaHorariaPorDefecto;
        foreach (var boleta in (await repositorio.BoletasPorIdAsync(boletaIds, ct)).Where(b => b.PdfRuta is null))
        {
            var pdf = generador.Pdf([ReglasBoletas.ParaPdf(boleta, zona)]);
            var ruta = await almacen.GuardarAsync(
                Path.Combine(Carpeta, boleta.PeriodoId.ToString()), $"{boleta.Id}-{boleta.UpdatedAt.ToUnixTimeMilliseconds()}.pdf", pdf, ct);
            boleta.RegistrarPdf(ruta, reloj.GetUtcNow());
            await unidad.GuardarAsync(ct);
        }
    }

    /// <summary>Tras el último reintento, las que siguen sin PDF dicen "No se pudo preparar".</summary>
    public async Task MarcarFallidasAsync(IReadOnlyCollection<Guid> boletaIds, CancellationToken ct)
    {
        foreach (var boleta in (await repositorio.BoletasPorIdAsync(boletaIds, ct)).Where(b => b.PdfRuta is null))
        {
            boleta.RegistrarFalloPdf(reloj.GetUtcNow());
        }

        await unidad.GuardarAsync(ct);
    }
}
