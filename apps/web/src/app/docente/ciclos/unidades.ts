/** Fechas como las manda la API: "aaaa-mm-dd", sin hora ni zona. */
export type Fecha = string;

export const TIPOS_UNIDAD = [
  'bimestre',
  'trimestre',
  'cuatrimestre',
  'semestre',
  'mensual',
  'personalizada',
] as const;
export type TipoUnidad = (typeof TIPOS_UNIDAD)[number];

export const MAXIMO_UNIDADES = 12;
const MESES_MAXIMOS = 13;

const MESES_POR_UNIDAD: Partial<Record<TipoUnidad, number>> = {
  bimestre: 2,
  trimestre: 3,
  cuatrimestre: 4,
  semestre: 6,
  mensual: 1,
};

export interface UnidadEnEdicion {
  readonly id: string | null;
  readonly nombre: string;
  readonly inicio: Fecha;
  readonly fin: Fecha;
}

/** Errores con el mismo código que devuelve la API (SPEC-02). */
export type ErrorCiclo =
  | 'ciclo_fechas_invalidas'
  | 'ciclo_muy_largo'
  | 'unidad_nombre_requerido'
  | 'unidad_fechas_invalidas'
  | 'unidad_fuera_de_ciclo'
  | 'unidades_traslapadas'
  | 'cantidad_unidades_invalida';

const MS_DIA = 86_400_000;

export function diaNumero(fecha: Fecha): number {
  const [a, m, d] = fecha.split('-').map(Number);
  return Math.round(Date.UTC(a, m - 1, d) / MS_DIA);
}

export function deDiaNumero(dia: number): Fecha {
  return new Date(dia * MS_DIA).toISOString().slice(0, 10);
}

export function esFecha(texto: string | null | undefined): texto is Fecha {
  return !!texto && /^\d{4}-\d{2}-\d{2}$/.test(texto) && deDiaNumero(diaNumero(texto)) === texto;
}

/** Nombre de la unidad número n (1, 2, …) según el tipo, igual que la API. */
export function nombreUnidad(tipo: TipoUnidad, numero: number): string {
  const prefijos: Record<TipoUnidad, string> = {
    bimestre: 'Bimestre',
    trimestre: 'Trimestre',
    cuatrimestre: 'Cuatrimestre',
    semestre: 'Semestre',
    mensual: 'Mes',
    personalizada: 'Unidad',
  };
  return `${prefijos[tipo]} ${numero}`;
}

/** RN-024: piso de meses entre meses por unidad más 0.25 (3 trimestres de agosto a julio). */
export function cantidadPropuesta(inicio: Fecha, fin: Fecha, tipo: TipoUnidad): number {
  const meses = MESES_POR_UNIDAD[tipo];
  if (!meses) {
    return 1;
  }
  const duracion = (diaNumero(fin) - diaNumero(inicio) + 1) / 30.44;
  return Math.min(MAXIMO_UNIDADES, Math.max(1, Math.floor(duracion / meses + 0.25)));
}

/** Reparte el ciclo en partes iguales por días, como lo hace la API al crear el ciclo. */
export function proponerUnidades(
  inicio: Fecha,
  fin: Fecha,
  tipo: TipoUnidad,
  cantidad: number,
): UnidadEnEdicion[] {
  const desde = diaNumero(inicio);
  const dias = diaNumero(fin) - desde + 1;
  if (dias < 1 || cantidad < 1 || cantidad > dias) {
    return [];
  }
  return Array.from({ length: cantidad }, (_, i) => ({
    id: null,
    nombre: nombreUnidad(tipo, i + 1),
    inicio: deDiaNumero(desde + Math.floor((i * dias) / cantidad)),
    fin: deDiaNumero(desde + Math.floor(((i + 1) * dias) / cantidad) - 1),
  }));
}

/** Como `DateOnly.AddMonths`: si el día no existe en el mes de llegada, queda el último del mes. */
export function sumarMeses(fecha: Fecha, meses: number): Fecha {
  const [a, m, d] = fecha.split('-').map(Number);
  const ultimo = new Date(Date.UTC(a, m - 1 + meses + 1, 0)).getUTCDate();
  return new Date(Date.UTC(a, m - 1 + meses, Math.min(d, ultimo))).toISOString().slice(0, 10);
}

/** RN-022: fin después del inicio y como máximo 13 meses. */
export function validarCiclo(inicio: Fecha, fin: Fecha): ErrorCiclo | null {
  if (!esFecha(inicio) || !esFecha(fin) || diaNumero(fin) <= diaNumero(inicio)) {
    return 'ciclo_fechas_invalidas';
  }
  return fin > sumarMeses(inicio, MESES_MAXIMOS) ? 'ciclo_muy_largo' : null;
}

/**
 * RN-023: cada unidad con nombre y fechas en orden, dentro del ciclo y sin traslape con la anterior.
 * Devuelve el error de cada unidad (null si está bien) en el mismo orden.
 */
export function validarUnidades(
  inicio: Fecha,
  fin: Fecha,
  unidades: readonly UnidadEnEdicion[],
): (ErrorCiclo | null)[] {
  return unidades.map((u, i) => {
    // Solo en la web: la API responde validación genérica; aquí el mensaje dice qué falta.
    if (!u.nombre.trim()) {
      return 'unidad_nombre_requerido';
    }
    if (!esFecha(u.inicio) || !esFecha(u.fin) || u.fin < u.inicio) {
      return 'unidad_fechas_invalidas';
    }
    if (u.inicio < inicio || u.fin > fin) {
      return 'unidad_fuera_de_ciclo';
    }
    const anterior = unidades[i - 1];
    return anterior && esFecha(anterior.fin) && u.inicio <= anterior.fin
      ? 'unidades_traslapadas'
      : null;
  });
}
