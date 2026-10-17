/**
 * Decimal exacto como fracción de enteros grandes (RN-035): suma, multiplica y divide sin perder nada, igual
 * que `decimal` en el backend. Solo se redondea al mostrar.
 */
export interface Fraccion {
  readonly n: bigint;
  readonly d: bigint;
}

export type Sentido = 'arriba' | 'abajo' | 'cercano';

export const CERO: Fraccion = { n: 0n, d: 1n };

/** Lee "8.75", "-1" o 8.75. Un número de JS se lee por su texto: así 8.1 es 81/10 y no su binario. */
export function fraccion(valor: string | number): Fraccion {
  const texto = typeof valor === 'number' ? numeroATexto(valor) : valor.trim();
  const coincide = /^(-?)(\d+)(?:\.(\d+))?$/.exec(texto);
  if (!coincide) {
    throw new Error(`No es un decimal: ${texto}`);
  }
  const [, signo, enteros, decimales = ''] = coincide;
  const n = BigInt(enteros + decimales) * (signo ? -1n : 1n);
  return reducir({ n, d: 10n ** BigInt(decimales.length) });
}

export function sumar(a: Fraccion, b: Fraccion): Fraccion {
  return reducir({ n: a.n * b.d + b.n * a.d, d: a.d * b.d });
}

export function multiplicar(a: Fraccion, b: Fraccion): Fraccion {
  return reducir({ n: a.n * b.n, d: a.d * b.d });
}

export function dividir(a: Fraccion, b: Fraccion): Fraccion {
  if (b.n === 0n) {
    throw new Error('División entre cero');
  }
  return reducir({ n: a.n * b.d, d: a.d * b.n });
}

export function comparar(a: Fraccion, b: Fraccion): number {
  const diferencia = a.n * b.d - b.n * a.d;
  return diferencia === 0n ? 0 : diferencia > 0n ? 1 : -1;
}

export function mayor(a: Fraccion, b: Fraccion): Fraccion {
  return comparar(a, b) >= 0 ? a : b;
}

/** RN-035: hacia arriba, hacia abajo o al más cercano con el medio hacia arriba (lejos del cero). */
export function redondear(valor: Fraccion, decimales: number, sentido: Sentido): Fraccion {
  const factor = 10n ** BigInt(decimales);
  const escalado = valor.n * factor;
  let entero: bigint;
  if (sentido === 'arriba') {
    entero = divisionPiso(escalado + valor.d - 1n, valor.d);
  } else if (sentido === 'abajo') {
    entero = divisionPiso(escalado, valor.d);
  } else {
    const absoluto = (escalado < 0n ? -escalado : escalado) * 2n + valor.d;
    const redondeado = absoluto / (2n * valor.d);
    entero = escalado < 0n ? -redondeado : redondeado;
  }
  return reducir({ n: entero, d: factor });
}

/** Texto con decimales fijos ("8.10"); redondea al más cercano si hace falta. */
export function formatear(valor: Fraccion, decimales: number): string {
  const r = redondear(valor, decimales, 'cercano');
  const factor = 10n ** BigInt(decimales);
  const escalado = (r.n * factor) / r.d;
  const negativo = escalado < 0n;
  const digitos = (negativo ? -escalado : escalado).toString().padStart(decimales + 1, '0');
  const corte = digitos.length - decimales;
  const texto = decimales === 0 ? digitos : `${digitos.slice(0, corte)}.${digitos.slice(corte)}`;
  return negativo ? `-${texto}` : texto;
}

export function aNumero(valor: Fraccion): number {
  return Number(formatear(valor, 6));
}

function divisionPiso(a: bigint, b: bigint): bigint {
  const q = a / b;
  return a % b !== 0n && a < 0n !== b < 0n ? q - 1n : q;
}

function reducir(f: Fraccion): Fraccion {
  const signo = f.d < 0n ? -1n : 1n;
  const g = mcd(f.n < 0n ? -f.n : f.n, f.d < 0n ? -f.d : f.d) || 1n;
  return { n: (signo * f.n) / g, d: (signo * f.d) / g };
}

function mcd(a: bigint, b: bigint): bigint {
  while (b !== 0n) {
    [a, b] = [b, a % b];
  }
  return a;
}

function numeroATexto(valor: number): string {
  if (!Number.isFinite(valor)) {
    throw new Error(`No es un decimal: ${valor}`);
  }
  // Calificaciones y pesos llevan a lo más 2 decimales; toFixed evita la notación 1e-7.
  return Number.isInteger(valor) ? String(valor) : String(Number(valor.toFixed(10)));
}
