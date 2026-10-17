import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/** Descarga un archivo de la API (con la sesión) y lo guarda con el nombre que manda el servidor. */
export async function descargarArchivo(
  http: HttpClient,
  url: string,
  nombrePorDefecto: string,
): Promise<void> {
  const respuesta = await firstValueFrom(
    http.get(url, { observe: 'response', responseType: 'blob' }),
  );
  const nombre = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(
    respuesta.headers.get('Content-Disposition') ?? '',
  )?.[1];
  const enlace = document.createElement('a');
  const objeto = URL.createObjectURL(respuesta.body!);
  enlace.href = objeto;
  enlace.download = nombre ? decodeURIComponent(nombre) : nombrePorDefecto;
  enlace.click();
  // Algunos navegadores (Safari en iOS) leen el archivo después del clic.
  setTimeout(() => URL.revokeObjectURL(objeto), 60_000);
}
