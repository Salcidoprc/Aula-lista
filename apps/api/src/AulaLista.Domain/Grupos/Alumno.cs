using System.Security.Cryptography;
using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Grupos;

/// <summary>Concordancia gramatical de las observaciones (RN-020).</summary>
public static class Trato
{
    public const string Neutro = "neutro";
    public const string El = "el";
    public const string Ella = "ella";

    public static readonly IReadOnlyList<string> Todos = [Neutro, El, Ella];
}

/// <summary>
/// Alumno de un grupo. Sin CURP ni fecha de nacimiento. La baja es lógica y conserva su historial (RN-017).
/// Ante la IA solo viaja su <see cref="Seudonimo"/> (RN-019).
/// </summary>
public sealed class Alumno : Entidad
{
    public const int LongitudNombres = 80;
    public const int LongitudApellidos = 80;
    public const int NumeroListaMaximo = 99;

    internal Alumno(Grupo grupo, string nombres, string apellidos, int numeroLista, string seudonimo, DateTimeOffset ahora)
        : base(ahora)
    {
        GrupoId = grupo.Id;
        DocenteId = grupo.DocenteId;
        Nombres = nombres;
        Apellidos = apellidos;
        NumeroLista = numeroLista;
        Seudonimo = seudonimo;
    }

    private Alumno()
    {
    }

    public Guid GrupoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Nombres { get; private set; } = string.Empty;

    public string Apellidos { get; private set; } = string.Empty;

    public int NumeroLista { get; private set; }

    public string Trato { get; private set; } = Grupos.Trato.Neutro;

    public string Seudonimo { get; private set; } = string.Empty;

    public DateTimeOffset? BajaEn { get; private set; }

    public bool EstaActivo => BajaEn is null;

    /// <summary>
    /// Da de alta un lote en un grupo (RN-011, RN-013): cabe hasta llenar 50 activos y se numera por orden
    /// alfabético de apellidos y nombres a partir del último número (ADR-0014). Devuelve los que cupieron, en el
    /// orden en que llegaron.
    /// </summary>
    public static IReadOnlyList<Alumno> Inscribir(
        Grupo grupo,
        IReadOnlyCollection<Alumno> existentes,
        IEnumerable<(string Nombres, string Apellidos)> nuevos,
        DateTimeOffset ahora)
    {
        var libres = Grupo.MaximoAlumnosActivos - existentes.Count(a => a.EstaActivo);
        var siguiente = existentes.Count == 0 ? 1 : existentes.Max(a => a.NumeroLista) + 1;
        var seudonimos = existentes.Select(a => a.Seudonimo).ToHashSet(StringComparer.Ordinal);
        var comparador = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("es-MX"), ignoreCase: true);

        var alumnos = nuevos
            .Take(Math.Max(libres, 0))
            .Select(n => new Alumno(grupo, n.Nombres.Trim(), n.Apellidos.Trim(), 0, NuevoSeudonimo(seudonimos), ahora))
            .ToList();
        foreach (var alumno in alumnos.OrderBy(a => a.Apellidos, comparador).ThenBy(a => a.Nombres, comparador))
        {
            alumno.NumeroLista = siguiente++;
        }

        return alumnos;
    }

    public Resultado Editar(string nombres, string apellidos, string trato, DateTimeOffset ahora)
    {
        if (!EstaActivo)
        {
            return Resultado.Fallo(ErroresGrupos.AlumnoDadoDeBaja);
        }

        if (!Grupos.Trato.Todos.Contains(trato))
        {
            return Resultado.Fallo(ErroresGrupos.TratoInvalido);
        }

        Nombres = nombres.Trim();
        Apellidos = apellidos.Trim();
        Trato = trato;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>Cambia el número de lista; si otro alumno activo lo tiene, se intercambian (ADR-0014).</summary>
    public Resultado CambiarNumero(int numero, IEnumerable<Alumno> delGrupo, DateTimeOffset ahora)
    {
        if (!EstaActivo)
        {
            return Resultado.Fallo(ErroresGrupos.AlumnoDadoDeBaja);
        }

        if (numero is < 1 or > NumeroListaMaximo)
        {
            return Resultado.Fallo(ErroresGrupos.NumeroListaInvalido);
        }

        if (numero == NumeroLista)
        {
            return Resultado.Exito();
        }

        if (delGrupo.FirstOrDefault(a => a.Id != Id && a.EstaActivo && a.NumeroLista == numero) is { } otro)
        {
            otro.NumeroLista = NumeroLista;
            otro.MarcarActualizada(ahora);
        }

        NumeroLista = numero;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>RN-017: deja de aparecer en captura y en periodos posteriores; conserva su historial.</summary>
    public void DarDeBaja(DateTimeOffset ahora)
    {
        if (EstaActivo)
        {
            BajaEn = ahora;
            MarcarActualizada(ahora);
        }
    }

    /// <summary>"Alumno " más 4 caracteres sin ambiguos (sin 0, O, 1, I, L), único en el grupo.</summary>
    private static string NuevoSeudonimo(HashSet<string> usados)
    {
        const string Alfabeto = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        string seudonimo;
        do
        {
            seudonimo = "Alumno " + RandomNumberGenerator.GetString(Alfabeto, 4);
        }
        while (!usados.Add(seudonimo));

        return seudonimo;
    }
}
