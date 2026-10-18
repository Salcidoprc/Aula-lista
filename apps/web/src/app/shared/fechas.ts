/** "2026-08-25" se muestra "25/08/2026" (es-MX, PRODUCT.md). */
export function mostrarFecha(fecha: string): string {
  const [a, m, d] = fecha.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

const MES_CORTO = new Intl.DateTimeFormat('es-MX', { month: 'short', timeZone: 'UTC' });

/** "ago", "sep"…: el mes corto sin punto, para la línea de tiempo. */
export function mesCorto(fecha: string): string {
  return MES_CORTO.format(new Date(`${fecha.slice(0, 10)}T00:00:00Z`)).replace('.', '');
}

const FECHA_LARGA = new Intl.DateTimeFormat('es-MX', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

/** "3 de octubre de 2026" para un instante (no una fecha del calendario), en la zona del dispositivo. */
export function fechaLarga(instante: string): string {
  return FECHA_LARGA.format(new Date(instante));
}
