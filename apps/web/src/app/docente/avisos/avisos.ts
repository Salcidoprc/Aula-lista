import { AlumnoDeTutorDto } from '../../api/models/alumno-de-tutor-dto';
import { DestinatarioDto } from '../../api/models/destinatario-dto';
import { EntregaDto } from '../../api/models/entrega-dto';

/** RN-070: los topes del título y del cuerpo, los mismos que valida la API. */
export const TITULO_MAXIMO = 80;
export const CUERPO_MAXIMO = 1000;

/** El contador aparece cuando falta poco para el tope: antes solo distrae. */
export function mostrarContador(largo: number, maximo: number): boolean {
  return largo >= maximo * 0.8;
}

export interface CanalesPrevios {
  readonly push: number;
  readonly correo: number;
  readonly whatsapp: number;
  /** Sin ningún canal: el docente se lo comparte desde su WhatsApp (RN-073). */
  readonly compartir: number;
}

/**
 * Por dónde le llegará el aviso a cada tutor, como lo decidirá la API (RN-072, RN-073): push y correo si los tiene,
 * WhatsApp del sistema solo si es urgente y le queda saldo este mes (RN-086); sin nada, compartir a mano.
 */
export function canalesPrevios(
  tutores: readonly DestinatarioDto[],
  urgente: boolean,
): CanalesPrevios {
  let push = 0;
  let correo = 0;
  let whatsapp = 0;
  let compartir = 0;
  for (const t of tutores) {
    const conWhatsapp = urgente && Number(t.urgentesRestantes) > 0;
    push += t.push ? 1 : 0;
    correo += t.correo ? 1 : 0;
    whatsapp += conWhatsapp ? 1 : 0;
    compartir += !t.push && !t.correo && !conWhatsapp ? 1 : 0;
  }
  return { push, correo, whatsapp, compartir };
}

/** Cuántos de los elegidos ya no tienen urgentes este mes: el interruptor lo dice. */
export function sinSaldo(tutores: readonly DestinatarioDto[]): number {
  return tutores.filter((t) => Number(t.urgentesRestantes) === 0).length;
}

/** "Ana y Mateo López": a quién representa el tutor, en el orden de la lista. */
export function nombresAlumnos(alumnos: readonly AlumnoDeTutorDto[]): string {
  const nombres = alumnos.map((a) => `${a.nombres} ${a.apellidos}`.trim());
  return nombres.length <= 1
    ? (nombres[0] ?? '')
    : `${nombres.slice(0, -1).join(', ')} y ${nombres[nombres.length - 1]}`;
}

/** Búsqueda sin acentos ni mayúsculas sobre el tutor y sus alumnos. */
export function coincide(t: DestinatarioDto, busqueda: string): boolean {
  const q = normalizar(busqueda);
  if (!q) {
    return true;
  }
  return normalizar(`${t.nombre} ${nombresAlumnos(t.alumnos)}`).includes(q);
}

function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().trim();
}

/** Lo que falta compartir primero, luego lo ya compartido; en cada grupo, el orden de la API (lista). */
export function porCompartir(entregas: readonly EntregaDto[]): EntregaDto[] {
  return entregas
    .filter((e) => e.porCompartir)
    .sort((a, b) => Number(!!a.compartidoEn) - Number(!!b.compartidoEn));
}

/** La fracción de "Leído por n de m", entre 0 y 1. */
export function fraccionLeidos(leidos: number | string, total: number | string): number {
  const m = Number(total);
  return m === 0 ? 0 : Math.min(1, Number(leidos) / m);
}

export { fechaAviso, fechaHoraAviso } from '../../shared/fechas';
