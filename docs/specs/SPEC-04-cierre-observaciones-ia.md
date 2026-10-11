# SPEC-04: Cierre de periodo y observaciones con IA

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-040 a RF-046, RNF-004, RNF-007, RNF-013, RNF-022

## Objetivo

Que el docente cierre un grupo de 30 alumnos en menos de 1 hora: la IA escribe, el docente revisa y aprueba.

## Historias de usuario

- Como docente, quiero que la app me proponga la observación de cada alumno con base en sus calificaciones y mis notas.
- Como docente, quiero corregir o pedir otra versión sin reescribir todo.
- Como docente, quiero que las observaciones suenen como yo.

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-040 | Iniciar el cierre requiere: rubros que suman 100 y al menos una actividad calificada. Cambia el periodo a `en_cierre`. |
| RN-041 | El cierre (observaciones y boletas) requiere plan Pro en el grupo o prueba Pro vigente. Si el docente está en Gratis y no ha usado su prueba, iniciar el cierre le pide elegir el grupo de prueba y la activa en ese grupo (RN-081); si ya la usó, se le ofrece contratar Pro para ese grupo. En Gratis puede exportar sus calificaciones a Excel. |
| RN-042 | Ninguna observación llega a un tutor sin estado `aprobada` por el docente. |
| RN-043 | Regenerar reemplaza el borrador, no el texto aprobado. Hay máximo 5 regeneraciones por alumno y periodo. |
| RN-044 | Si cambian las calificaciones o notas de un alumno con observación aprobada y boleta no publicada, la observación vuelve a `borrador` con la marca "datos cambiaron, revisa". |
| RN-045 | Se guarda `borrador_ia` (primera versión generada) y `texto_final` para la métrica de aceptación (distancia de edición). |
| RN-046 | Longitud por defecto: 60 a 90 palabras. Opciones: breve (30 a 50), normal, detallada (100 a 140). |
| RN-047 | Tono: siempre respetuoso, en segunda o tercera persona según configuración, centrado en logros y en acciones concretas para mejorar. Nunca diagnósticos médicos o psicológicos, comparaciones con otros alumnos ni lenguaje que etiquete. |
| RN-048 | El periodo pasa a `cerrado` cuando todas las boletas del grupo están publicadas o el docente lo cierra manualmente. |

## Contrato con la IA

**Modelo:** `claude-haiku-5-5` por defecto, configurable por variable de entorno; nunca escrito en el código. Esfuerzo `low` explícito (el valor por defecto de Haiku 5.5 es `medium`).

**Entrada por alumno (después de seudonimizar, RN-019):**

```json
{
  "alumno": "Alumno 7F3K",
  "trato": "ella",
  "nivel": "primaria",
  "grado": 4,
  "materia": null,
  "periodo": "Trimestre 1",
  "calificacion_periodo": 8.4,
  "minima_aprobatoria": 6,
  "rubros": [
    {"nombre": "Tareas", "peso": 30, "promedio": 9.1, "no_entrego": 1},
    {"nombre": "Proyecto", "peso": 40, "promedio": 8.0, "no_entrego": 0}
  ],
  "notas": [
    {"fecha": "2026-09-18", "etiqueta": "fortaleza", "texto": "Explicó la fracción al equipo"},
    {"fecha": "2026-10-02", "etiqueta": "mejora", "texto": "Entrega tarde las tareas"}
  ],
  "estilo": {"longitud": "normal", "persona": "tercera", "ejemplos": ["..."]},
  "instruccion_extra": "más breve"
}
```

**Salida:** solo el texto de la observación, en español de México, sin encabezados ni nombre del alumno. El backend reemplaza el seudónimo si el modelo lo incluye y valida longitud.

**Prompt:** el prompt de sistema vive versionado en `apps/api/src/AulaLista.Infrastructure/Ia/Prompts/` y se cachea (prompt caching). Cambiarlo requiere correr la evaluación de calidad (ver `reglas/05-ia.md`).

**Errores:** si la respuesta se rechaza (`stop_reason` = `refusal`), excede longitud o falla, el alumno queda en `sin_generar` con mensaje "No se pudo generar, intenta de nuevo o escribe la observación". Los reintentos automáticos son 3 con espera exponencial.

**Ejecución:** un job por grupo procesa alumnos en paralelo con máximo 5 solicitudes simultáneas por docente. La UI muestra el avance en vivo (consulta cada 3 segundos o Server-Sent Events).

## API

| Método y ruta | Descripción |
| --- | --- |
| `POST /api/v1/periodos/{id}/cierre` | Inicia el cierre (RN-040) |
| `GET /api/v1/periodos/{id}/cierre` | Estado del cierre: conteos y estado por alumno |
| `POST /api/v1/periodos/{id}/observaciones/generar` | Encola generación para todos o para `alumnoIds` |
| `GET /api/v1/periodos/{id}/observaciones` | Lista de observaciones con estado |
| `PUT /api/v1/observaciones/{id}` | Guarda texto editado |
| `POST /api/v1/observaciones/{id}/regenerar` | Regenera con `instruccion` opcional |
| `POST /api/v1/observaciones/{id}/aprobar` | Aprueba |
| `POST /api/v1/observaciones/{id}/reabrir` | Reabre si la boleta no está publicada |
| `GET` / `PUT /api/v1/docente/estilo-observaciones` | Configuración de estilo (RF-043) |

## Pantallas

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

- **Cierre de periodo:** barra de avance (sin generar, borrador, aprobadas), botón "Generar observaciones", lista de alumnos con su estado y calificación.
- **Revisar observación:** calificaciones y notas del alumno arriba, texto editable abajo; botones Aprobar y siguiente, Regenerar (con sugerencias rápidas: más breve, más positiva, más específica), Escribir a mano.
- **Estilo:** longitud, persona gramatical y hasta 3 ejemplos.

## Criterios de aceptación

- Dado un grupo de 50 alumnos en Pro, cuando el docente pide generar, entonces todos los borradores están listos en menos de 3 minutos y la UI muestra el avance.
- Dado cualquier envío a la IA, entonces el contenido no incluye nombres, apellidos, teléfonos ni correos (prueba automática sobre el armado del prompt).
- Dado un grupo en plan Gratis de un docente que ya usó su prueba, cuando intenta iniciar el cierre, entonces se rechaza con `requiere_pro` y se le ofrece contratar Pro para ese grupo.
- Dada una observación aprobada, cuando cambia una calificación de ese alumno antes de publicar, entonces vuelve a borrador con la marca de revisión.
- Dada una regeneración número 6 del mismo alumno y periodo, entonces se rechaza con `limite_regeneraciones`.
- Dada la evaluación de calidad de 30 casos de prueba, entonces ninguna salida contiene diagnósticos, comparaciones con otros alumnos ni el seudónimo.

## Fuera de alcance

Observaciones por campo formativo de la NEM, traducción a lenguas indígenas, generación de planeación.
