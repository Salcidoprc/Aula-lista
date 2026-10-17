import { CambioCelda } from '../../api/models/cambio-celda';
import { CeldaDto } from '../../api/models/celda-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';

/** Llave de una celda: la actividad y el alumno. */
export function llaveCelda(c: { actividadId: string; alumnoId: string }): string {
  return `${c.actividadId}:${c.alumnoId}`;
}

/** La celda como queda en el dispositivo antes de que el servidor la confirme. */
export function celdaLocal(cambio: CambioCelda, anterior?: CeldaDto): CeldaDto {
  return {
    actividadId: cambio.actividadId,
    alumnoId: cambio.alumnoId,
    valor: cambio.valor,
    marca: cambio.marca,
    origen: anterior?.origen ?? 'individual',
    version: anterior?.version ?? 0,
    criterios: (cambio.criterios ?? []).map((c) => ({ ...c })),
  };
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

/** Aplica cambios aún no enviados encima de lo que mandó el servidor, para que no se pierdan de la vista. */
export function conCambios(
  matriz: MatrizCalificacionesDto,
  cambios: readonly CambioCelda[],
): MatrizCalificacionesDto {
  const actuales = new Map(matriz.celdas.map((c) => [llaveCelda(c), c]));
  const nuevas = new Map<string, CeldaDto>();
  for (const cambio of cambios) {
    const llave = llaveCelda(cambio);
    nuevas.set(llave, celdaLocal(cambio, nuevas.get(llave) ?? actuales.get(llave)));
  }
  return conCeldas(matriz, nuevas);
}
