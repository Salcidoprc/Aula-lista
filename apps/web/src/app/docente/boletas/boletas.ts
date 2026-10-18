import { BoletaAlumnoDto } from '../../api/models/boleta-alumno-dto';

/** Estados de la boleta como los manda la API (SPEC-05, pantalla "Boletas y envío"). */
export type EstadoBoleta = 'sin_aprobar' | 'sin_boleta' | 'preparando' | 'lista' | 'publicada';

/** Sin boleta, o lista pero con datos que cambiaron: lo que "Generar boletas" va a preparar (RF-050). */
export function porGenerar(alumnos: readonly BoletaAlumnoDto[]): BoletaAlumnoDto[] {
  return alumnos.filter(
    (a) => a.estado === 'sin_boleta' || (a.estado === 'lista' && a.desactualizada),
  );
}

/** Listas y al día: lo que se puede publicar (RN-053 rechaza las desactualizadas). */
export function publicables(alumnos: readonly BoletaAlumnoDto[]): BoletaAlumnoDto[] {
  return alumnos.filter((a) => a.estado === 'lista' && !a.desactualizada);
}

/** Publicada y con datos o texto distintos: se ofrece la versión corregida (RN-052). */
export function porCorregir(a: BoletaAlumnoDto): boolean {
  return a.estado === 'publicada' && a.desactualizada;
}

export interface ResumenPublicacion {
  readonly boletas: number;
  readonly whatsapp: number;
  readonly correo: number;
  readonly push: number;
  /** Tutores a los que no les llegará por ningún canal: el docente se los comparte al publicar (RN-054). */
  readonly porCompartir: number;
  /** Alumnos sin ningún tutor registrado: su boleta se publica pero nadie recibe aviso. */
  readonly sinContacto: readonly BoletaAlumnoDto[];
}

/**
 * El resumen previo a publicar (RN-054): cuántos tutores recibirán WhatsApp, correo o push, a cuántos tendrá que
 * compartirla el docente y qué alumnos no tienen tutor. Un tutor con dos hijos cuenta en cada uno.
 */
export function resumenPublicacion(alumnos: readonly BoletaAlumnoDto[]): ResumenPublicacion {
  return {
    boletas: alumnos.length,
    whatsapp: alumnos.reduce((n, a) => n + Number(a.tutoresWhatsapp), 0),
    correo: alumnos.reduce((n, a) => n + Number(a.tutoresCorreo), 0),
    push: alumnos.reduce((n, a) => n + Number(a.tutoresPush), 0),
    porCompartir: alumnos.reduce((n, a) => n + Number(a.tutoresPorCompartir), 0),
    sinContacto: alumnos.filter((a) => Number(a.tutoresWhatsapp) === 0),
  };
}

/**
 * Avance real de la preparación: de las boletas pedidas en esta tanda, cuántas ya no están "preparando". Si la
 * pantalla se abrió a media tanda, el total son las que se ven preparando la primera vez.
 */
export function avancePreparacion(
  total: number,
  preparando: number,
): { listas: number; fraccion: number } {
  const listas = Math.max(0, total - preparando);
  return { listas, fraccion: total === 0 ? 1 : listas / total };
}

/** La calificación final con los decimales del grupo (RN-034), o una raya sin datos. */
export function calificacionFinal(valor: number | string | null, decimales: number): string {
  return valor === null ? '—' : Number(valor).toFixed(decimales);
}
