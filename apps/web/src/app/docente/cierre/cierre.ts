import { AlumnoCierreDto } from '../../api/models/alumno-cierre-dto';
import { ConteosCierreDto } from '../../api/models/conteos-cierre-dto';

/** Estados de la observación como los manda la API (RF-044). */
export type EstadoObservacion = 'sin_generar' | 'generando' | 'borrador' | 'aprobada' | 'publicada';

/** Sugerencias rápidas para regenerar (SPEC-04, Revisar observación). */
export const SUGERENCIAS = ['breve', 'positiva', 'especifica'] as const;

/** Lo que el lector cuenta como palabras: separadas por espacios, como en el servidor. */
export function contarPalabras(texto: string): number {
  const limpio = texto.trim();
  return limpio ? limpio.split(/\s+/).length : 0;
}

/** Aprobada o ya publicada: el texto no se edita sin reabrir. */
export function estaAprobada(estado: string): boolean {
  return estado === 'aprobada' || estado === 'publicada';
}

export interface Segmento {
  readonly estado: 'aprobada' | 'borrador' | 'pendiente';
  readonly cantidad: number;
  readonly porcentaje: number;
}

/** La barra de avance: aprobadas, borradores y lo que falta (sin generar o generando), sobre el total. */
export function segmentos(c: ConteosCierreDto): Segmento[] {
  const aprobada = Number(c.aprobada) + Number(c.publicada);
  const borrador = Number(c.borrador);
  const pendiente = Number(c.sinGenerar) + Number(c.generando);
  const total = aprobada + borrador + pendiente;
  const parte = (n: number) => (total === 0 ? 0 : (n * 100) / total);
  return [
    { estado: 'aprobada', cantidad: aprobada, porcentaje: parte(aprobada) },
    { estado: 'borrador', cantidad: borrador, porcentaje: parte(borrador) },
    { estado: 'pendiente', cantidad: pendiente, porcentaje: parte(pendiente) },
  ];
}

/**
 * "Aprobar y siguiente": el próximo alumno en orden de lista que todavía no está aprobado, empezando después del
 * actual y dando la vuelta. Nulo si ya no queda ninguno.
 */
export function siguientePendiente(
  alumnos: readonly AlumnoCierreDto[],
  actualId: string,
): AlumnoCierreDto | null {
  const i = alumnos.findIndex((a) => a.alumnoId === actualId);
  const despues = [...alumnos.slice(i + 1), ...alumnos.slice(0, Math.max(i, 0))];
  return despues.find((a) => !estaAprobada(a.estado)) ?? null;
}

/** Anterior y siguiente en orden de lista, sin dar la vuelta. */
export function vecinos(
  alumnos: readonly AlumnoCierreDto[],
  actualId: string,
): { anterior: AlumnoCierreDto | null; siguiente: AlumnoCierreDto | null; posicion: number } {
  const i = alumnos.findIndex((a) => a.alumnoId === actualId);
  return {
    anterior: i > 0 ? alumnos[i - 1] : null,
    siguiente: i >= 0 && i < alumnos.length - 1 ? alumnos[i + 1] : null,
    posicion: i + 1,
  };
}

/** La calificación del periodo con sus 2 decimales (RN-034), o una raya sin datos. */
export function calificacion(valor: number | string | null): string {
  return valor === null ? '—' : Number(valor).toFixed(2);
}
