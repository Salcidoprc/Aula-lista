using AulaLista.Application.Comun;
using AulaLista.Domain.Operacion;

namespace AulaLista.Infrastructure.Persistencia;

internal sealed class Auditoria(AulaListaDbContext db, IContextoUsuario contexto, TimeProvider reloj) : IAuditoria
{
    public void Registrar(string accion, string entidad, Guid? entidadId, Guid? docenteId, int? cantidad = null) =>
        db.Auditoria.Add(new RegistroAuditoria(
            docenteId is null ? "sistema" : "docente",
            docenteId,
            accion,
            entidad,
            entidadId,
            contexto.DireccionIp,
            reloj.GetUtcNow(),
            cantidad));
}
