/** Solo rutas de la propia app (empiezan con una diagonal), para no redirigir a otro sitio. */
export function rutaLocal(volver: string | null | undefined): string {
  return volver && volver.startsWith('/') && !volver.startsWith('//') && !volver.startsWith('/\\')
    ? volver
    : '/';
}

/** Pantalla con cambios sin guardar: pregunta antes de salir. */
export interface ConCambiosSinGuardar {
  puedeSalir(): boolean | Promise<boolean>;
}

/** `canDeactivate` de las pantallas que implementan `ConCambiosSinGuardar`. */
export const confirmarSalida = (pantalla: ConCambiosSinGuardar | null) =>
  pantalla?.puedeSalir() ?? true;
