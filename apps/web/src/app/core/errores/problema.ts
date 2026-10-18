import { HttpErrorResponse } from '@angular/common/http';

/** Códigos con texto en `errores.*` (es-MX.json). Cualquier otro se muestra como `desconocido`. */
const CONOCIDOS = new Set([
  'aceptacion_requerida',
  'actividad_invalida',
  'alumno_dado_de_baja',
  'archivo_ilegible',
  'archivo_invalido',
  'asistencia_duplicada',
  'asistencia_invalida',
  'borrado_requiere_confirmacion',
  'calificacion_fuera_de_rango',
  'calificacion_invalida',
  'calificacion_menor_a_minima',
  'calificaciones_menores_a_minima',
  'cantidad_unidades_invalida',
  'cct_invalida',
  'celda_inexistente',
  'ciclo_duplicado',
  'ciclo_fechas_invalidas',
  'ciclo_inexistente',
  'ciclo_muy_largo',
  'configuracion_calificacion_invalida',
  'contrasena_comun',
  'correo_no_verificado',
  'correo_registrado',
  'credenciales_invalidas',
  'criterio_inexistente',
  'criterios_invalidos',
  'demasiados_intentos',
  'desconocido',
  'en_generacion',
  'entidad_federativa_invalida',
  'equipos_invalidos',
  'error_interno',
  'estilo_invalido',
  'exportacion_no_disponible',
  'fecha_fuera_de_unidad',
  'forma_con_calificaciones',
  'forma_invalida',
  'google',
  'google_no_disponible',
  'google_sin_correo_verificado',
  'grado_invalido',
  'grupo_archivado',
  'grupo_con_calificaciones',
  'grupo_duplicado',
  'importacion_confirmada',
  'instruccion_invalida',
  'limite_alumnos_grupo',
  'limite_ia',
  'limite_regeneraciones',
  'limite_tutores_alumno',
  'lote_invalido',
  'marca_invalida',
  'minima_invalida',
  'nivel_invalido',
  'no_autenticado',
  'no_encontrado',
  'nota_invalida',
  'numero_lista_invalido',
  'observacion_aprobada',
  'observacion_larga',
  'observacion_publicada',
  'observacion_vacia',
  'origen_sin_rubros',
  'periodo_cerrado',
  'periodo_con_rubros',
  'periodo_en_cierre',
  'periodo_sin_cierre',
  'pesos_criterios_no_suman_100',
  'pesos_no_suman_100',
  'reautenticacion_requerida',
  'requiere_pro',
  'rubro_sin_actividades',
  'rubros_invalidos',
  'rubros_no_suman_100',
  'sesion_invalida',
  'sin_calificaciones',
  'sin_conexion',
  'telefono_invalido',
  'tipo_rubro_invalido',
  'tipo_unidad_invalido',
  'token_invalido',
  'token_vencido',
  'tomas_fuera_de_unidad',
  'trato_invalido',
  'tutor_ya_vinculado',
  'unidad_cerrada',
  'unidad_con_datos',
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
