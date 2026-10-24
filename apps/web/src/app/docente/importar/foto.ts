/** SPEC-02, foto de lista (RF-015): JPG o PNG; la API rechaza lo que pase de 5 MB. */
export const TIPOS_FOTO = ['image/jpeg', 'image/png'] as const;
export const TAMANO_MAXIMO_FOTO = 5 * 1024 * 1024;

/**
 * Lado largo de la foto que se sube: suficiente para leer letra a mano y no más, porque la IA cobra por
 * píxeles y el docente paga los datos del celular.
 */
export const LADO_MAXIMO = 1568;

export function esFotoAceptada(archivo: Pick<File, 'type'>): boolean {
  return (TIPOS_FOTO as readonly string[]).includes(archivo.type);
}

/** Las medidas a las que se reduce una foto sin deformarla; una foto chica se queda igual. */
export function medidaReducida(
  ancho: number,
  alto: number,
  maximo = LADO_MAXIMO,
): { ancho: number; alto: number } {
  const escala = Math.min(1, maximo / Math.max(ancho, alto));
  return { ancho: Math.round(ancho * escala), alto: Math.round(alto * escala) };
}

/**
 * Reduce la foto y la vuelve a codificar como JPEG. Además de pesar menos, así no viajan los metadatos de la
 * cámara, como la ubicación. Si el navegador no puede dibujarla, se sube tal cual y la API decide.
 */
export async function prepararFoto(archivo: File): Promise<Blob> {
  if (typeof createImageBitmap !== 'function') {
    return archivo;
  }
  let imagen: ImageBitmap;
  try {
    imagen = await createImageBitmap(archivo, { imageOrientation: 'from-image' });
  } catch {
    return archivo;
  }
  try {
    const { ancho, alto } = medidaReducida(imagen.width, imagen.height);
    const lienzo = document.createElement('canvas');
    lienzo.width = ancho;
    lienzo.height = alto;
    const contexto = lienzo.getContext('2d');
    if (!contexto) {
      return archivo;
    }
    contexto.drawImage(imagen, 0, 0, ancho, alto);
    const reducida = await new Promise<Blob | null>((listo) =>
      lienzo.toBlob(listo, 'image/jpeg', 0.85),
    );
    return reducida ?? archivo;
  } finally {
    imagen.close();
  }
}
