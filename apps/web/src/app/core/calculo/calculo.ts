import {
  CERO,
  Fraccion,
  Sentido,
  dividir,
  fraccion,
  mayor,
  menor,
  multiplicar,
  redondear,
  sumar,
} from './decimal';

/**
 * Gemelo del cálculo del backend (`Domain/Evaluacion/Calculo.cs`): RN-032 a RN-035, RN-102 a RN-104, RN-112, RN-118,
 * RN-119, RN-123 y RN-124.
 * Sirve para mostrar promedios al capturar sin conexión. Pasa los mismos vectores de prueba (RN-036).
 */
export type Forma = 'numerica' | 'cumplimiento';
export type Marca = 'ninguna' | 'cumplio' | 'no_cumplio' | 'no_entrego' | 'exento';
export type Numero = string | number;
export type EstadoAsistencia = 'presente' | 'falta' | 'retardo' | 'justificada';

export interface CriterioCalculo {
  readonly id: string;
  readonly peso: Numero;
  readonly forma: Forma;
  readonly califMinima: Numero;
}

export interface CeldaCriterio {
  readonly criterioId: string;
  readonly valor?: Numero | null;
  readonly marca?: Marca;
}

export interface CeldaCalculo {
  readonly valor?: Numero | null;
  readonly marca?: Marca;
  readonly criterios?: readonly CeldaCriterio[];
}

export interface RubroCalculo {
  readonly id: string;
  readonly peso: Numero;
  readonly comportamiento?: 'actividades' | 'asistencia';
  readonly forma: Forma;
  readonly califMinima: Numero;
  readonly criterios?: readonly CriterioCalculo[];
  readonly celdas: readonly CeldaCalculo[];
  /** Rubro de asistencia: el estado del alumno en cada toma de la unidad. */
  readonly tomas?: readonly string[];
}

export interface ConfiguracionCalculo {
  readonly califMinima: Numero;
  readonly decimalesFinal: number;
  readonly redondeo: Sentido;
  /** Lo que vale un retardo (RN-102); por defecto 1. */
  readonly valorRetardo?: Numero;
  /** Lo que suma cada participación extra (RN-121); por defecto 0.10. */
  readonly valorParticipacion?: Numero;
}

export interface ResultadoPeriodo {
  readonly rubros: ReadonlyMap<string, Fraccion | null>;
  readonly exacto: Fraccion | null;
  readonly periodo: Fraccion | null;
  readonly final: Fraccion | null;
  readonly sinDatos: readonly string[];
  /** Participaciones por su valor (RN-123), aunque el tope de 10 no deje sumarlas todas. */
  readonly puntosExtra: Fraccion;
}

const DIEZ = fraccion(10);

/** Valor de una actividad para el promedio, o nulo si no cuenta (vacía o exenta). */
export function valorActividad(rubro: RubroCalculo, celda: CeldaCalculo): Fraccion | null {
  const marca = celda.marca ?? 'ninguna';
  if (marca === 'exento') {
    return null;
  }
  if (marca === 'no_entrego') {
    return fraccion(rubro.califMinima); // RN-118
  }
  if (rubro.criterios?.length) {
    return valorConCriterios(rubro.criterios, celda.criterios ?? []);
  }
  return valorSimple(rubro.forma, rubro.califMinima, celda.valor, marca);
}

/** RN-112: suma ponderada de los criterios calificados; sus pesos se reparten si falta alguno. */
export function valorConCriterios(
  criterios: readonly CriterioCalculo[],
  celdas: readonly CeldaCriterio[],
): Fraccion | null {
  let suma = CERO;
  let pesos = CERO;
  for (const criterio of criterios) {
    const celda = celdas.find((c) => c.criterioId === criterio.id);
    const valor = celda
      ? valorSimple(criterio.forma, criterio.califMinima, celda.valor, celda.marca ?? 'ninguna')
      : null;
    if (valor === null) {
      continue;
    }
    const peso = fraccion(criterio.peso);
    suma = sumar(suma, multiplicar(valor, peso));
    pesos = sumar(pesos, peso);
  }
  return pesos.n === 0n ? null : dividir(suma, pesos);
}

/** RN-032: media aritmética de las actividades que cuentan. */
export function promedioRubro(rubro: RubroCalculo): Fraccion | null {
  let suma = CERO;
  let cuenta = 0;
  for (const celda of rubro.celdas) {
    const valor = valorActividad(rubro, celda);
    if (valor !== null) {
      suma = sumar(suma, valor);
      cuenta++;
    }
  }
  return cuenta === 0 ? null : dividir(suma, fraccion(cuenta));
}

/**
 * RN-102 a RN-104: presente vale 1, falta 0, retardo lo que diga el grupo y justificada no cuenta; el porcentaje
 * por 10 es el promedio. Sin tomas que cuenten, nulo.
 */
export function promedioAsistencia(
  tomas: readonly string[],
  valorRetardo: Numero = 1,
): Fraccion | null {
  let suma = CERO;
  let cuenta = 0;
  for (const estado of tomas) {
    const valor =
      estado === 'presente'
        ? fraccion(1)
        : estado === 'falta'
          ? CERO
          : estado === 'retardo'
            ? fraccion(valorRetardo)
            : null;
    if (valor !== null) {
      suma = sumar(suma, valor);
      cuenta++;
    }
  }
  return cuenta === 0 ? null : dividir(multiplicar(suma, DIEZ), fraccion(cuenta));
}

/** RN-123: participaciones de la unidad por el valor del grupo. */
export function puntosExtra(participaciones: number, valor: Numero = '0.10'): Fraccion {
  return multiplicar(fraccion(participaciones), fraccion(valor));
}

/**
 * RN-033 a RN-035: rubros con datos por su peso; los sin datos reparten el suyo; nunca bajo la mínima. RN-124: encima
 * de esa calificación ordinaria (ya sin bajar de la mínima) se suman los puntos de las participaciones, sin pasar de
 * 10; sin calificación ordinaria no hay calificación.
 */
export function calcularPeriodo(
  rubros: readonly RubroCalculo[],
  configuracion: ConfiguracionCalculo,
  participaciones = 0,
): ResultadoPeriodo {
  const extra = puntosExtra(participaciones, configuracion.valorParticipacion);
  const promedios = new Map<string, Fraccion | null>();
  const sinDatos: string[] = [];
  let suma = CERO;
  let pesos = CERO;
  for (const rubro of rubros) {
    const comportamiento = rubro.comportamiento ?? 'actividades';
    const promedio =
      comportamiento === 'actividades'
        ? promedioRubro(rubro)
        : comportamiento === 'asistencia'
          ? promedioAsistencia(rubro.tomas ?? [], configuracion.valorRetardo)
          : null;
    promedios.set(rubro.id, promedio);
    const peso = fraccion(rubro.peso);
    if (promedio !== null && peso.n > 0n) {
      suma = sumar(suma, multiplicar(promedio, peso));
      pesos = sumar(pesos, peso);
    } else {
      sinDatos.push(rubro.id);
    }
  }

  if (pesos.n === 0n) {
    return {
      rubros: promedios,
      exacto: null,
      periodo: null,
      final: null,
      sinDatos,
      puntosExtra: extra,
    };
  }
  const minima = fraccion(configuracion.califMinima);
  let exacto = dividir(suma, pesos);
  if (extra.n > 0n) {
    exacto = menor(DIEZ, sumar(mayor(minima, exacto), extra)); // RN-124
  }
  return {
    rubros: promedios,
    exacto,
    periodo: mayor(minima, redondear(exacto, 2, 'cercano')),
    final: mayor(minima, redondear(exacto, configuracion.decimalesFinal, configuracion.redondeo)),
    sinDatos,
    puntosExtra: extra,
  };
}

function valorSimple(
  forma: Forma,
  minima: Numero,
  valor: Numero | null | undefined,
  marca: Marca,
): Fraccion | null {
  switch (marca) {
    case 'cumplio':
      return DIEZ; // RN-118: ✅ vale 10
    case 'no_cumplio':
    case 'no_entrego':
      return fraccion(minima); // ❌ y No entregó valen la mínima
    case 'exento':
      return null;
    default:
      return forma === 'numerica' && valor !== null && valor !== undefined ? fraccion(valor) : null;
  }
}
