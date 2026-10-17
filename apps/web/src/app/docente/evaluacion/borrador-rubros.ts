import { RubroDto } from '../../api/models/rubro-dto';
import { RubroEntrada } from '../../api/models/rubro-entrada';
import { TipoRubroDto } from '../../api/models/tipo-rubro-dto';
import { Forma } from '../../core/calculo/calculo';
import { CERO, Fraccion, comparar, formatear, fraccion, sumar } from '../../core/calculo/decimal';
import { numero } from './matriz';

/** Lo que el docente está editando: textos tal cual los escribe, hasta guardar (RN-110, RN-111, RN-118). */
export interface CriterioBorrador {
  readonly clave: string;
  readonly id: string | null;
  readonly nombre: string;
  readonly peso: string;
  readonly forma: Forma;
  readonly califMinima: string;
  readonly tieneCalificaciones: boolean;
}

export interface RubroBorrador {
  readonly clave: string;
  readonly id: string | null;
  readonly nombre: string;
  readonly tipoRubroId: string;
  readonly peso: string;
  readonly forma: Forma;
  readonly califMinima: string;
  readonly tieneCalificaciones: boolean;
  readonly conCriterios: boolean;
  readonly criterios: readonly CriterioBorrador[];
}

export const MAXIMO_RUBROS = 20;
export const CRITERIOS_MINIMO = 2;
export const CRITERIOS_MAXIMO = 10;

/** Lo que falta corregir, por id de campo (`<clave>-nombre`, `<clave>-peso`...) o por rubro (`<clave>-criterios`). */
export type ErrorBorrador =
  'nombre' | 'peso' | 'minima' | 'sumaCriterios' | 'cantidadCriterios' | 'tipo';

let siguiente = 0;
function nuevaClave(): string {
  return `b${++siguiente}`;
}

export function deRubros(rubros: readonly RubroDto[]): RubroBorrador[] {
  return rubros.map((r) => ({
    clave: nuevaClave(),
    id: r.id,
    nombre: r.nombre,
    tipoRubroId: r.tipoRubroId,
    peso: numero(r.peso),
    forma: r.forma as Forma,
    califMinima: numero(r.califMinima),
    tieneCalificaciones: r.tieneCalificaciones,
    conCriterios: r.criterios.length > 0,
    criterios: r.criterios.map((c) => ({
      clave: nuevaClave(),
      id: c.id,
      nombre: c.nombre,
      peso: numero(c.peso),
      forma: c.forma as Forma,
      califMinima: numero(c.califMinima),
      tieneCalificaciones: c.tieneCalificaciones,
    })),
  }));
}

/** Un rubro nuevo del tipo dado; el peso propone lo que falta para 100. */
export function rubroNuevo(
  tipo: TipoRubroDto,
  minimaGrupo: string,
  sumaActual: Fraccion,
): RubroBorrador {
  const falta = sumar(fraccion(100), { n: -sumaActual.n, d: sumaActual.d });
  return {
    clave: nuevaClave(),
    id: null,
    nombre: tipo.nombresSugeridos[0] ?? tipo.nombre,
    tipoRubroId: tipo.id,
    peso: comparar(falta, CERO) > 0 ? numero(formatear(falta, 2)) : '0',
    forma: (tipo.formaSugerida as Forma) || 'numerica',
    califMinima: minimaGrupo,
    tieneCalificaciones: false,
    conCriterios: false,
    criterios: [],
  };
}

export function criterioNuevo(minima: string, forma: Forma = 'numerica'): CriterioBorrador {
  return {
    clave: nuevaClave(),
    id: null,
    nombre: '',
    peso: '',
    forma,
    califMinima: minima,
    tieneCalificaciones: false,
  };
}

/** "8,5" y "8.5" valen lo mismo; devuelve el texto normalizado o null si no es un decimal. */
export function decimal(texto: string): string | null {
  const t = texto.trim().replace(',', '.');
  return /^\d{1,3}(\.\d{1,2})?$/.test(t) ? t : null;
}

function enRango(texto: string, maximo: number): boolean {
  const t = decimal(texto);
  return t !== null && comparar(fraccion(t), fraccion(maximo)) <= 0;
}

/** Suma de lo que se puede leer; lo que todavía no es número no cuenta. */
export function sumaPesos(lista: readonly { peso: string }[]): Fraccion {
  return lista.reduce((s, x) => {
    const t = decimal(x.peso);
    return t === null ? s : sumar(s, fraccion(t));
  }, CERO);
}

/**
 * Validación en el cliente, la misma de la API (RN-110, RN-111, RN-118): nombre, peso de 0 a 100, mínima de
 * 0 a 10, y con criterios de 2 a 10 que sumen 100. Los pesos de los rubros no tienen que sumar 100 para
 * guardar (borrador, SPEC-03 API); eso se exige al iniciar el cierre.
 */
export function validar(
  rubros: readonly RubroBorrador[],
  tipos: ReadonlyMap<string, TipoRubroDto>,
): Map<string, ErrorBorrador> {
  const errores = new Map<string, ErrorBorrador>();
  for (const r of rubros) {
    const tipo = tipos.get(r.tipoRubroId);
    if (!tipo) {
      errores.set(`${r.clave}-tipo`, 'tipo');
    }
    if (!r.nombre.trim()) {
      errores.set(`${r.clave}-nombre`, 'nombre');
    }
    if (!enRango(r.peso, 100)) {
      errores.set(`${r.clave}-peso`, 'peso');
    }
    if (tipo?.comportamiento !== 'asistencia' && !enRango(r.califMinima, 10)) {
      errores.set(`${r.clave}-minima`, 'minima');
    }
    if (!r.conCriterios || tipo?.comportamiento === 'asistencia') {
      continue;
    }
    for (const c of r.criterios) {
      if (!c.nombre.trim()) {
        errores.set(`${c.clave}-nombre`, 'nombre');
      }
      if (!enRango(c.peso, 100)) {
        errores.set(`${c.clave}-peso`, 'peso');
      }
      if (!enRango(c.califMinima, 10)) {
        errores.set(`${c.clave}-minima`, 'minima');
      }
    }
    if (r.criterios.length < CRITERIOS_MINIMO || r.criterios.length > CRITERIOS_MAXIMO) {
      errores.set(`${r.clave}-criterios`, 'cantidadCriterios');
    } else if (comparar(sumaPesos(r.criterios), fraccion(100)) !== 0) {
      errores.set(`${r.clave}-criterios`, 'sumaCriterios');
    }
  }
  return errores;
}

/** Lo que se manda a `PUT /periodos/{id}/rubros`, en el orden de la pantalla. Números como texto. */
export function aEntrada(
  rubros: readonly RubroBorrador[],
  tipos: ReadonlyMap<string, TipoRubroDto>,
): RubroEntrada[] {
  return rubros.map((r) => {
    const asistencia = tipos.get(r.tipoRubroId)?.comportamiento === 'asistencia';
    return {
      id: r.id,
      nombre: r.nombre.trim(),
      tipoRubroId: r.tipoRubroId,
      peso: decimal(r.peso) ?? r.peso,
      forma: asistencia ? 'numerica' : r.forma,
      califMinima: decimal(r.califMinima) ?? r.califMinima,
      criterios:
        r.conCriterios && !asistencia
          ? r.criterios.map((c) => ({
              id: c.id,
              nombre: c.nombre.trim(),
              peso: decimal(c.peso) ?? c.peso,
              forma: c.forma,
              califMinima: decimal(c.califMinima) ?? c.califMinima,
            }))
          : null,
    };
  });
}
