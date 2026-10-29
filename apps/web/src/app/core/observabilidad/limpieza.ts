/** Largo máximo de un texto que sale a Grafana: una pila o un mensaje no necesita más. */
const LARGO_MAXIMO = 2000;

/** Una URL (absoluta o una ruta) seguida de su consulta o fragmento. */
const URL_CON_EXTRAS = /((?:https?:\/\/|\/)[^\s?#"'()]*)[?#][^\s"'()]*/g;

/**
 * SPEC-09 y RN-019: deja un envío de Faro sin la consulta ni el fragmento de ninguna URL (el enlace del tutor lleva su
 * token en `#` y los de verificar o restablecer van en `?`) y recorta los textos largos. Las rutas solo llevan IDs.
 */
export function limpiarEnvio<T>(valor: T): T {
  return limpiar(valor) as T;
}

function limpiar(valor: unknown): unknown {
  if (typeof valor === 'string') {
    const limpio = valor.replace(URL_CON_EXTRAS, '$1');
    return limpio.length > LARGO_MAXIMO ? limpio.slice(0, LARGO_MAXIMO) : limpio;
  }
  if (Array.isArray(valor)) {
    return valor.map(limpiar);
  }
  if (valor !== null && typeof valor === 'object' && !(valor instanceof Error)) {
    return Object.fromEntries(Object.entries(valor).map(([clave, v]) => [clave, limpiar(v)]));
  }
  return valor;
}
