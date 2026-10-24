using System.Globalization;
using System.Text;
using AulaLista.Application.Cierre;
using AulaLista.Application.Comun;
using AulaLista.Domain.Cierre;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Grupos;

/// <summary>Columnas que la vista previa sabe llenar. Las de tutor llevan el número de tutor: <c>tutor_telefono_2</c>.</summary>
public static class CampoImportacion
{
    public const string Ignorar = "ignorar";
    public const string NombreCompleto = "nombre_completo";
    public const string Nombres = "nombres";
    public const string Apellidos = "apellidos";
    public const string ApellidoPaterno = "apellido_paterno";
    public const string ApellidoMaterno = "apellido_materno";
    public const string TutorNombre = "tutor_nombre";
    public const string TutorTelefono = "tutor_telefono";
    public const string TutorCorreo = "tutor_correo";
    public const string TutorParentesco = "tutor_parentesco";
}

/// <summary>Errores por fila al confirmar; la web los muestra en la vista previa.</summary>
public static class ErrorFilaImportacion
{
    public const string NombreVacio = "nombre_vacio";
    public const string NombreLargo = "nombre_largo";
    public const string TelefonoInvalido = "telefono_invalido";
    public const string TutorIncompleto = "tutor_incompleto";
    public const string CorreoInvalido = "correo_invalido";
    public const string DemasiadosTutores = "demasiados_tutores";

    public static readonly IReadOnlyList<string> Todos = [NombreVacio, NombreLargo, TelefonoInvalido, TutorIncompleto, CorreoInvalido, DemasiadosTutores];
}

public sealed record VistaPreviaImportacion(
    Guid Id, string Origen, IReadOnlyList<string> Encabezados, IReadOnlyList<IReadOnlyList<string>> Filas, IReadOnlyList<string> Mapeo, int Disponibles);

/// <summary>Archivo subido (RF-013), texto pegado (RF-014) o foto de la lista (RF-015).</summary>
public sealed record FuenteImportacion(Stream? Archivo, string? NombreArchivo, long Tamano, string? Texto, ImagenLista? Foto = null);

/// <summary>
/// Lee el archivo, el texto o la foto y devuelve la tabla cruda con el mapeo propuesto (ADR-0014). No guarda nombres:
/// la vista previa vive en la web hasta confirmar. La foto se lee en memoria y se descarta (reglas/04-seguridad).
/// </summary>
public sealed class CrearImportacionCasoUso(
    IRepositorioGrupos repositorio, IHojasDeCalculo hojas, ILectorListas lector, IRepositorioCierre llamadas,
    IOptions<OpcionesIa> opciones, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public const long TamanoMaximo = 2 * 1024 * 1024;
    public const int TamanoMaximoFoto = 5 * 1024 * 1024;
    public const int LongitudTextoMaxima = 20_000;
    private const int FilasMaximas = 500;

    public async Task<Resultado<VistaPreviaImportacion>?> EjecutarAsync(Guid grupoId, FuenteImportacion fuente, CancellationToken ct)
    {
        if (await repositorio.GrupoAsync(grupoId, ct) is not { } grupo)
        {
            return null;
        }

        if (grupo.EstaArchivado)
        {
            return Resultado.Fallo<VistaPreviaImportacion>(ErroresGrupos.GrupoArchivado);
        }

        var leida = fuente.Foto is { } foto ? await LeerFotoAsync(grupo, foto, ct)
            : fuente.Archivo is null ? LeerTexto(fuente.Texto)
            : LeerArchivo(fuente);
        if (leida.EsFallo)
        {
            return Resultado.Fallo<VistaPreviaImportacion>(leida.Error);
        }

        var (origen, tabla) = leida.Valor;
        var importacion = new Importacion(grupo, origen, reloj.GetUtcNow());
        repositorio.Agregar(importacion);
        await unidad.GuardarAsync(ct);

        var activos = (await repositorio.AlumnosAsync(grupoId, ct)).Count(a => a.EstaActivo);
        var filas = tabla.Filas.Where(f => f.Any(c => !string.IsNullOrWhiteSpace(c))).Take(FilasMaximas).ToList();
        return Resultado.Exito(new VistaPreviaImportacion(
            importacion.Id, origen, tabla.Encabezados, filas, MapeoImportacion.Proponer(tabla.Encabezados),
            Grupo.MaximoAlumnosActivos - activos));
    }

    private Resultado<(string, TablaCruda)> LeerArchivo(FuenteImportacion fuente)
    {
        var extension = Path.GetExtension(fuente.NombreArchivo ?? string.Empty).ToLowerInvariant();
        var origen = extension switch
        {
            ".xlsx" => OrigenImportacion.Excel,
            ".csv" => OrigenImportacion.Csv,
            _ => null,
        };
        if (origen is null || fuente.Tamano is <= 0 or > TamanoMaximo)
        {
            return Resultado.Fallo<(string, TablaCruda)>(ErroresGrupos.ArchivoInvalido);
        }

        return hojas.Leer(fuente.Archivo!, origen) is { Encabezados.Count: > 0 } tabla
            ? Resultado.Exito((origen, tabla))
            : Resultado.Fallo<(string, TablaCruda)>(ErroresGrupos.ArchivoIlegible);
    }

    /// <summary>Un alumno por línea en una sola columna; la web separa nombres y apellidos según el orden elegido.</summary>
    private static Resultado<(string, TablaCruda)> LeerTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > LongitudTextoMaxima)
        {
            return Resultado.Fallo<(string, TablaCruda)>(ErroresGrupos.ArchivoIlegible);
        }

        return Resultado.Exito((OrigenImportacion.Texto, UnaColumna(texto.Split('\n'))));
    }

    /// <summary>
    /// RF-015: la IA lee los nombres de la foto y el resultado llega como si se hubiera pegado la lista. La llamada
    /// cuenta para el límite por hora (RNF-004) y para el costo del grupo (RNF-022), aunque no se lea nada; en ese
    /// caso la vista previa no trae filas.
    /// </summary>
    private async Task<Resultado<(string, TablaCruda)>> LeerFotoAsync(Grupo grupo, ImagenLista foto, CancellationToken ct)
    {
        if (foto.Datos.Length is 0 or > TamanoMaximoFoto || TipoDeFoto(foto.Datos.Span) != foto.TipoMedio)
        {
            return Resultado.Fallo<(string, TablaCruda)>(ErroresGrupos.FotoInvalida);
        }

        var ahora = reloj.GetUtcNow();
        if (await llamadas.SolicitudesIaDesdeAsync(ahora.AddHours(-1), ahora, ct) >= opciones.Value.SolicitudesPorHora)
        {
            return Resultado.Fallo<(string, TablaCruda)>(ErroresGrupos.LimiteLecturas);
        }

        var lista = await lector.LeerAsync(foto, ct);
        llamadas.Agregar(new LlamadaIa(
            grupo.DocenteId, grupo.Id, lista.Modelo, lista.VersionPrompt, lista.TokensEntrada, lista.TokensSalida, 0,
            lista.DuracionMs, lista.Fallo ?? "ok", reloj.GetUtcNow()));

        // Si no se leyó nada la vista previa llega vacía y la web ofrece pegar la lista: una respuesta de error
        // revertiría la transacción de la solicitud y la llamada, que sí costó, dejaría de contar.
        return Resultado.Exito((OrigenImportacion.Foto, lista.Fallo is null ? UnaColumna(lista.Nombres) : UnaColumna([])));
    }

    /// <summary>JPG o PNG por sus primeros bytes, no por lo que diga el navegador.</summary>
    public static string? TipoDeFoto(ReadOnlySpan<byte> datos) =>
        datos is [0xFF, 0xD8, 0xFF, ..] ? ImagenLista.Jpeg
        : datos is [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] ? ImagenLista.Png
        : null;

    private static TablaCruda UnaColumna(IEnumerable<string> lineas) => new(
        ["Alumno"],
        lineas.Select(l => l.Trim().TrimStart('-', '*', '•').Trim())
            .Select(QuitarNumeracion)
            .Where(l => l.Length > 0)
            .Select(l => (IReadOnlyList<string>)[l])
            .ToList());

    /// <summary>Quita "1.", "2)" o "3 -" al inicio, como en las listas copiadas de Word.</summary>
    private static string QuitarNumeracion(string linea)
    {
        var i = 0;
        while (i < linea.Length && char.IsAsciiDigit(linea[i]))
        {
            i++;
        }

        if (i == 0 || i == linea.Length)
        {
            return linea;
        }

        var resto = linea[i..].TrimStart();
        return resto.Length > 0 && resto[0] is '.' or ')' or '-' ? resto[1..].Trim() : linea;
    }
}

/// <summary>Propone qué campo es cada columna por su encabezado (RF-013, paso 2).</summary>
public static class MapeoImportacion
{
    public static IReadOnlyList<string> Proponer(IReadOnlyList<string> encabezados)
    {
        var normalizados = encabezados.Select(Normalizar).ToList();
        var hayApellidos = normalizados.Any(e => e.Contains("apellido", StringComparison.Ordinal));
        var usados = new Dictionary<string, int>(StringComparer.Ordinal);
        var mapeo = new List<string>();
        foreach (var encabezado in normalizados)
        {
            var campo = Campo(encabezado, hayApellidos);
            if (campo.StartsWith("tutor_", StringComparison.Ordinal))
            {
                var numero = encabezado.LastOrDefault(char.IsAsciiDigit) is var d and >= '1' and <= '3'
                    ? d - '0'
                    : usados.GetValueOrDefault(campo) + 1;
                usados[campo] = Math.Max(usados.GetValueOrDefault(campo), numero);
                campo = numero is >= 1 and <= VinculoTutor.MaximoPorAlumno ? $"{campo}_{numero}" : CampoImportacion.Ignorar;
            }
            else if (campo != CampoImportacion.Ignorar && !usados.TryAdd(campo, 1))
            {
                campo = CampoImportacion.Ignorar;
            }

            mapeo.Add(campo);
        }

        return mapeo;
    }

    private static string Campo(string e, bool hayApellidos)
    {
        bool Tiene(params string[] palabras) => palabras.Any(p => e.Contains(p, StringComparison.Ordinal));
        bool EsTelefono() => Tiene("telefono", "whatsapp", "celular", "movil") || e.Split(' ').Contains("tel");
        bool EsCorreo() => Tiene("correo", "email", "e mail", "mail");

        if (e.Length == 0 || e is "no" or "n" or "num" or "numero" or "no lista" or "numero de lista" or "lista")
        {
            return CampoImportacion.Ignorar;
        }

        if (Tiene("tutor", "padre", "madre", "responsable", "familiar"))
        {
            return EsTelefono() ? CampoImportacion.TutorTelefono
                : EsCorreo() ? CampoImportacion.TutorCorreo
                : Tiene("parentesco", "relacion") ? CampoImportacion.TutorParentesco
                : CampoImportacion.TutorNombre;
        }

        if (EsTelefono())
        {
            return CampoImportacion.TutorTelefono;
        }

        if (EsCorreo())
        {
            return CampoImportacion.TutorCorreo;
        }

        if (Tiene("parentesco"))
        {
            return CampoImportacion.TutorParentesco;
        }

        if (Tiene("paterno", "primer apellido"))
        {
            return CampoImportacion.ApellidoPaterno;
        }

        if (Tiene("materno", "segundo apellido"))
        {
            return CampoImportacion.ApellidoMaterno;
        }

        if (Tiene("apellido"))
        {
            return Tiene("nombre") ? CampoImportacion.NombreCompleto : CampoImportacion.Apellidos;
        }

        if (Tiene("nombre completo", "alumno", "estudiante"))
        {
            return CampoImportacion.NombreCompleto;
        }

        if (Tiene("nombre"))
        {
            return hayApellidos ? CampoImportacion.Nombres : CampoImportacion.NombreCompleto;
        }

        return CampoImportacion.Ignorar;
    }

    /// <summary>Minúsculas, sin acentos y con un solo espacio entre palabras.</summary>
    private static string Normalizar(string texto)
    {
        var sinAcentos = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sinAcentos.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            }
        }

        return string.Join(' ', sinAcentos.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed record FilaImportacion(string Nombres, string? Apellidos, IReadOnlyList<TutorImportado>? Tutores);

public sealed record TutorImportado(string? Nombre, string? Telefono, string? Correo, string? Parentesco);

public sealed record ConfirmarImportacion(IReadOnlyList<FilaImportacion> Filas);

internal sealed class ConfirmarImportacionValidador : AbstractValidator<ConfirmarImportacion>
{
    public ConfirmarImportacionValidador() => RuleFor(c => c.Filas).NotNull().Must(f => f.Count <= 500);
}

public sealed record ErrorDeFila(int Fila, string Codigo);

public sealed record ResultadoImportacion(int Guardados, int OmitidosPorError, int OmitidosPorLimite, IReadOnlyList<ErrorDeFila> Errores);

/// <summary>
/// RF-013 y RF-014, pasos 3 y 4: vuelve a validar las filas corregidas, guarda las válidas hasta llenar el grupo
/// (RN-011) con sus tutores, y dice cuántas omitió.
/// </summary>
public sealed class ConfirmarImportacionCasoUso(
    IRepositorioGrupos repositorio, ITutores tutores, VerificacionCorreo verificacion, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<ResultadoImportacion>?> EjecutarAsync(Guid importacionId, ConfirmarImportacion comando, CancellationToken ct)
    {
        if (await repositorio.ImportacionAsync(importacionId, ct) is not { } importacion
            || await repositorio.GrupoParaInscribirAsync(importacion.GrupoId, ct) is not { } grupo)
        {
            return null;
        }

        if (grupo.EstaArchivado)
        {
            return Resultado.Fallo<ResultadoImportacion>(ErroresGrupos.GrupoArchivado);
        }

        if (!await verificacion.VerificadoAsync(ct))
        {
            return Resultado.Fallo<ResultadoImportacion>(ErroresGrupos.CorreoNoVerificado);
        }

        var ahora = reloj.GetUtcNow();
        var confirmada = importacion.Confirmar(ahora);
        if (confirmada.EsFallo)
        {
            return Resultado.Fallo<ResultadoImportacion>(confirmada.Error);
        }

        var errores = new List<ErrorDeFila>();
        var validas = new List<(FilaImportacion Fila, IReadOnlyList<(string Telefono, TutorImportado Datos)> Tutores)>();
        for (var i = 0; i < comando.Filas.Count; i++)
        {
            var fila = comando.Filas[i];
            if (Validar(fila, out var tutoresValidos) is { } codigo)
            {
                errores.Add(new ErrorDeFila(i + 1, codigo));
            }
            else
            {
                validas.Add((fila, tutoresValidos));
            }
        }

        var existentes = await repositorio.AlumnosAsync(grupo.Id, ct);
        var inscritos = Alumno.Inscribir(grupo, existentes, validas.Select(v => (v.Fila.Nombres, v.Fila.Apellidos ?? string.Empty)), ahora);
        repositorio.Agregar(inscritos);

        // Inscribir devuelve los alumnos en el orden de las filas.
        for (var i = 0; i < inscritos.Count; i++)
        {
            foreach (var (telefono, datos) in validas[i].Tutores)
            {
                var (tutorId, _) = await tutores.VincularAsync(telefono, datos.Nombre!.Trim(), Correos.Normalizar(datos.Correo), ct);
                repositorio.Agregar(new VinculoTutor(inscritos[i], tutorId, telefono, new DatosTutor(datos.Nombre, datos.Correo, datos.Parentesco), ahora));
            }
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(new ResultadoImportacion(inscritos.Count, errores.Count, validas.Count - inscritos.Count, errores));
    }

    private static string? Validar(FilaImportacion fila, out IReadOnlyList<(string Telefono, TutorImportado Datos)> tutoresValidos)
    {
        tutoresValidos = [];
        if (string.IsNullOrWhiteSpace(fila.Nombres))
        {
            return ErrorFilaImportacion.NombreVacio;
        }

        if (fila.Nombres.Trim().Length > Alumno.LongitudNombres || (fila.Apellidos?.Trim().Length ?? 0) > Alumno.LongitudApellidos)
        {
            return ErrorFilaImportacion.NombreLargo;
        }

        var capturados = (fila.Tutores ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.Nombre) || !string.IsNullOrWhiteSpace(t.Telefono)
                || !string.IsNullOrWhiteSpace(t.Correo) || !string.IsNullOrWhiteSpace(t.Parentesco))
            .ToList();
        var lista = new List<(string, TutorImportado)>();
        foreach (var tutor in capturados)
        {
            if (string.IsNullOrWhiteSpace(tutor.Nombre) || string.IsNullOrWhiteSpace(tutor.Telefono)
                || tutor.Nombre.Trim().Length > VinculoTutor.LongitudNombre
                || (tutor.Parentesco?.Trim().Length ?? 0) > VinculoTutor.LongitudParentesco)
            {
                return ErrorFilaImportacion.TutorIncompleto;
            }

            if (Telefono.Normalizar(tutor.Telefono) is not { } telefono)
            {
                return ErrorFilaImportacion.TelefonoInvalido;
            }

            if (!Correos.EsValidoOVacio(tutor.Correo) || (tutor.Correo?.Trim().Length ?? 0) > VinculoTutor.LongitudCorreo)
            {
                return ErrorFilaImportacion.CorreoInvalido;
            }

            if (lista.All(t => t.Item1 != telefono))
            {
                lista.Add((telefono, tutor));
            }
        }

        if (lista.Count > VinculoTutor.MaximoPorAlumno)
        {
            return ErrorFilaImportacion.DemasiadosTutores;
        }

        tutoresValidos = lista;
        return null;
    }
}
