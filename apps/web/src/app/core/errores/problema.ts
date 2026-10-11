import { HttpErrorResponse } from '@angular/common/http';

/** Códigos con texto en `errores.*` (es-MX.json). Cualquier otro se muestra como `desconocido`. */
const CONOCIDOS = new Set([
  'aceptacion_requerida',
  'cct_invalida',
  'contrasena_comun',
  'correo_registrado',
  'credenciales_invalidas',
  'demasiados_intentos',
  'desconocido',
  'entidad_federativa_invalida',
  'error_interno',
  'exportacion_no_disponible',
  'google',
  'google_no_disponible',
  'google_sin_correo_verificado',
  'nivel_invalido',
  'no_autenticado',
  'no_encontrado',
  'reautenticacion_requerida',
  'sesion_invalida',
  'sin_conexion',
  'token_invalido',
  'token_vencido',
  'validacion_fallida',
  'version_no_vigente',
]);

/** Respuesta de error de la API (Problem Details con `codigo`, reglas/01). */
interface Problema {
  codigo?: string;
  errors?: Record<string, string[]>;
}

/**
 * Código de error de negocio para buscar su texto en `errores.<codigo>`. Sin respuesta del servidor
 * devuelve `sin_conexion`; cualquier otro caso, `desconocido`.
 */
export function codigoDeError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'desconocido';
  }
  if (error.status === 0) {
    return 'sin_conexion';
  }
  const cuerpo = leer(error.error);
  return cuerpo?.codigo && CONOCIDOS.has(cuerpo.codigo) ? cuerpo.codigo : 'desconocido';
}

/** Campos con error de validación (en minúsculas, como los nombra el formulario). */
export function camposConError(error: unknown): string[] {
  if (!(error instanceof HttpErrorResponse)) {
    return [];
  }
  return Object.keys(leer(error.error)?.errors ?? {}).map(
    (c) => c.charAt(0).toLowerCase() + c.slice(1),
  );
}

function leer(cuerpo: unknown): Problema | null {
  if (typeof cuerpo === 'string') {
    try {
      return JSON.parse(cuerpo) as Problema;
    } catch {
      return null;
    }
  }
  return typeof cuerpo === 'object' && cuerpo !== null ? (cuerpo as Problema) : null;
}
