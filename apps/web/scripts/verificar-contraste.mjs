// RNF-018 y RNF-027: los pares de color de tokens.css cumplen WCAG 2.1 AA en el tema claro y en el oscuro.
// Lee los tokens del bloque :root y del bloque del tema oscuro, resuelve var() y calcula cada contraste.
// Texto: 4.5:1. Íconos, bordes de controles y marcas de estado: 3:1.
import Color from 'colorjs.io';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const css = readFileSync(join(import.meta.dirname, '..', 'src', 'styles', 'tokens.css'), 'utf8');

function bloque(inicio) {
  const desde = css.indexOf(inicio);
  if (desde < 0) throw new Error(`tokens.css no tiene el bloque ${inicio}`);
  const tokens = {};
  const cuerpo = css.slice(desde, css.indexOf('\n}', desde));
  for (const m of cuerpo.matchAll(/--([\w-]+):\s*(oklch\([^)]*\)|var\(--[\w-]+\))/g)) {
    tokens[m[1]] = m[2];
  }
  return tokens;
}

const claro = bloque(':root {');
const temas = { claro, oscuro: { ...claro, ...bloque(":root[data-tema='oscuro'] {") } };

function color(tokens, nombre) {
  const valor = tokens[nombre];
  if (!valor) throw new Error(`falta el token --${nombre}`);
  const referencia = /^var\(--([\w-]+)\)$/.exec(valor);
  return referencia ? color(tokens, referencia[1]) : new Color(valor);
}

// El trazo del grado repite la regla de tokens.css: la base en el claro, mezcla con la tinta en el oscuro.
function trazo(tema, tokens, grado) {
  const base = color(tokens, `grado-${grado}`);
  return tema === 'oscuro'
    ? Color.mix(base, color(tokens, `grado-${grado}-tinta`), 0.55, { space: 'oklch' })
    : base;
}

const grados = ['p1', 'p2', 'p3', 'p4', 'p5', 'p6', 's1', 's2', 's3'];
const superficies = ['color-fondo', 'color-superficie', 'color-superficie-hundida'];
const pares = [
  ...superficies.flatMap((s) => [
    ['color-texto', s, 4.5],
    ['color-texto-suave', s, 4.5],
    ['color-texto-tenue', s, 3],
  ]),
  ['color-accion', 'color-fondo', 4.5],
  ['color-accion', 'color-superficie', 4.5],
  ['color-accion', 'color-superficie-hundida', 3],
  ['color-sobre-accion', 'color-accion', 4.5],
  ['color-sobre-accion', 'color-accion-presionada', 4.5],
  ['color-accion-tinta', 'color-accion-tinte', 4.5],
  ['color-accion', 'color-accion-tinte', 3],
  ['color-sobre-grado', 'color-marca', 4.5],
  ['color-marca-rotulo', 'color-marca', 4.5],
  ...['exito', 'aviso', 'peligro'].flatMap((e) => [
    [`color-${e}-tinta`, `color-${e}-tinte`, 4.5],
    [`color-${e}-tinta`, 'color-fondo', 4.5],
    [`color-${e}`, 'color-fondo', 3],
    [`color-${e}`, 'color-superficie', 3],
  ]),
  ...grados.flatMap((g) => [
    ['color-sobre-grado', `grado-${g}`, 4.5],
    [`grado-${g}-rotulo`, `grado-${g}`, 4.5],
    [`grado-${g}-tinta`, `grado-${g}-tinte`, 4.5],
    [`grado-${g}-tinta`, 'color-fondo', 4.5],
    [`grado-${g}-tinta`, 'color-superficie', 4.5],
    ['color-texto', `grado-${g}-tinte`, 4.5],
    ['color-texto-suave', `grado-${g}-tinte`, 4.5],
  ]),
];

let fallas = 0;
for (const [tema, tokens] of Object.entries(temas)) {
  const medidos = pares.map(([texto, fondo, minimo]) => [
    `--${texto} / --${fondo}`,
    color(tokens, texto).contrast(color(tokens, fondo), 'WCAG21'),
    minimo,
  ]);
  for (const g of grados) {
    for (const fondo of [...superficies, `grado-${g}-tinte`]) {
      medidos.push([
        `trazo ${g} / --${fondo}`,
        trazo(tema, tokens, g).contrast(color(tokens, fondo), 'WCAG21'),
        3,
      ]);
    }
  }
  for (const [par, valor, minimo] of medidos) {
    if (valor < minimo) {
      fallas++;
      console.error(`${tema}: ${par} = ${valor.toFixed(2)}:1, mínimo ${minimo}:1`);
    }
  }
  const peor = medidos.reduce((a, b) => (b[1] / b[2] < a[1] / a[2] ? b : a));
  console.log(
    `${tema}: ${medidos.length} pares; el más justo es ${peor[0]} con ${peor[1].toFixed(2)}:1`,
  );
}

if (fallas > 0) {
  console.error(`${fallas} pares no llegan a WCAG 2.1 AA.`);
  process.exit(1);
}
