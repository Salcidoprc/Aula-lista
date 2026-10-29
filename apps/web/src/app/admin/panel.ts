import { MetasDto } from '../api/models/metas-dto';
import { SemanaMetricasDto } from '../api/models/semana-metricas-dto';

/** Las seis métricas del piloto (00-prd, sección 4), en el orden de la boleta. */
export type ClaveMetrica =
  | 'docentesActivos'
  | 'porcentajeCierran'
  | 'minutosCierre'
  | 'porcentajeObservacionesAceptadas'
  | 'porcentajeTutoresAbren'
  | 'porcentajeConversion';

export const METRICAS: readonly ClaveMetrica[] = [
  'docentesActivos',
  'porcentajeCierran',
  'minutosCierre',
  'porcentajeObservacionesAceptadas',
  'porcentajeTutoresAbren',
  'porcentajeConversion',
];

/** Cómo se lee cada métrica contra su meta: el tiempo de cierre cumple si queda por debajo. */
const MENOR_ES_MEJOR: ReadonlySet<ClaveMetrica> = new Set(['minutosCierre']);

export type EstadoMeta = 'cumple' | 'falta' | 'sinDatos';

/** El valor de una semana, o `null` si no hay con qué calcularlo (sin docentes, sin enlaces). */
export function valorMetrica(semana: SemanaMetricasDto, clave: ClaveMetrica): number | null {
  const v = semana[clave];
  return v === null || v === undefined ? null : Number(v);
}

export function estadoMeta(valor: number | null, meta: number, clave: ClaveMetrica): EstadoMeta {
  if (valor === null) {
    return 'sinDatos';
  }
  return (MENOR_ES_MEJOR.has(clave) ? valor < meta : valor >= meta) ? 'cumple' : 'falta';
}

export function metaDe(metas: MetasDto, clave: ClaveMetrica): number {
  return Number(metas[clave]);
}

/** Cifra corta para la boleta: enteros sin decimales, el resto con uno. */
export function cifraMetrica(valor: number | null, clave: ClaveMetrica): string {
  if (valor === null) {
    return '—';
  }
  const texto = new Intl.NumberFormat('es-MX', {
    maximumFractionDigits: Number.isInteger(valor) ? 0 : 1,
  }).format(valor);
  return clave === 'docentesActivos' || clave === 'minutosCierre' ? texto : `${texto} %`;
}

/** Pesos con dos decimales; menos de un centavo pero más de cero se dice así, no como 0.00. */
export function pesos(monto: number | string): string {
  const n = Number(monto);
  if (n > 0 && n < 0.01) {
    return '< 0.01';
  }
  return new Intl.NumberFormat('es-MX', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n);
}

export function entero(valor: number | string): string {
  return new Intl.NumberFormat('es-MX').format(Number(valor));
}

/** "13 oct": la semana por su lunes. */
export function semanaCorta(lunes: string): string {
  const [a, m, d] = lunes.split('-').map(Number);
  return new Intl.DateTimeFormat('es-MX', { day: 'numeric', month: 'short', timeZone: 'UTC' })
    .format(new Date(Date.UTC(a, m - 1, d)))
    .replace(/\./g, '');
}

/** Nombres sugeridos escritos uno por renglón: sin vacíos ni repetidos, en el orden escrito. */
export function nombresSugeridos(texto: string): string[] {
  const vistos = new Set<string>();
  const nombres: string[] = [];
  for (const linea of texto.split('\n')) {
    const nombre = linea.trim().replace(/\s+/g, ' ');
    const llave = nombre.toLocaleLowerCase('es-MX');
    if (nombre && !vistos.has(llave)) {
      vistos.add(llave);
      nombres.push(nombre);
    }
  }
  return nombres;
}

/** Los valores de la auditoría ya vienen como texto (listas unidas por comas); los sí/no se dicen en palabras. */
export function valorAuditoria(valor: string | null, si: string, no: string): string {
  if (valor === null) {
    return '—';
  }
  return valor === 'true' ? si : valor === 'false' ? no : valor;
}

/** "octubre de 2026": el mes del panel por su primer día. */
export function mesLargo(primerDia: string): string {
  const [a, m] = primerDia.split('-').map(Number);
  return new Intl.DateTimeFormat('es-MX', {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(a, m - 1, 1)));
}

const INSTANTE = new Intl.DateTimeFormat('es-MX', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
  timeZone: 'America/Mexico_City',
});

/** Fecha y hora del centro de México, como las cuenta el panel. */
export function instante(valor: string): string {
  return INSTANTE.format(new Date(valor)).replace(/\./g, '');
}

/** Porcentaje con un decimal si lo tiene; sin con qué calcularlo es una raya, nunca 0 %. */
export function porciento(valor: number | string | null | undefined): string {
  if (valor === null || valor === undefined) {
    return '—';
  }
  return `${new Intl.NumberFormat('es-MX', { maximumFractionDigits: 1 }).format(Number(valor))} %`;
}
