// RNF-011: el bundle inicial pesa máximo 250 KB comprimido. Angular mide sin comprimir, así que esto
// suma con gzip los archivos que index.html carga al inicio (scripts, módulos precargados y estilos).
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';

const LIMITE_KB = 250;
const dir = join(import.meta.dirname, '..', 'dist', 'web', 'browser');
const html = readFileSync(join(dir, 'index.html'), 'utf8');
const pendientes = [...html.matchAll(/(?:src|href)="([^"]+\.(?:js|css))"/g)].map((m) => m[1]);
const archivos = new Set();

// Sigue las importaciones estáticas entre chunks; las dinámicas (import()) son carga diferida.
while (pendientes.length > 0) {
  const archivo = pendientes.pop();
  if (archivos.has(archivo)) continue;
  archivos.add(archivo);
  if (!archivo.endsWith('.js')) continue;
  const codigo = readFileSync(join(dir, archivo), 'utf8');
  for (const m of codigo.matchAll(/(?:^|[;}\s])import\s*(?:[\w${},*\s]+from\s*)?["']\.\/([\w.-]+\.js)["']/g)) {
    pendientes.push(m[1]);
  }
}

let total = 0;
for (const archivo of archivos) {
  const kb = gzipSync(readFileSync(join(dir, archivo)), { level: 9 }).length / 1024;
  total += kb;
  console.log(`${archivo.padEnd(32)} ${kb.toFixed(1)} KB`);
}

console.log(`Inicial comprimido: ${total.toFixed(1)} KB (límite ${LIMITE_KB} KB)`);
if (total > LIMITE_KB) {
  console.error('El bundle inicial excede el límite de RNF-011.');
  process.exit(1);
}
