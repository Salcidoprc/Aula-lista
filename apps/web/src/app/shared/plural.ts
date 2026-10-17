/** Forma del texto según la cantidad: las claves de es-MX.json traen `uno` y `otros`. */
export function forma(cantidad: number | string): 'uno' | 'otros' {
  return Number(cantidad) === 1 ? 'uno' : 'otros';
}
