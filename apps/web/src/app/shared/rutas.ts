/** Solo rutas de la propia app (empiezan con una diagonal), para no redirigir a otro sitio. */
export function rutaLocal(volver: string | null | undefined): string {
  return volver && volver.startsWith('/') && !volver.startsWith('//') && !volver.startsWith('/\\')
    ? volver
    : '/';
}
