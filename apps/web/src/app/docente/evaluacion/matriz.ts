import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { ActividadDto } from '../../api/models/actividad-dto';
import { CambioCelda } from '../../api/models/cambio-celda';
import { CeldaDto } from '../../api/models/celda-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { RubroDto } from '../../api/models/rubro-dto';
import {
  Forma,
  Marca,
  ResultadoPeriodo,
  calcularPeriodo,
  valorActividad,
} from '../../core/calculo/calculo';
import { Fraccion, Sentido, comparar, fraccion, formatear } from '../../core/calculo/decimal';
import { llaveCelda } from '../../core/sincronizacion/celdas';

/** Cómo se califica una actividad (RN-115). */
export const MODOS = ['individual', 'por_equipo'] as const;
export type Modo = (typeof MODOS)[number];

/** Lo que se escribe en una celda, ya interpretado. */
export interface Captura {
  readonly valor: string | null;
  readonly marca: Marca;
}

export type ErrorCaptura =
  | 'calificacion_fuera_de_rango'
  | 'calificacion_invalida'
  | 'calificacion_menor_a_minima'
  | 'marca_invalida';

/** Índices para no recorrer la matriz en cada celda de la tabla. */
export interface IndiceMatriz {
  readonly celdas: ReadonlyMap<string, CeldaDto>;
  readonly rubroDe: ReadonlyMap<string, RubroDto>;
  readonly actividades: readonly ActividadDto[];
  readonly alumnosActivos: readonly AlumnoMatrizDto[];
}

export function indexar(matriz: MatrizCalificacionesDto): IndiceMatriz {
  const rubroDe = new Map<string, RubroDto>();
  const actividades: ActividadDto[] = [];
  for (const r of matriz.rubros) {
    for (const a of r.actividades) {
      rubroDe.set(a.id, r.rubro);
      actividades.push(a);
    }
  }
  return {
    celdas: new Map(matriz.celdas.map((c) => [llaveCelda(c), c])),
    rubroDe,
    actividades,
    alumnosActivos: matriz.alumnos.filter((a) => !a.dadoDeBaja),
  };
}

export function celdaVacia(celda: CeldaDto | undefined): boolean {
  return (
    !celda || (celda.valor === null && celda.marca === 'ninguna' && celda.criterios.length === 0)
  );
}

/** RN-033 a RN-035 para un alumno, con lo que hay en la matriz (incluidos cambios sin enviar). */
export function resultadoAlumno(
  matriz: MatrizCalificacionesDto,
  indice: IndiceMatriz,
  alumnoId: string,
): ResultadoPeriodo {
  return calcularPeriodo(
    matriz.rubros.map(({ rubro, actividades }) => ({
      id: rubro.id,
      peso: rubro.peso,
      comportamiento: rubro.comportamiento as 'actividades' | 'asistencia',
      forma: rubro.forma as Forma,
      califMinima: rubro.califMinima,
      criterios: rubro.criterios.map((c) => ({ ...c, forma: c.forma as Forma })),
      celdas: actividades.map((a) => aCalculo(indice.celdas.get(`${a.id}:${alumnoId}`))),
    })),
    {
      califMinima: matriz.configuracion.califMinima,
      decimalesFinal: Number(matriz.configuracion.decimalesFinal),
      redondeo: matriz.configuracion.redondeo as Sentido,
    },
  );
}

/** El valor que cuenta de una celda (RN-112 y RN-118), o nulo si no cuenta. */
export function valorDeCelda(rubro: RubroDto, celda: CeldaDto | undefined): Fraccion | null {
  return celda
    ? valorActividad(
        {
          id: rubro.id,
          peso: rubro.peso,
          forma: rubro.forma as Forma,
          califMinima: rubro.califMinima,
          criterios: rubro.criterios.map((c) => ({ ...c, forma: c.forma as Forma })),
          celdas: [],
        },
        aCalculo(celda),
      )
    : null;
}

export function aCalculo(celda: CeldaDto | undefined) {
  return {
    valor: celda?.valor ?? null,
    marca: (celda?.marca ?? 'ninguna') as Marca,
    criterios: (celda?.criterios ?? []).map((c) => ({ ...c, marca: c.marca as Marca })),
  };
}

/**
 * Interpreta lo que se escribe o se pega en una celda (tabla y teclado): "8.5", "8,5", "NE" (no entregó), "EX"
 * (exento); en cumplimiento, "✓", "si", "s" o "1" y "✗", "x", "no", "n" o "0". Vacío limpia la celda.
 */
export function interpretar(texto: string, forma: Forma): Captura | 'marca_invalida' {
  const t = texto.trim().toLowerCase();
  if (t === '') {
    return { valor: null, marca: 'ninguna' };
  }
  if (t === 'ne' || t === 'n/e') {
    return { valor: null, marca: 'no_entrego' };
  }
  if (t === 'ex' || t === 'exento') {
    return { valor: null, marca: 'exento' };
  }
  if (forma === 'cumplimiento') {
    if (['✓', '✅', 'si', 'sí', 's', '1'].includes(t)) {
      return { valor: null, marca: 'cumplio' };
    }
    if (['✗', '❌', 'x', 'no', 'n', '0'].includes(t)) {
      return { valor: null, marca: 'no_cumplio' };
    }
    return 'marca_invalida';
  }
  return { valor: t.replace(',', '.'), marca: 'ninguna' };
}

/** RN-030 y RN-118, igual que la API: de la mínima a 10, con hasta dos decimales. */
export function validarValor(valor: string | null, minima: string | number): ErrorCaptura | null {
  if (valor === null) {
    return null;
  }
  if (!/^-?\d+(\.\d+)?$/.test(valor)) {
    return 'calificacion_invalida';
  }
  const f = fraccion(valor);
  if (comparar(f, fraccion(0)) < 0 || comparar(f, fraccion(10)) > 0) {
    return 'calificacion_fuera_de_rango';
  }
  if ((valor.split('.')[1]?.length ?? 0) > 2 && !/^\d+\.\d{2}0+$/.test(valor)) {
    return 'calificacion_invalida';
  }
  return comparar(f, fraccion(minima)) < 0 ? 'calificacion_menor_a_minima' : null;
}

/** Texto corto de una celda para la tabla: número, ✓, ✗, NE o EX. */
export function textoCelda(celda: CeldaDto | undefined): string {
  if (!celda) {
    return '';
  }
  switch (celda.marca) {
    case 'cumplio':
      return '✓';
    case 'no_cumplio':
      return '✗';
    case 'no_entrego':
      return 'NE';
    case 'exento':
      return 'EX';
    default:
      return celda.valor === null ? '' : numero(celda.valor);
  }
}

/** "8", "8.5" o "8.75": sin ceros de más, como lo escribiría el docente. */
export function numero(valor: string | number): string {
  return formatear(fraccion(valor), 2).replace(/\.?0+$/, '');
}

export function dosDecimales(valor: Fraccion | null): string {
  return valor === null ? '—' : formatear(valor, 2);
}

/** Un cambio de celda para la cola, con la versión que conoce el dispositivo. */
export function cambio(
  actividadId: string,
  alumnoId: string,
  captura: Captura,
  anterior: CeldaDto | undefined,
  criterios: CambioCelda['criterios'] = null,
): CambioCelda {
  return {
    id: null,
    actividadId,
    alumnoId,
    valor: captura.valor,
    marca: captura.marca,
    criterios,
    version: anterior ? Number(anterior.version) : 0,
  };
}

/** Lo pegado desde Excel: renglones por salto de línea y columnas por tabulador. */
export function leerPegado(texto: string): string[][] {
  const renglones = texto.replace(/\r\n?/g, '\n').replace(/\n$/, '').split('\n');
  return renglones.map((r) => r.split('\t'));
}

/** Hoy en la zona del docente, como "2026-10-17". */
export function hoy(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** La unidad que abre: la que tiene a hoy entre sus fechas; si no, la primera abierta; si no, la última. */
export function unidadDeHoy(
  periodos: readonly PeriodoDto[],
  fecha = hoy(),
): PeriodoDto | undefined {
  return (
    periodos.find((p) => p.inicio <= fecha && fecha <= p.fin) ??
    periodos.find((p) => p.estado === 'abierto') ??
    periodos.at(-1)
  );
}
