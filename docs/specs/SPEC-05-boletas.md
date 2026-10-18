# SPEC-05: Boletas

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-050 a RF-055, RNF-005, RNF-016

## Objetivo

Convertir lo aprobado en el cierre en una boleta PDF clara, publicarla para los tutores y conservar cada versión.

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-050 | Solo se genera boleta de alumnos activos con observación `aprobada`. |
| RN-051 | La boleta guarda una foto inmutable de los datos (`boleta.datos`): calificaciones por rubro, final, observación, docente, grupo y fecha. Cambios posteriores no alteran una boleta emitida. |
| RN-052 | Corregir una boleta publicada crea una versión nueva; la anterior queda como `reemplazada_por` y el tutor solo ve la vigente, con la leyenda "Versión corregida el dd/mm/aaaa". |
| RN-053 | Publicar requiere conexión y es una acción explícita del docente, por grupo completo o por alumnos seleccionados. |
| RN-054 | Al publicar se notifica a cada tutor vinculado por WhatsApp del sistema (plantilla `boleta_disponible`, cuenta para el límite, RN-086) y por push o correo si los tiene. Si se agotó el límite, el docente recibe la lista para compartir desde su WhatsApp. |
| RN-055 | La boleta de la app no sustituye documentos oficiales de la SEP; el PDF lo indica en el pie: "Reporte de evaluación del docente. No es un documento oficial." |
| RN-056 | La exportación a Excel incluye por alumno: número de lista, nombre, promedio por rubro, calificación final y observación aprobada, en el orden de la lista. |

## Contenido del PDF

1. Encabezado: nombre de la escuela, CCT si existe, ciclo escolar, periodo.
2. Datos: alumno, grado y grupo, materia (si aplica), docente.
3. Tabla de rubros: nombre, peso, promedio; calificación final destacada.
4. Observación del docente.
5. Pie: fecha de emisión, versión, leyenda de RN-055 y código corto de verificación.

Tamaño carta, legible al imprimir en blanco y negro, menos de 200 KB por alumno. Generado con QuestPDF en el backend.

## API

| Método y ruta | Descripción |
| --- | --- |
| `POST /api/v1/periodos/{id}/boletas/generar` | Genera boletas (todos o `alumnoIds`); job en segundo plano |
| `GET /api/v1/periodos/{id}/boletas` | Lista con estado y versión |
| `GET /api/v1/boletas/{id}/pdf` | PDF de un alumno (docente dueño o tutor vinculado) |
| `GET /api/v1/periodos/{id}/boletas/pdf` | PDF combinado del grupo |
| `POST /api/v1/periodos/{id}/boletas/publicar` | Publica y encola notificaciones |
| `POST /api/v1/boletas/{id}/corregir` | Genera nueva versión con los datos actuales y la publica |
| `GET /api/v1/periodos/{id}/exportacion.xlsx` | Exportación a Excel |

## Pantalla

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

**Boletas y envío:** vista previa del PDF por alumno, descarga del PDF del grupo, botón "Publicar y avisar a tutores", resumen previo con cuántos tutores recibirán WhatsApp, cuántos push o correo y cuántos no tienen contacto. Si el límite de WhatsApp se agotó, una lista con botón "Compartir por WhatsApp" por tutor.

## Criterios de aceptación

- Dado un grupo con 30 observaciones aprobadas, cuando el docente genera boletas, entonces hay 30 PDFs en menos de 60 segundos y un PDF combinado.
- Dada una boleta publicada, cuando el docente corrige una calificación y vuelve a emitir, entonces el tutor ve solo la versión nueva con la leyenda de corrección y la anterior se conserva.
- Dado un alumno sin tutores, cuando se publica, entonces su boleta queda disponible para descarga del docente y el resumen lo marca "sin contacto".
- Dada la exportación a Excel de un periodo, entonces los promedios coinciden con los de las boletas.
- Dada una publicación, entonces queda registro en auditoría con docente, periodo y número de boletas.

## Decisiones de implementación (H4.2)

- **Avisos al publicar (H5.2, RN-054):** el botón dice "Publicar y avisar a tutores (n)" y la hoja de confirmación cuenta tutores con WhatsApp, correo, notificación y a cuántos se la compartirá el docente. Cada tutor recibe un solo aviso aunque tenga varios hijos publicados (el WhatsApp nombra a todos). Una versión corregida (RN-052) no vuelve a avisar. Si a un tutor no le llega por ningún canal, al publicar aparece la hoja "Compártela tú" con un `wa.me` por tutor (`POST /boletas/{id}/tutores/{tutorId}/compartir`).
- **Foto y PDF por separado:** generar guarda la foto (`boleta.datos`) al instante y encola el PDF en Hangfire (3 reintentos). Mientras el PDF no existe la boleta está "preparando"; si falla tras los reintentos vuelve a "sin boleta" con la marca "No se pudo preparar" y generar lo reintenta. La pantalla consulta cada 2 s mientras haya alguna preparándose y muestra el avance real de la tanda.
- **Desactualizada:** una boleta sin publicar cuya foto ya no coincide con los datos actuales se marca "Desactualizada"; generar la rehace en la misma versión y publicar responde `boletas_desactualizadas` (409) hasta rehacerla. Si la observación se reabrió, la boleta sin publicar vuelve a "Falta aprobar la observación".
- **Corregir una publicada (RN-052):** se reabre la observación (ahora también desde `publicada`) o se cambia una calificación con la unidad en cierre; la fila dice "Cambió después de publicar" y "Emitir versión corregida" crea la versión n+1 ya publicada. Corregir no está disponible con la unidad cerrada: primero se reabre (RN-038).
- **Cierre de la unidad (RN-048):** se cierra sola cuando todas las boletas vigentes están publicadas; también a mano con "Dar por cerrada la unidad" (`POST /api/v1/periodos/{id}/cierre/cerrar`), con una confirmación que dice cuántos alumnos quedan sin boleta publicada. Reabrir (`.../cierre/reabrir`) pide confirmación y queda en auditoría; las boletas publicadas siguen a la vista hasta emitir su versión corregida.
- **Auditoría (RNF-005):** `boletas_publicadas`, `boleta_corregida`, `calificaciones_exportadas`, `periodo_cerrado` y `periodo_reabierto`; la nueva columna `auditoria.cantidad` guarda el número de boletas o filas.
- **PDF:** carta, solo tinta y grises; Lexend (estáticas generadas de la fuente variable) y "Aula Cifras" (dígitos tabulares de Figtree) embebidas, ambas OFL con su licencia junto a los archivos. El código de verificación son 8 caracteres en base 32 de Crockford. Fechas en la zona horaria del docente.
- **Vista previa:** el PDF de un alumno se abre en una pestaña nueva del navegador (en Android, el visor de PDF); el del grupo y el Excel se descargan.

## Fuera de alcance

Formato oficial de la SEP, firma electrónica, boleta anual consolidada.
