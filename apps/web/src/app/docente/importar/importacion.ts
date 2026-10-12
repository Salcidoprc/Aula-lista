import { FilaImportacion } from '../../api/models/fila-importacion';
import { normalizarTelefono } from '../../shared/telefono';

/** Campos a los que se asigna cada columna; mismos nombres que propone la API (`CampoImportacion`). */
export const CAMPOS_ALUMNO = [
  'ignorar',
  'nombre_completo',
  'apellidos',
  'nombres',
  'apellido_paterno',
  'apellido_materno',
] as const;
export const DATOS_TUTOR = ['nombre', 'telefono', 'correo', 'parentesco'] as const;
export const MAXIMO_TUTORES = 3;

export type DatoTutor = (typeof DATOS_TUTOR)[number];
export type Campo = (typeof CAMPOS_ALUMNO)[number] | `tutor_${DatoTutor}_${1 | 2 | 3}`;

/** Todos los campos en el orden en que se ofrecen en el mapeo. */
export const CAMPOS: readonly Campo[] = [
  ...CAMPOS_ALUMNO,
  ...[1, 2, 3].flatMap((n) => DATOS_TUTOR.map((d) => `tutor_${d}_${n}` as Campo)),
];

/** RF-014: el docente elige si cada renglón empieza con apellidos o con nombres. */
export type Orden = 'apellidos' | 'nombres';

export interface TutorFila {
  nombre: string;
  telefono: string;
  correo: string;
  parentesco: string;
}

export interface FilaVista {
  /** Renglón del archivo o del texto (1, 2, …), para que el docente lo encuentre. */
  readonly renglon: number;
  apellidos: string;
  nombres: string;
  tutores: TutorFila[];
  excluida: boolean;
}

/** Mismos códigos que `ErrorFilaImportacion` de la API, más el aviso de repetido (que no impide importar). */
export type ProblemaFila =
  | 'nombre_vacio'
  | 'nombre_largo'
  | 'telefono_invalido'
  | 'tutor_incompleto'
  | 'correo_invalido'
  | 'demasiados_tutores';

export type EstadoFila = 'lista' | 'error' | 'no_cabe' | 'excluida';

const PARTICULAS = new Set([
  'de',
  'del',
  'la',
  'las',
  'los',
  'y',
  'san',
  'santa',
  'van',
  'von',
  'mc',
  'da',
  'di',
]);

/**
 * Parte "García López Ana Sofía" en apellidos y nombres. Las partículas ("de la Cruz", "del Río") van con la
 * palabra que sigue. Con 3 palabras o más, dos son apellidos; con 2, una y una. Con coma, lo de antes son
 * los apellidos ("García López, Ana Sofía").
 */
export function separarNombreCompleto(
  texto: string,
  orden: Orden,
): { apellidos: string; nombres: string } {
  const limpio = texto.replace(/\s+/g, ' ').trim();
  const coma = limpio.indexOf(',');
  if (coma >= 0) {
    return { apellidos: limpio.slice(0, coma).trim(), nombres: limpio.slice(coma + 1).trim() };
  }
  const piezas: string[] = [];
  let pendiente: string[] = [];
  for (const palabra of limpio.split(' ').filter(Boolean)) {
    pendiente.push(palabra);
    if (!PARTICULAS.has(palabra.toLocaleLowerCase('es-MX'))) {
      piezas.push(pendiente.join(' '));
      pendiente = [];
    }
  }
  if (pendiente.length) {
    piezas.push(pendiente.join(' '));
  }
  if (piezas.length <= 1) {
    return { apellidos: '', nombres: piezas.join('') };
  }
  const cuantos = piezas.length >= 3 ? 2 : 1;
  return orden === 'apellidos'
    ? { apellidos: piezas.slice(0, cuantos).join(' '), nombres: piezas.slice(cuantos).join(' ') }
    : { apellidos: piezas.slice(-cuantos).join(' '), nombres: piezas.slice(0, -cuantos).join(' ') };
}

/** ¿Hace falta preguntar el orden? Solo si el nombre viene en una sola columna. */
export function usaNombreCompleto(mapeo: readonly string[]): boolean {
  return mapeo.includes('nombre_completo');
}

/** Aplica el mapeo a la tabla cruda y arma una fila por alumno (RF-013, paso 2). */
export function filasDesdeTabla(
  filas: readonly (readonly string[])[],
  mapeo: readonly string[],
  orden: Orden,
): FilaVista[] {
  return filas.map((celdas, i) => {
    const valor = (campo: string) =>
      mapeo
        .map((c, j) => (c === campo ? (celdas[j] ?? '').trim() : ''))
        .filter(Boolean)
        .join(' ');
    let apellidos = [valor('apellidos'), valor('apellido_paterno'), valor('apellido_materno')]
      .filter(Boolean)
      .join(' ');
    let nombres = valor('nombres');
    const completo = valor('nombre_completo');
    if (completo) {
      const partes = separarNombreCompleto(completo, orden);
      apellidos = apellidos || partes.apellidos;
      nombres = nombres || partes.nombres;
    }
    const tutores = [1, 2, 3].map((n) => ({
      nombre: valor(`tutor_nombre_${n}`),
      telefono: valor(`tutor_telefono_${n}`),
      correo: valor(`tutor_correo_${n}`),
      parentesco: valor(`tutor_parentesco_${n}`),
    }));
    return {
      renglon: i + 1,
      apellidos,
      nombres,
      tutores: tutores.filter(tieneDatos),
      excluida: false,
    };
  });
}

function tieneDatos(t: TutorFila): boolean {
  return !!(t.nombre.trim() || t.telefono.trim() || t.correo.trim() || t.parentesco.trim());
}

/** Misma validación que la API al confirmar (`ConfirmarImportacionCasoUso.Validar`). */
export function validarFila(fila: FilaVista): ProblemaFila | null {
  if (!fila.nombres.trim()) {
    return 'nombre_vacio';
  }
  if (fila.nombres.trim().length > 80 || fila.apellidos.trim().length > 80) {
    return 'nombre_largo';
  }
  const telefonos = new Set<string>();
  for (const tutor of fila.tutores.filter(tieneDatos)) {
    if (
      !tutor.nombre.trim() ||
      !tutor.telefono.trim() ||
      tutor.nombre.trim().length > 120 ||
      tutor.parentesco.trim().length > 30
    ) {
      return 'tutor_incompleto';
    }
    const telefono = normalizarTelefono(tutor.telefono);
    if (!telefono) {
      return 'telefono_invalido';
    }
    if (tutor.correo.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(tutor.correo.trim())) {
      return 'correo_invalido';
    }
    telefonos.add(telefono);
  }
  return telefonos.size > MAXIMO_TUTORES ? 'demasiados_tutores' : null;
}

/** Renglones con el mismo nombre que otro: se permiten (casos borde de SPEC-02) pero se señalan. */
export function repetidos(filas: readonly FilaVista[]): Set<number> {
  const clave = (f: FilaVista) =>
    `${f.apellidos} ${f.nombres}`
      .toLocaleLowerCase('es-MX')
      .normalize('NFD')
      .replace(/\p{M}/gu, '')
      .replace(/\s+/g, ' ')
      .trim();
  const vistos = new Map<string, number>();
  const resultado = new Set<number>();
  for (const f of filas.filter((x) => !x.excluida && x.nombres.trim())) {
    const k = clave(f);
    const previo = vistos.get(k);
    if (previo !== undefined) {
      resultado.add(previo);
      resultado.add(f.renglon);
    } else {
      vistos.set(k, f.renglon);
    }
  }
  return resultado;
}

/** Estado de cada fila: con error, lista, o sin lugar porque el grupo llega a 50 (RN-011). */
export function estados(filas: readonly FilaVista[], disponibles: number): EstadoFila[] {
  let lugares = disponibles;
  return filas.map((f) => {
    if (f.excluida) {
      return 'excluida';
    }
    if (validarFila(f)) {
      return 'error';
    }
    if (lugares <= 0) {
      return 'no_cabe';
    }
    lugares--;
    return 'lista';
  });
}

/** Lo que se manda a confirmar: solo las filas listas, con sus tutores. */
export function aConfirmar(filas: readonly FilaVista[], disponibles: number): FilaImportacion[] {
  const estado = estados(filas, disponibles);
  return filas
    .filter((_, i) => estado[i] === 'lista')
    .map((f) => ({
      nombres: f.nombres.trim(),
      apellidos: f.apellidos.trim() || null,
      tutores: f.tutores.filter(tieneDatos).map((t) => ({
        nombre: t.nombre.trim() || null,
        telefono: t.telefono.trim() || null,
        correo: t.correo.trim() || null,
        parentesco: t.parentesco.trim() || null,
      })),
    }));
}
