import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { ParticipacionDto } from '../../api/models/participacion-dto';
import { ParticipacionEnCola } from '../../core/sincronizacion/almacen-local';

/** Una participación como se ve en el dispositivo; `pendiente` si todavía no llega al servidor. */
export interface ParticipacionVisible {
  readonly id: string;
  readonly alumnoId: string;
  readonly fecha: string;
  readonly pendiente: boolean;
}

/**
 * Las participaciones del servidor con la cola encima (RF-111): las que se dieron sin red se suman y las que se
 * quitaron sin red desaparecen. En orden de llegada: la última de un alumno es la que quita "Quitar una".
 */
export function conPendientes(
  servidor: readonly ParticipacionDto[],
  pendientes: readonly ParticipacionEnCola[],
): ParticipacionVisible[] {
  const quitadas = new Set(pendientes.filter((p) => p.operacion === 'quitar').map((p) => p.id));
  const vistas: ParticipacionVisible[] = servidor
    .filter((p) => !quitadas.has(p.id))
    .map((p) => ({ id: p.id, alumnoId: p.alumnoId, fecha: p.fecha, pendiente: false }));
  const yaEstan = new Set(vistas.map((p) => p.id));
  for (const p of pendientes) {
    if (p.operacion === 'dar' && !yaEstan.has(p.id)) {
      vistas.push({ id: p.id, alumnoId: p.alumnoId, fecha: p.fecha, pendiente: true });
    }
  }
  return vistas;
}

/** Cuántas tiene cada alumno; con fecha, solo las de ese día. */
export function contar(
  participaciones: readonly ParticipacionVisible[],
  fecha?: string,
): ReadonlyMap<string, number> {
  const cuenta = new Map<string, number>();
  for (const p of participaciones) {
    if (fecha === undefined || p.fecha === fecha) {
      cuenta.set(p.alumnoId, (cuenta.get(p.alumnoId) ?? 0) + 1);
    }
  }
  return cuenta;
}

/** La más reciente del alumno en el día: la que se quita con "Quitar una". */
export function ultimaDelDia(
  participaciones: readonly ParticipacionVisible[],
  alumnoId: string,
  fecha: string,
): ParticipacionVisible | undefined {
  return participaciones.filter((p) => p.alumnoId === alumnoId && p.fecha === fecha).at(-1);
}

/** Sin acentos ni mayúsculas, para buscar "Muñoz" escribiendo "munoz". */
export function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();
}

/**
 * RF-110: busca por número de lista exacto o por palabras del nombre y los apellidos, en cualquier orden ("ana lop"
 * encuentra a "López Ruiz Ana").
 */
export function buscar(
  alumnos: readonly AlumnoMatrizDto[],
  texto: string,
): readonly AlumnoMatrizDto[] {
  const t = normalizar(texto);
  if (!t) {
    return alumnos;
  }
  if (/^\d+$/.test(t)) {
    return alumnos.filter((a) => String(a.numeroLista) === t);
  }
  const palabras = t.split(/\s+/);
  return alumnos.filter((a) => {
    const nombre = normalizar(`${a.apellidos} ${a.nombres}`).split(/\s+/);
    return palabras.every((p) => nombre.some((n) => n.startsWith(p)));
  });
}

/** "0.1" se ve "0.10": siempre con dos decimales. */
export function puntos(cantidad: number, valor: string | number): string {
  return ((cantidad * Math.round(Number(valor) * 100)) / 100).toFixed(2);
}

/** Valores de un toque en el diálogo (RN-121); cualquier otro de 0.01 a 1.00 se escribe. */
export const VALORES_RAPIDOS = ['0.10', '0.25', '0.50', '1.00'] as const;
