# ADR-0018: Promedios, exportación a Excel y avisos pasan a Pro

**Estado:** Aceptada · **Fecha:** 14/11/2026

## Contexto

ADR-0006 dejó en Gratis todo menos el cierre con IA y las boletas. El 14/11/2026 el dueño del producto decidió que Gratis se quede con la captura (grupos, alumnos, importación, captura, rubros, equipos, notas y asistencia) y que los promedios, la exportación a Excel y todos los avisos a tutores sean de Pro. Con el catálogo de ADR-0017, cada una de esas funciones puede ir o no en un producto.

## Decisión

- **Tres funciones nuevas en el catálogo:** `promedios`, `exportar_excel` y `avisos`. `avisos_urgentes` sigue aparte: es el WhatsApp del sistema para los urgentes, y solo aplica si el grupo ya puede enviar avisos.
- **La API decide.** La matriz de calificaciones que ve el docente (`VerMatrizCalificacionesCasoUso`) llega sin `resultados` y con `conPromedios: false` si el grupo no tiene `promedios`. El Excel y crear avisos responden 403 `excel_requiere_pro` y `avisos_requieren_pro`. Las pantallas leen `conPromedios`, `conExcel` y `conAvisos` para mostrar el candado.
- **El cálculo no cambia.** Cierre, boletas y Excel usan la matriz completa (`MatrizCalificacionesCasoUso`), así una boleta y su Excel coinciden siempre (RN-056).
- **Nadie pierde lo que paga.** La migración agrega las tres funciones a todos los productos que ya existen, así quien paga o está en prueba sigue teniéndolas. El administrador puede quitarlas de un producto desde el panel.
- **Lo enviado no se esconde.** En un grupo Gratis los avisos ya enviados siguen en la pestaña Avisos y en el portal de tutores. Al pasar a Gratis no se borra nada (RN-085).

## Consecuencias

- Un grupo Gratis ve sus calificaciones sin promedios. Como la prueba Pro empieza en el primer cierre (RN-081), el docente ve promedios por primera vez en ese cierre o al contratar.
- La tabla calcula en el dispositivo para trabajar sin conexión. Con `conPromedios: false` no calcula ni muestra promedios. Quien guarde la matriz y la recalcule por su cuenta podría verlos: es un candado de producto, no un secreto. Las calificaciones son del docente.
- La exportación de todos los datos de la cuenta (RF-008, derechos ARCO) no cambia y sigue siendo gratuita.
- El porcentaje de asistencia del historial no es un promedio de calificaciones y sigue en Gratis.
