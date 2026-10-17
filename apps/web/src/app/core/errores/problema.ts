import { HttpErrorResponse } from '@angular/common/http';

/** Códigos con texto en `errores.*` (es-MX.json). Cualquier otro se muestra como `desconocido`. */
const CONOCIDOS = new Set([
  'aceptacion_requerida',
  'alumno_dado_de_baja',
  'archivo_ilegible',
  'archivo_invalido',
  'cantidad_unidades_invalida',
  'cct_invalida',
  'ciclo_duplicado',
  'ciclo_fechas_invalidas',
  'ciclo_inexistente',
  'ciclo_muy_largo',
  'configuracion_calificacion_invalida',
  'contrasena_comun',
  'correo_no_verificado',
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
  'grado_invalido',
  'grupo_archivado',
  'grupo_duplicado',
  'importacion_confirmada',
  'limite_alumnos_grupo',
  'limite_tutores_alumno',
  'lote_invalido',
  'nivel_invalido',
  'no_autenticado',
  'no_encontrado',
  'numero_lista_invalido',
  'reautenticacion_requerida',
  'sesion_invalida',
  'sin_conexion',
  'telefono_invalido',
  'tipo_unidad_invalido',
  'token_invalido',
  'token_vencido',
  'trato_invalido',
  'tutor_ya_vinculado',
  'unidad_cerrada',
  'unidad_fechas_invalidas',
  'unidad_fuera_de_ciclo',
  'unidad_inexistente',
  'unidades_traslapadas',
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
