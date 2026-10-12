import { ClaveGrado } from './portada/portada';

/** Nivel tal como lo guarda la API (SPEC-01, SPEC-02). */
export type Nivel = 'primaria' | 'secundaria';

export const GRADOS: Record<Nivel, readonly number[]> = {
  primaria: [1, 2, 3, 4, 5, 6],
  secundaria: [1, 2, 3],
};

/** Clave de color del grado: p1 … p6 o s1 … s3 (DESIGN.md, "Colores de grado"). */
export function claveGrado(nivel: string, grado: number | string): ClaveGrado {
  return `${nivel === 'secundaria' ? 's' : 'p'}${Number(grado)}` as ClaveGrado;
}

/** "4° B": el grado con número y símbolo de grado, nunca con palabras (DESIGN.md). */
export function nombreGrupo(grado: number | string, nombre: string): string {
  return `${Number(grado)}° ${nombre}`;
}
