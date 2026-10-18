import { TomaAsistenciaDto } from '../../api/models/toma-asistencia-dto';
import { EstadoAsistencia, Numero } from '../../core/calculo/calculo';
import { AsistenciaEnCola } from '../../core/sincronizacion/almacen-local';

/** RN-101: el orden en que cambia el estado con cada toque. */
export const ESTADOS: readonly EstadoAsistencia[] = ['presente', 'falta', 'retardo', 'justificada'];

/** Teclas que ponen el estado directo en la fila con foco. */
export const TECLAS: Readonly<Record<string, EstadoAsistencia>> = {
  p: 'presente',
  f: 'falta',
  r: 'retardo',
  j: 'justificada',
};

/** La lista de un día como se ve en el dispositivo: el estado de cada alumno que cuenta en ella. */
export interface TomaVisible {
  readonly fecha: string;
  readonly estados: ReadonlyMap<string, EstadoAsistencia>;
}

export function siguienteEstado(actual: EstadoAsistencia): EstadoAsistencia {
  return ESTADOS[(ESTADOS.indexOf(actual) + 1) % ESTADOS.length];
}

/**
 * Las tomas del servidor con lo que el dispositivo aún no envía encima, en orden (RF-103). Una fecha que solo
 * existe en la cola parte de todos los activos presentes (RN-101), como la creará el servidor.
 */
export function conPendientes(
  servidor: readonly TomaAsistenciaDto[],
  pendientes: readonly AsistenciaEnCola[],
  activos: readonly string[],
): TomaVisible[] {
  const porFecha = new Map<string, Map<string, EstadoAsistencia>>();
  for (const toma of servidor) {
    porFecha.set(
      toma.fecha,
      new Map(toma.alumnos.map((a) => [a.alumnoId, a.estado as EstadoAsistencia])),
    );
  }
  for (const p of pendientes) {
    let estados = porFecha.get(p.fecha);
    if (!estados) {
      estados = new Map(activos.map((id) => [id, 'presente' as EstadoAsistencia]));
      porFecha.set(p.fecha, estados);
    }
    for (const a of p.alumnos) {
      if (estados.has(a.alumnoId) || activos.includes(a.alumnoId)) {
        estados.set(a.alumnoId, a.estado as EstadoAsistencia);
      }
    }
  }
  return [...porFecha]
    .map(([fecha, estados]) => ({ fecha, estados }))
    .sort((a, b) => a.fecha.localeCompare(b.fecha));
}

export type Resumen = Readonly<Record<EstadoAsistencia, number>>;

export function resumen(estados: Iterable<EstadoAsistencia>): Resumen {
  const cuenta = { presente: 0, falta: 0, retardo: 0, justificada: 0 };
  for (const e of estados) {
    cuenta[e]++;
  }
  return cuenta;
}

/**
 * RN-103: porcentaje de asistencia con el valor del retardo del grupo; la justificada no cuenta. Entero redondeado
 * para mostrar; el cálculo del promedio usa `promedioAsistencia`, con fracciones exactas.
 */
export function porcentaje(
  estados: readonly EstadoAsistencia[],
  valorRetardo: Numero = 1,
): number | null {
  let suma = 0;
  let cuenta = 0;
  for (const e of estados) {
    if (e === 'justificada') {
      continue;
    }
    cuenta++;
    suma += e === 'presente' ? 100 : e === 'retardo' ? Number(valorRetardo) * 100 : 0;
  }
  return cuenta === 0 ? null : Math.round(suma / cuenta);
}

/** "2026-10-17" ± días, sin pasar por la zona horaria del dispositivo. */
export function moverDia(fecha: string, dias: number): string {
  const d = new Date(`${fecha}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + dias);
  return d.toISOString().slice(0, 10);
}

const DIA_LARGO = new Intl.DateTimeFormat('es-MX', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  timeZone: 'UTC',
});
const DIA_CORTO = new Intl.DateTimeFormat('es-MX', {
  day: 'numeric',
  month: 'short',
  timeZone: 'UTC',
});

/** "sábado, 17 de octubre" se muestra "Sábado 17 de octubre". */
export function diaLargo(fecha: string): string {
  const texto = DIA_LARGO.format(new Date(`${fecha}T00:00:00Z`)).replace(',', '');
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

/** "17 oct", para el encabezado del historial. */
export function diaCorto(fecha: string): string {
  return DIA_CORTO.format(new Date(`${fecha}T00:00:00Z`)).replace('.', '');
}
