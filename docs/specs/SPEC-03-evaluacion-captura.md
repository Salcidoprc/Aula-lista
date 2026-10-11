# SPEC-03: Evaluación y captura

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-030 a RF-039, RNF-010, RNF-014

## Objetivo

Que capturar una calificación tome un toque y que el docente nunca pierda lo capturado, aunque no tenga señal.

## Historias de usuario

- Como docente, quiero definir mis rubros (tareas 30%, proyecto 40%, examen 30%) una vez y copiarlos a otros grupos.
- Como docente, quiero aplicar las actividades que necesite en cada rubro (por ejemplo 5 actividades en la unidad) y que se promedien solas para dar el porcentaje del rubro.
- Como docente, quiero capturar las calificaciones de una tarea para todo el grupo en un minuto desde el celular.
- Como docente, quiero crear los rubros que yo decida (asistencia, prácticas, participaciones, tareas, actividades, exámenes, exposición) y elegir su tipo.
- Como docente, quiero que un rubro como Exposición tenga criterios propios (información, presentación, dominio del tema) con su porcentaje.
- Como docente, quiero armar equipos en mi grupo y calificar una actividad por equipo, o a cada integrante por separado si así lo decido.
- Como docente, quiero anotar en el momento que un alumno participó o se distrajo, para usarlo al cerrar el periodo.

## Escala y cálculo

| ID | Regla |
| --- | --- |
| RN-030 | La escala es de 0 a 10 con hasta dos decimales (por ejemplo 8.75). Cada grupo define la calificación mínima reportable (por defecto 5) y la aprobatoria (por defecto 6). |
| RN-031 | Los rubros se definen por periodo (unidad del grupo). Los pesos de los rubros de un periodo deben sumar exactamente 100. No se puede iniciar el cierre si no suman 100. |
| RN-032 | Un rubro de tipo actividades tiene de 0 a N actividades (sin límite práctico) y todas pesan igual dentro del rubro. Promedio del rubro = media aritmética de sus actividades calificadas. "Exento" no cuenta; "No entregó" cuenta como la calificación mínima del grupo; una celda vacía no cuenta. Ejemplo: Actividades vale 30% y el alumno tiene 8, 9, 10, 7 y 6 en sus 5 actividades; el promedio del rubro es 8 y aporta 8 × 30 / 100 = 2.4 a la calificación de la unidad. |
| RN-033 | Calificación del periodo = suma de (promedio del rubro × peso) / 100, solo con rubros que tienen al menos una actividad calificada. Si un rubro no tiene datos, su peso se reparte proporcionalmente entre los demás y se muestra una advertencia. |
| RN-034 | Ninguna calificación del periodo puede ser menor que la mínima reportable: si el cálculo da menos, se reporta la mínima. |
| RN-035 | Los cálculos se hacen con decimales exactos (`decimal` en .NET, `numeric` en Postgres) sin redondear en pasos intermedios. Promedios de rubro y calificación del periodo se muestran con 2 decimales. La calificación final (la de la boleta) usa la configuración del grupo: decimales (0, 1 o 2; por defecto 2) y sentido del redondeo (hacia arriba, hacia abajo o al más cercano con medio hacia arriba; por defecto al más cercano). Ejemplo: 7.456 con 1 decimal da 7.5 hacia arriba, 7.4 hacia abajo y 7.5 al más cercano. La mínima reportable (RN-034) se aplica después de redondear. |
| RN-036 | El cálculo vive en el dominio del backend y se replica en el frontend para mostrarlo sin conexión; ambos usan los mismos casos de prueba (archivo compartido de vectores de prueba en `apps/api/tests/vectores-calculo.json`). |
| RN-037 | Durante el cierre (periodo `en_cierre`) los pesos y rubros no se editan; las calificaciones sí, y la observación del alumno afectado se marca "datos cambiaron, revisa". |
| RN-038 | Un periodo `cerrado` es de solo lectura. Reabrirlo requiere confirmación y queda en auditoría. |
| RN-039 | Cada rubro tiene un tipo: `actividades` (por defecto; se promedia con RN-032) o `asistencia` (se calcula con las tomas de asistencia de las fechas de la unidad, SPEC-10 RN-103 y RN-104). Ambos entran igual al cálculo de RN-033. Una actividad solo puede tener fecha dentro de la unidad del periodo. |

## Rubros, criterios y equipos

| ID | Regla |
| --- | --- |
| RN-110 | Los rubros son libres: el docente crea los que quiera, con el nombre que quiera, hasta 20 por periodo. Al crearlos se sugieren nombres comunes (Tareas, Actividades, Prácticas, Participaciones, Exámenes, Proyecto, Exposición, Asistencia) y el tipo que les corresponde. |
| RN-111 | Un rubro de tipo `actividades` puede tener criterios (subrubros): de 2 a 10, cada uno con nombre y peso; los pesos de los criterios suman 100. Todas las actividades de ese rubro se califican por criterio, de 0 a 10. Un rubro de tipo `asistencia` no tiene criterios. |
| RN-112 | Calificación de una actividad con criterios = suma de (calificación del criterio × peso) / 100. Si falta algún criterio, se calcula con los calificados y sus pesos se reparten proporcionalmente (como RN-033); si no hay ninguno, la celda está vacía. "No entregó" y "Exento" se marcan para la actividad completa. Ejemplo: Exposición con Información 40%, Presentación 30% y Dominio del tema 30%; un alumno con 9, 8 y 10 obtiene 3.6 + 2.4 + 3.0 = 9.0 en esa exposición. Esa calificación entra al promedio del rubro con RN-032. |
| RN-113 | Los criterios se editan como los rubros: libres hasta iniciar el cierre (RN-037). Cambiar pesos recalcula las actividades; borrar un criterio con calificaciones pide confirmación. Copiar rubros (RF-031) copia también sus criterios. |
| RN-114 | El docente arma equipos dentro del grupo, con nombre e integrantes. Un alumno está como máximo en un equipo a la vez. Puede reorganizarlos cuando quiera. |
| RN-115 | Cada actividad se califica de forma `individual` (por defecto) o `por_equipo`. Por equipo, el docente captura una calificación (o sus criterios) por equipo y se copia a cada integrante. Después puede ajustar a un integrante por separado; esa celda queda marcada como ajustada y ya no cambia al recalificar al equipo. Un alumno sin equipo se califica individualmente. |
| RN-116 | Una actividad por equipo guarda la composición de los equipos con la que se calificó. Reorganizar los equipos después no cambia las calificaciones ya capturadas. |
| RN-117 | El cálculo de promedios no cambia: cada alumno siempre tiene su propia calificación por actividad, venga de captura individual, de su equipo o de un ajuste. |

## Notas por alumno

- Texto libre de hasta 1,000 caracteres, con fecha y etiqueta opcional (fortaleza, área de mejora, conducta).
- El dictado por voz usa el micrófono del teclado del sistema operativo; la app no graba audio.
- Las notas son privadas del docente: nunca se muestran a tutores. Solo alimentan el borrador de observación.

## Captura

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

- **Vista por actividad (celular):** lista de alumnos con un campo numérico grande por alumno; al escribir y confirmar pasa al siguiente. Botones rápidos 10, 9, 8, 7, 6, 5, "No entregó" y "Exento". Si el rubro tiene criterios, cada alumno muestra un campo por criterio y su resultado calculado. Si la actividad es por equipo, la lista muestra equipos con sus integrantes; al tocar un integrante se ajusta solo a él.
- **Rubros del periodo:** lista de rubros con tipo, peso y criterios; suma de pesos visible en todo momento.
- **Equipos del grupo:** crear equipos, mover alumnos entre equipos y ver quién no tiene equipo.
- **Vista de tabla (computadora):** alumnos por actividades con navegación por teclado (flechas, Enter, Tab) y pegado desde Excel.
- **Vista por alumno:** desde la ficha, sus calificaciones por rubro, promedio actual y notas.
- Cada celda guarda al salir del campo, en IndexedDB primero y luego en el servidor. No hay botón "Guardar".
- Indicador de sincronización siempre visible (ver `02-arquitectura.md`, Sincronización sin conexión).

## API

| Método y ruta | Descripción |
| --- | --- |
| `GET` / `PUT /api/v1/periodos/{id}/rubros` | Lee y reemplaza rubros (valida RN-031 solo al iniciar cierre; permite guardar borradores) |
| `POST /api/v1/periodos/{id}/rubros/copiar` | Copia rubros desde otro periodo |
| `GET` / `POST /api/v1/rubros/{id}/actividades` | Lista y crea actividades |
| `PUT` / `DELETE /api/v1/actividades/{id}` | Edita o borra (borrar pide confirmación si tiene calificaciones) |
| `GET /api/v1/periodos/{id}/calificaciones` | Matriz completa del periodo con promedios calculados |
| `PUT /api/v1/calificaciones` | Lote de celdas `{actividadId, alumnoId o equipoId, valor o criterios[], marca, version}` con `Idempotency-Key`; por equipo escribe a cada integrante no ajustado (RN-115) |
| `GET` / `PUT /api/v1/grupos/{id}/equipos` | Lee y reemplaza los equipos del grupo (RN-114) |
| `GET` / `POST /api/v1/alumnos/{id}/notas` | Lista y crea notas |
| `POST /api/v1/sincronizacion` | Recibe la cola sin conexión (ver arquitectura) |

## Casos borde

- Valor fuera de rango (por ejemplo 11 o -1): se rechaza en la UI y en la API con `calificacion_fuera_de_rango`.
- Dos dispositivos editan la misma celda sin conexión: gana la última que llega al servidor; el otro dispositivo ve el valor actualizado al sincronizar.
- Se borra una actividad con calificaciones: se pide confirmación y se recalculan promedios.
- El docente sin conexión por más de 7 días: la cola se conserva; se muestra una advertencia a partir del día 3.

## Criterios de aceptación

- Dado un rubro Exposición con criterios 40/30/30, cuando un alumno obtiene 9, 8 y 10, entonces su calificación en la actividad es 9.00.
- Dados criterios que suman 90, cuando el docente guarda el rubro, entonces se rechaza con `pesos_criterios_no_suman_100`.
- Dada una actividad por equipo y un equipo de 4 integrantes, cuando el docente captura 9 al equipo, entonces los 4 tienen 9; si luego ajusta a uno con 7 y recalifica al equipo con 8, entonces 3 tienen 8 y el ajustado conserva 7.
- Dada una actividad por equipo ya calificada, cuando el docente reorganiza los equipos, entonces las calificaciones de esa actividad no cambian.

- Dados rubros 30/40/30 y calificaciones conocidas, cuando se calcula el periodo, entonces el resultado coincide con todos los casos del archivo de vectores de prueba, en backend y frontend.
- Dado un rubro Actividades de 30% con 5 actividades calificadas 8, 9, 10, 7 y 6, cuando se calcula, entonces el promedio del rubro es 8.00 y aporta 2.40 a la unidad.
- Dado un rubro de tipo asistencia, cuando el docente abre la captura, entonces no puede crear actividades en él y ve el porcentaje calculado de cada alumno.
- Dado un docente sin conexión, cuando captura 30 calificaciones y cierra la app, entonces al reabrirla con conexión las 30 llegan al servidor sin duplicados.
- Dados pesos que suman 95, cuando el docente intenta iniciar el cierre, entonces se rechaza con `pesos_no_suman_100`.
- Dada una celda marcada "No entregó" en un grupo con mínima 5, entonces cuenta como 5 en el promedio del rubro.
- Dada la vista por actividad en un Android de gama baja, entonces capturar 30 calificaciones con los botones rápidos toma menos de 90 segundos en prueba de usabilidad.

## Fuera de alcance

Ponderación por actividad dentro de un rubro, escalas cualitativas (A, B, C), evaluación por campos formativos de la NEM (pregunta abierta), rúbricas con descriptores.
