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
| `POST /api/v1/boletas/{id}/corregir` | Genera nueva versión con los datos actuales |
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

## Fuera de alcance

Formato oficial de la SEP, firma electrónica, boleta anual consolidada.
