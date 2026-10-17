using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using FluentValidation;

namespace AulaLista.Application.Grupos;

public sealed record CicloDto(Guid Id, string Nombre, DateOnly Inicio, DateOnly Fin, string TipoUnidad, IReadOnlyList<UnidadDto> Unidades)
{
    public static CicloDto De(CicloEscolar c) => new(
        c.Id, c.Nombre, c.Inicio, c.Fin, c.TipoUnidad,
        c.Unidades.OrderBy(u => u.Orden).Select(u => new UnidadDto(u.Id, u.Nombre, u.Orden, u.Inicio, u.Fin)).ToList());
}

public sealed record UnidadDto(Guid Id, string Nombre, int Orden, DateOnly Inicio, DateOnly Fin);

public sealed class ListarCiclosCasoUso(IRepositorioGrupos repositorio)
{
    public async Task<IReadOnlyList<CicloDto>> EjecutarAsync(CancellationToken ct) =>
        (await repositorio.CiclosAsync(ct)).OrderByDescending(c => c.Inicio).Select(CicloDto.De).ToList();
}

public sealed record CrearCiclo(string Nombre, DateOnly Inicio, DateOnly Fin, string TipoUnidad, int? CantidadUnidades);

internal sealed class CrearCicloValidador : AbstractValidator<CrearCiclo>
{
    public CrearCicloValidador()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(CicloEscolar.LongitudNombre);
        RuleFor(c => c.TipoUnidad).NotEmpty().MaximumLength(20);
    }
}

/// <summary>RF-021, RF-022: crea el ciclo con las unidades propuestas (RN-024).</summary>
public sealed class CrearCicloCasoUso(IRepositorioGrupos repositorio, IContextoUsuario contexto, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<CicloDto>> EjecutarAsync(CrearCiclo comando, CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        if (await repositorio.ExisteCicloAsync(comando.Nombre.Trim(), null, ct))
        {
            return Resultado.Fallo<CicloDto>(ErroresGrupos.CicloDuplicado);
        }

        var creado = CicloEscolar.Crear(
            docenteId, comando.Nombre, comando.Inicio, comando.Fin, comando.TipoUnidad, comando.CantidadUnidades, reloj.GetUtcNow());
        if (creado.EsFallo)
        {
            return Resultado.Fallo<CicloDto>(creado.Error);
        }

        repositorio.Agregar(creado.Valor);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(CicloDto.De(creado.Valor));
    }
}

public sealed record EditarCiclo(string Nombre, DateOnly Inicio, DateOnly Fin);

internal sealed class EditarCicloValidador : AbstractValidator<EditarCiclo>
{
    public EditarCicloValidador() => RuleFor(c => c.Nombre).NotEmpty().MaximumLength(CicloEscolar.LongitudNombre);
}

public sealed class EditarCicloCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<CicloDto>?> EjecutarAsync(Guid id, EditarCiclo comando, CancellationToken ct)
    {
        if (await repositorio.CicloAsync(id, ct) is not { } ciclo)
        {
            return null;
        }

        if (await repositorio.ExisteCicloAsync(comando.Nombre.Trim(), id, ct))
        {
            return Resultado.Fallo<CicloDto>(ErroresGrupos.CicloDuplicado);
        }

        var resultado = ciclo.Editar(comando.Nombre, comando.Inicio, comando.Fin, reloj.GetUtcNow());
        if (resultado.EsFallo)
        {
            return Resultado.Fallo<CicloDto>(resultado.Error);
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(CicloDto.De(ciclo));
    }
}

public sealed record ReemplazarUnidades(IReadOnlyList<UnidadEditada> Unidades);

public sealed record UnidadEditada(Guid? Id, string Nombre, DateOnly Inicio, DateOnly Fin);

internal sealed class ReemplazarUnidadesValidador : AbstractValidator<ReemplazarUnidades>
{
    public ReemplazarUnidadesValidador()
    {
        RuleFor(c => c.Unidades).NotNull();
        RuleForEach(c => c.Unidades).ChildRules(u => u.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(Unidad.LongitudNombre));
    }
}

/// <summary>
/// Reemplaza las unidades del ciclo (RN-023, RN-026, RN-027). El cambio aplica a todos los grupos del ciclo
/// porque los periodos toman nombre y fechas de su unidad; cada unidad nueva crea su periodo en cada grupo.
/// </summary>
public sealed class ReemplazarUnidadesCasoUso(
    IRepositorioGrupos repositorio, IDatosDeEvaluacion evaluacion, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<CicloDto>?> EjecutarAsync(Guid cicloId, ReemplazarUnidades comando, CancellationToken ct)
    {
        if (await repositorio.CicloAsync(cicloId, ct) is not { } ciclo)
        {
            return null;
        }

        var ahora = reloj.GetUtcNow();
        var cerradas = await repositorio.UnidadesCerradasAsync(cicloId, ct);
        var propuestas = comando.Unidades.Select(u => new UnidadPropuesta(u.Id, u.Nombre, u.Inicio, u.Fin)).ToList();
        var conDatos = await evaluacion.UnidadesConDatosAsync(cicloId, ct);
        var resultado = ciclo.ReemplazarUnidades(propuestas, cerradas, conDatos, ahora);
        if (resultado.EsFallo)
        {
            return Resultado.Fallo<CicloDto>(resultado.Error);
        }

        if (resultado.Valor.Count > 0)
        {
            foreach (var grupo in await repositorio.GruposDelCicloAsync(cicloId, ct))
            {
                repositorio.Agregar(grupo.CrearPeriodos(resultado.Valor, ahora));
            }
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(CicloDto.De(ciclo));
    }
}
