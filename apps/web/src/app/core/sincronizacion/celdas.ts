import { CambioCelda } from '../../api/models/cambio-celda';
import { CeldaDto } from '../../api/models/celda-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';

/** El `alumnoId` de un cambio que califica a un equipo completo (RN-115). */
export const SIN_ALUMNO = '00000000-0000-0000-0000-000000000000';

/** Llave de una celda: la actividad y el alumno. */
export function llaveCelda(c: { actividadId: string; alumnoId: string }): string {
  return `${c.actividadId}:${c.alumnoId}`;
}

export function esDeEquipo(cambio: Pick<CambioCelda, 'equipoId'>): boolean {
  return !!cambio.equipoId && cambio.equipoId !== SIN_ALUMNO;
}

/** Un equipo como se califica en una actividad: nombre e integrantes activos. */
export interface EquipoDeActividad {
  readonly equipoId: string;
  readonly nombre: string;
  readonly alumnoIds: readonly string[];
}

/**
 * RN-116: la composición guardada de la actividad; si todavía no se califica por equipo, los equipos de hoy.
 * Solo integrantes activos, igual que el servidor.
 */
export function composicion(
  matriz: MatrizCalificacionesDto,
  actividadId: string,
): readonly EquipoDeActividad[] {
  const activos = new Set(matriz.alumnos.filter((a) => !a.dadoDeBaja).map((a) => a.id));
  const guardada = (matriz.composiciones ?? []).filter((c) => c.actividadId === actividadId);
  const origen =
    guardada.length > 0
      ? guardada.map((c) => ({ equipoId: c.equipoId, nombre: c.nombre, alumnoIds: c.alumnoIds }))
      : (matriz.equipos ?? []).map((e) => ({
          equipoId: e.id,
          nombre: e.nombre,
          alumnoIds: e.alumnoIds,
        }));
  // En el orden de los equipos de hoy; los que ya no existen, al final por nombre.
  const orden = new Map((matriz.equipos ?? []).map((e, i) => [e.id, i]));
  return origen
    .map((e) => ({ ...e, alumnoIds: e.alumnoIds.filter((id) => activos.has(id)) }))
    .sort(
      (a, b) =>
        (orden.get(a.equipoId) ?? Infinity) - (orden.get(b.equipoId) ?? Infinity) ||
        a.nombre.localeCompare(b.nombre, 'es-MX'),
    );
}

function modoDe(matriz: MatrizCalificacionesDto, actividadId: string): string | undefined {
  for (const r of matriz.rubros) {
    const a = r.actividades.find((x) => x.id === actividadId);
    if (a) {
      return a.modo;
    }
  }
  return undefined;
}

function vacia(c: Pick<CambioCelda, 'valor' | 'marca' | 'criterios'>): boolean {
  return c.valor === null && c.marca === 'ninguna' && (c.criterios ?? []).length === 0;
}

/** La celda como queda en el dispositivo antes de que el servidor la confirme. */
export function celdaLocal(
  cambio: CambioCelda,
  anterior?: CeldaDto,
  origen: string = anterior?.origen ?? 'individual',
): CeldaDto {
  return {
    actividadId: cambio.actividadId,
    alumnoId: cambio.alumnoId,
    valor: cambio.valor,
    marca: cambio.marca,
    origen: vacia(cambio) ? 'individual' : origen,
    version: anterior?.version ?? 0,
    criterios: (cambio.criterios ?? []).map((c) => ({ ...c })),
  };
}

/** Las celdas que toca un cambio: la suya o, por equipo, las de cada integrante. */
export function llavesDeCambio(matriz: MatrizCalificacionesDto, cambio: CambioCelda): string[] {
  if (!esDeEquipo(cambio)) {
    return [llaveCelda(cambio)];
  }
  const equipo = composicion(matriz, cambio.actividadId).find(
    (e) => e.equipoId === cambio.equipoId,
  );
  return (equipo?.alumnoIds ?? []).map((alumnoId) =>
    llaveCelda({ actividadId: cambio.actividadId, alumnoId }),
  );
}

/** Pone o reemplaza celdas en la matriz; con `null`, la quita (nunca existió en el servidor). */
export function conCeldas(
  matriz: MatrizCalificacionesDto,
  celdas: ReadonlyMap<string, CeldaDto | null>,
): MatrizCalificacionesDto {
  if (celdas.size === 0) {
    return matriz;
  }
  const restantes = new Map(celdas);
  const lista: CeldaDto[] = [];
  for (const celda of matriz.celdas) {
    const llave = llaveCelda(celda);
    if (!restantes.has(llave)) {
      lista.push(celda);
      continue;
    }
    const nueva = restantes.get(llave);
    restantes.delete(llave);
    if (nueva) {
      lista.push(nueva);
    }
  }
  for (const nueva of restantes.values()) {
    if (nueva) {
      lista.push(nueva);
    }
  }
  return { ...matriz, celdas: lista };
}

/**
 * Aplica cambios aún no enviados encima de lo que mandó el servidor, para que no se pierdan de la vista. Sigue
 * la regla del servidor (RN-115): lo de un equipo llega a cada integrante salvo a los ajustados, y calificar
 * aparte a un integrante en una actividad por equipo lo ajusta.
 */
export function conCambios(
  matriz: MatrizCalificacionesDto,
  cambios: readonly CambioCelda[],
): MatrizCalificacionesDto {
  const actuales = new Map(matriz.celdas.map((c) => [llaveCelda(c), c]));
  const nuevas = new Map<string, CeldaDto>();
  const vigente = (llave: string) => nuevas.get(llave) ?? actuales.get(llave);
  for (const cambio of cambios) {
    if (esDeEquipo(cambio)) {
      const equipo = composicion(matriz, cambio.actividadId).find(
        (e) => e.equipoId === cambio.equipoId,
      );
      for (const alumnoId of equipo?.alumnoIds ?? []) {
        const llave = llaveCelda({ actividadId: cambio.actividadId, alumnoId });
        const anterior = vigente(llave);
        if (anterior?.origen !== 'ajustada') {
          nuevas.set(
            llave,
            celdaLocal({ ...cambio, alumnoId, equipoId: null }, anterior, 'equipo'),
          );
        }
      }
      continue;
    }
    const llave = llaveCelda(cambio);
    const enEquipo =
      modoDe(matriz, cambio.actividadId) === 'por_equipo' &&
      composicion(matriz, cambio.actividadId).some((e) => e.alumnoIds.includes(cambio.alumnoId));
    nuevas.set(llave, celdaLocal(cambio, vigente(llave), enEquipo ? 'ajustada' : 'individual'));
  }
  return conCeldas(matriz, nuevas);
}
