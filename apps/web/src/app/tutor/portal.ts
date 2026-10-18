import { fechaLarga } from '../shared/fechas';
import { BoletaPublicadaDto } from '../api/models/boleta-publicada-dto';
import { HijoDto } from '../api/models/hijo-dto';

/** Lo que va después de `#` en el enlace del tutor: solo el token, sin el signo. */
export function tokenDelFragmento(fragmento: string | null | undefined): string | null {
  const token = (fragmento ?? '').replace(/^#/, '').trim();
  return /^[A-Za-z0-9_-]{20,}$/.test(token) ? token : null;
}

/** `?alumno=<id>&ver=avisos` del enlace de un aviso o boleta; nada que no sea un id y una pestaña conocida. */
export function destinoDelEnlace(
  busqueda: string,
): { alumno: string; ver: 'avisos' | null } | null {
  const params = new URLSearchParams(busqueda);
  const alumno = params.get('alumno') ?? '';
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(alumno)) {
    return null;
  }
  return { alumno, ver: params.get('ver') === 'avisos' ? 'avisos' : null };
}

/** "Laura Treviño" se saluda "Laura". */
export function primerNombre(nombre: string | null | undefined): string | null {
  const primero = (nombre ?? '').trim().split(/\s+/)[0];
  return primero ? primero : null;
}

export function nombreCompleto(hijo: Pick<HijoDto, 'nombres' | 'apellidos'>): string {
  return `${hijo.nombres} ${hijo.apellidos}`.trim();
}

/** "4° B · Español": el grupo como lo dice la escuela, con la materia si la hay (secundaria). */
export function grupoDelHijo(hijo: Pick<HijoDto, 'grado' | 'grupo' | 'materia'>): string {
  const grupo = `${Number(hijo.grado)}° ${hijo.grupo}`;
  return hijo.materia ? `${grupo} · ${hijo.materia}` : grupo;
}

/** La calificación final con los decimales del grupo (RN-034), o una raya sin datos. */
export function calificacion(valor: number | string | null, decimales: number | string): string {
  return valor === null ? '—' : Number(valor).toFixed(Number(decimales));
}

/** El peso del rubro sin ceros de más: 40, 12.5. */
export function peso(valor: number | string): string {
  return `${Number(valor)}%`;
}

export { fechaLarga };

const FECHA_CORTA = new Intl.DateTimeFormat('es-MX', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

/** "12/10/2026": la leyenda de la versión corregida (RN-052) usa este formato. */
export function fechaCorta(iso: string): string {
  return FECHA_CORTA.format(new Date(iso));
}

/** Para la pestaña Calificaciones: del primer periodo al más reciente, como se lee una boleta de papel. */
export function enOrdenDelCiclo(boletas: readonly BoletaPublicadaDto[]): BoletaPublicadaDto[] {
  return [...boletas].reverse();
}

/** Nombre de la transición compartida entre la tarjeta de un periodo y su boleta (catálogo de la regla 07). */
export function transicionBoleta(id: string): string {
  return `boleta-${id}`;
}

/**
 * El nombre corto del chip; si se repite (el mismo hijo en dos materias), lleva su materia o su grupo, y si aun así
 * se repite, el nombre completo.
 */
export function etiquetaHijo(hijo: HijoDto, todos: readonly HijoDto[]): string {
  const corta = (h: HijoDto) => primerNombre(h.nombres) ?? h.nombres;
  const conGrupo = (h: HijoDto) => `${corta(h)} · ${h.materia ?? `${Number(h.grado)}° ${h.grupo}`}`;
  const otros = todos.filter((otro) => otro.id !== hijo.id);
  if (!otros.some((otro) => corta(otro) === corta(hijo))) {
    return corta(hijo);
  }
  return otros.some((otro) => conGrupo(otro) === conGrupo(hijo))
    ? nombreCompleto(hijo)
    : conGrupo(hijo);
}

/** La API manda los enteros como `number | string`; una versión mayor a 1 es una boleta corregida (RN-052). */
export function esCorregida(version: number | string): boolean {
  return Number(version) > 1;
}

export type EstadoPush = 'sinSoporte' | 'agregarInicio' | 'denegado' | 'activado' | 'apagado';

/**
 * RF-064, RN-066: qué puede hacer este navegador con las notificaciones. En iPhone solo hay push con Aula Lista
 * agregada a la pantalla de inicio; si el permiso se negó, solo el navegador puede volver a darlo.
 */
export function estadoPush(entorno: {
  readonly soportado: boolean;
  readonly ios: boolean;
  readonly instalada: boolean;
  readonly permiso: NotificationPermission | null;
  readonly suscrito: boolean;
}): EstadoPush {
  if (entorno.ios && !entorno.instalada) {
    return 'agregarInicio';
  }
  if (!entorno.soportado || entorno.permiso === null) {
    return 'sinSoporte';
  }
  if (entorno.permiso === 'denied') {
    return 'denegado';
  }
  return entorno.suscrito && entorno.permiso === 'granted' ? 'activado' : 'apagado';
}

/** iPhone o iPad (que en iPadOS se presenta como Mac con pantalla táctil). */
export function esIos(agente: string, plataforma: string, toques: number): boolean {
  return /iPad|iPhone|iPod/.test(agente) || (plataforma === 'MacIntel' && toques > 1);
}
