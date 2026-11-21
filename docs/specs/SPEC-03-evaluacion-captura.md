# SPEC-03: Evaluación y captura

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-030 a RF-039, RF-110, RF-111, RNF-010, RNF-014

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
- Como docente, quiero buscar rápido a un alumno en clase y darle una participación que le sume puntos extra a su calificación del periodo, con el valor que yo elija para el grupo.

## Escala y cálculo

| ID | Regla |
| --- | --- |
| RN-030 | La escala es de 0 a 10 con hasta dos decimales (por ejemplo 8.75). Cada grupo define la calificación mínima reportable (por defecto 5) y la aprobatoria (por defecto 6). |
| RN-031 | Los rubros se definen por periodo (unidad del grupo). Los pesos de los rubros de un periodo deben sumar exactamente 100. No se puede iniciar el cierre si no suman 100. |
| RN-032 | Un rubro de tipo actividades tiene de 0 a N actividades (sin límite práctico) y todas pesan igual dentro del rubro. Promedio del rubro = media aritmética de sus actividades calificadas. "Exento" no cuenta; "No entregó" cuenta como la calificación mínima del rubro (RN-118); una celda vacía no cuenta. Ejemplo: Actividades vale 30% y el alumno tiene 8, 9, 10, 7 y 6 en sus 5 actividades; el promedio del rubro es 8 y aporta 8 × 30 / 100 = 2.4 a la calificación de la unidad. |
| RN-033 | Calificación del periodo = suma de (promedio del rubro × peso) / 100, solo con rubros que tienen al menos una actividad calificada. Si un rubro no tiene datos, su peso se reparte proporcionalmente entre los demás y se muestra una advertencia. |
| RN-034 | Ninguna calificación del periodo puede ser menor que la mínima reportable: si el cálculo da menos, se reporta la mínima. |
| RN-035 | Los cálculos se hacen con decimales exactos (`decimal` en .NET, `numeric` en Postgres) sin redondear en pasos intermedios. Promedios de rubro y calificación del periodo se muestran con 2 decimales. La calificación final (la de la boleta) usa la configuración del grupo: decimales (0, 1 o 2; por defecto 2) y sentido del redondeo (hacia arriba, hacia abajo o al más cercano con medio hacia arriba; por defecto al más cercano). Ejemplo: 7.456 con 1 decimal da 7.5 hacia arriba, 7.4 hacia abajo y 7.5 al más cercano. La mínima reportable (RN-034) se aplica después de redondear. |
| RN-036 | El cálculo vive en el dominio del backend y se replica en el frontend para mostrarlo sin conexión; ambos usan los mismos casos de prueba (archivo compartido de vectores de prueba en `apps/api/tests/vectores-calculo.json`). |
| RN-037 | Durante el cierre (periodo `en_cierre`) los pesos y rubros no se editan; las calificaciones sí, y la observación del alumno afectado se marca "datos cambiaron, revisa". |
| RN-038 | Un periodo `cerrado` es de solo lectura. Reabrirlo requiere confirmación y queda en auditoría. |
| RN-039 | Cada rubro tiene un tipo del catálogo que administra el panel de administración (SPEC-09, ADR-0012). Cada tipo usa un comportamiento de cálculo y captura. En el MVP hay dos comportamientos: `actividades` (por defecto; se promedia con RN-032) y `asistencia` (se calcula con las tomas de asistencia de las fechas de la unidad, SPEC-10 RN-103 y RN-104). Todos entran igual al cálculo de RN-033. Una actividad solo puede tener fecha dentro de la unidad del periodo. |

## Rubros, criterios y equipos

| ID | Regla |
| --- | --- |
| RN-110 | Los rubros son libres: el docente crea los que quiera, con el nombre que quiera, hasta 20 por periodo, y elige su tipo entre los tipos activos del catálogo. Al crearlos se sugieren los nombres comunes de cada tipo (Tareas, Actividades, Prácticas, Participaciones, Exámenes, Proyecto, Exposición, Asistencia). |
| RN-111 | Un rubro con comportamiento `actividades` puede tener criterios (subrubros): de 2 a 10, cada uno con nombre y peso; los pesos de los criterios suman 100. Todas las actividades de ese rubro se califican por criterio, según la forma y la mínima de cada criterio (RN-119). Un rubro con comportamiento `asistencia` no tiene criterios. |
| RN-112 | Calificación de una actividad con criterios = suma de (calificación del criterio × peso) / 100. Si falta algún criterio, se calcula con los calificados y sus pesos se reparten proporcionalmente (como RN-033); si no hay ninguno, la celda está vacía. "No entregó" y "Exento" se marcan para la actividad completa. Ejemplo: Exposición con Información 40%, Presentación 30% y Dominio del tema 30%; un alumno con 9, 8 y 10 obtiene 3.6 + 2.4 + 3.0 = 9.0 en esa exposición. Esa calificación entra al promedio del rubro con RN-032. |
| RN-113 | Los criterios se editan como los rubros: libres hasta iniciar el cierre (RN-037). Cambiar pesos recalcula las actividades; borrar un criterio con calificaciones pide confirmación. Copiar rubros (RF-031) copia también sus criterios. |
| RN-114 | El docente arma equipos dentro del grupo, con nombre e integrantes. Un alumno está como máximo en un equipo a la vez. Puede reorganizarlos cuando quiera. |
| RN-115 | Cada actividad se califica de forma `individual` (por defecto) o `por_equipo`. Por equipo, el docente captura una calificación (o sus criterios) por equipo y se copia a cada integrante. Después puede ajustar a un integrante por separado; esa celda queda marcada como ajustada y ya no cambia al recalificar al equipo. Un alumno sin equipo se califica individualmente. |
| RN-116 | Una actividad por equipo guarda la composición de los equipos con la que se calificó. Reorganizar los equipos después no cambia las calificaciones ya capturadas. |
| RN-117 | El cálculo de promedios no cambia: cada alumno siempre tiene su propia calificación por actividad, venga de captura individual, de su equipo o de un ajuste. |
| RN-118 | Al agregar un rubro de actividades el docente elige su forma de calificar y su calificación mínima. Forma: `numerica` (de la mínima a 10, con hasta 2 decimales; por defecto) o `cumplimiento` (✅ cumplió o ❌ no cumplió). Mínima: de 0 a 10, por defecto la mínima reportable del grupo (RN-030). En `cumplimiento`, ✅ vale 10 y ❌ vale la mínima. En `numerica` no se acepta un valor menor que la mínima (`calificacion_menor_a_minima`). "No entregó" vale la mínima en ambas formas y "Exento" no cuenta. Ejemplo: Tareas por cumplimiento con mínima 5 y un alumno con 4 ✅ y 1 ❌ da (10 × 4 + 5) / 5 = 9.00. |
| RN-119 | Si el rubro tiene criterios, cada criterio elige su propia forma de calificar y su propia mínima con las mismas reglas de RN-118, y la actividad se calcula con RN-112. Ejemplo: Exposición con Información 40% numérica, Presentación 30% numérica y Material de apoyo 30% por cumplimiento con mínima 0; con 9, 8 y ❌ da 3.6 + 2.4 + 0 = 6.00. |
| RN-120 | La forma de calificar de un rubro o criterio solo se cambia mientras no tenga calificaciones. La mínima se puede cambiar hasta iniciar el cierre (RN-037) y recalcula las celdas "No entregó" y ❌; si hay valores numéricos menores que la nueva mínima, se avisa cuántos son y no se guarda hasta corregirlos. El tipo de rubro del catálogo puede sugerir una forma por defecto (por ejemplo Tareas por cumplimiento). |

## Notas por alumno

- Texto libre de hasta 1,000 caracteres, con fecha y etiqueta opcional (fortaleza, área de mejora, conducta).
- El dictado por voz usa el micrófono del teclado del sistema operativo; la app no graba audio.
- Las notas son privadas del docente: nunca se muestran a tutores. Solo alimentan el borrador de observación.

## Participaciones extra

Puntos que el docente da en clase y que se suman encima de la calificación ordinaria del periodo (ADR-0019). No son un rubro y no tienen peso; el tipo de rubro "Participaciones" (RN-110) sigue existiendo para quien quiera calificarlas como tareas.

| ID | Regla |
| --- | --- |
| RN-121 | Cada grupo tiene un valor por participación de 0.01 a 1.00 punto, con hasta 2 decimales; por defecto 0.10. Se cambia en la configuración del grupo o desde la pantalla de participaciones. Un valor fuera de rango se rechaza con `configuracion_calificacion_invalida`. |
| RN-122 | Una participación es un registro de un alumno activo en una fecha. La fecha decide la unidad (periodo) a la que pertenece; fuera de toda unidad se rechaza con `fecha_fuera_de_unidad`. Se pueden dar varias al mismo alumno el mismo día. Su id lo genera el cliente: reenviarla no la duplica. |
| RN-123 | Puntos extra del periodo = participaciones del alumno en la unidad × valor vigente del grupo (RN-121). Cambiar el valor recalcula los promedios; una boleta emitida no cambia (RN-051). |
| RN-124 | Calificación con extra: a la calificación ordinaria (RN-033, ya sin bajar de la mínima, RN-034) se le suman los puntos extra y el resultado nunca pasa de 10. Después se redondea como dice RN-035. Sin puntos extra, el resultado es el de siempre. Si el alumno no tiene calificación ordinaria (ningún rubro con datos), las participaciones no crean una. Ejemplo: ordinaria 8.10 con 4 participaciones de 0.10 da 8.50; ordinaria 9.80 con 5 participaciones de 0.10 da 10.00. |
| RN-125 | Las participaciones se dan y se quitan mientras la unidad admita captura: también durante el cierre (la observación del alumno se marca "datos cambiaron", RN-037) y nunca con la unidad cerrada (`periodo_cerrado`) ni el grupo archivado (`grupo_archivado`). |
| RN-126 | La boleta guarda en su foto cuántas participaciones tuvo el alumno y cuántos puntos sumaron (renglón "Participaciones extra" bajo los rubros). El Excel agrega las columnas "Participaciones" y "Puntos extra". El tutor ve el renglón de la boleta publicada, nunca las participaciones sueltas por fecha (RN-064). |

**Pantalla Participaciones (pestaña del grupo):** el día (hoy por defecto, con flechas y calendario como la asistencia), el valor de cada participación con "Cambiar", un buscador por nombre o número de lista y la lista del grupo. Tocar la fila de un alumno le suma una participación; la fila muestra cuántas lleva hoy y cuántos puntos suma en la unidad. "Quitar una" aparece en la fila cuando el alumno tiene participaciones ese día. En computadora, escribir en el buscador y presionar Enter suma una al único alumno que coincide. Nada se anima al sumar (reglas/07, acciones repetidas): solo cambian el número y el fondo de la fila. Funciona sin conexión como la asistencia (RF-111).

**Dónde se ven los puntos:** la pantalla de participaciones muestra a todos los planes cuántas lleva cada alumno y sus puntos (participaciones × valor), porque es la captura. La calificación con los puntos sumados sale donde salen los promedios (Pro, RN-097): en la tabla, una columna "Extra" antes de la calificación de la unidad cuando alguien tiene participaciones; en la ficha del alumno, un renglón "Participaciones extra" bajo los rubros.

## Captura

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

- **Vista por actividad (celular):** lista de alumnos con un campo numérico grande por alumno; al escribir y confirmar pasa al siguiente. Botones rápidos 10, 9, 8, 7, 6, 5, "No entregó" y "Exento". En un rubro por cumplimiento cada alumno tiene dos botones grandes, ✅ y ❌, y un botón "Todos cumplieron" para marcar al grupo de una vez. Si el rubro tiene criterios, cada alumno muestra un campo por criterio (numérico o ✅/❌, según el criterio) y su resultado calculado. Si la actividad es por equipo, la lista muestra equipos con sus integrantes; al tocar un integrante se ajusta solo a él.
- **Rubros del periodo:** lista de rubros con tipo, peso y criterios; suma de pesos visible en todo momento.
- **Equipos del grupo:** crear equipos, mover alumnos entre equipos y ver quién no tiene equipo.
- **Vista de tabla (computadora):** alumnos por actividades con navegación por teclado (flechas, Enter, Tab) y pegado desde Excel.
- **Vista por alumno:** desde la ficha, sus calificaciones por rubro, promedio actual y notas.
- Cada celda guarda al salir del campo, en IndexedDB primero y luego en el servidor. No hay botón "Guardar".
- Indicador de sincronización siempre visible (ver `02-arquitectura.md`, Sincronización sin conexión).

## API

| Método y ruta | Descripción |
| --- | --- |
| `GET /api/v1/tipos-rubro` | Catálogo de tipos activos con su comportamiento, forma sugerida y nombres sugeridos (RN-039, RN-110) |
| `GET` / `PUT /api/v1/periodos/{id}/rubros` | Lee y reemplaza rubros (valida RN-031 solo al iniciar cierre; permite guardar borradores). Si quita rubros o criterios con calificaciones responde 409 `borrado_requiere_confirmacion` hasta recibir `confirmarBorrado: true`; en cierre o cerrado, 409 `periodo_en_cierre` o `periodo_cerrado` |
| `POST /api/v1/periodos/{id}/rubros/copiar` | Copia rubros y criterios desde otro periodo del docente (`desdePeriodoId`); sin actividades. Si el destino ya tiene rubros, 409 `periodo_con_rubros` |
| `GET` / `POST /api/v1/rubros/{id}/actividades` | Lista y crea actividades |
| `PUT` / `DELETE /api/v1/actividades/{id}` | Edita o borra; borrar con calificaciones responde 409 `borrado_requiere_confirmacion` salvo con `?confirmar=true` |
| `GET /api/v1/periodos/{id}/calificaciones` | Matriz completa del periodo con promedios calculados, los equipos de hoy y la composición guardada de cada actividad por equipo. Los promedios son de Pro (RN-097, SPEC-08): en un grupo Gratis llega con `resultados` vacío y `conPromedios: false`, y la tabla y la ficha del alumno muestran lo capturado sin promedios |
| `PUT /api/v1/calificaciones` | Lote de celdas `{actividadId, alumnoId o equipoId, valor o criterios[], marca, version}` con `Idempotency-Key`; por equipo (`alumnoId` vacío) escribe a cada integrante no ajustado (RN-115) y la primera vez guarda la composición (RN-116). Calificar por separado a un integrante marca su celda `ajustada`; vaciarla la regresa al equipo |
| `GET` / `PUT /api/v1/grupos/{id}/equipos` | Lee y reemplaza los equipos del grupo (RN-114): cada equipo con `id` se conserva, sin `id` es nuevo y el que falta se quita. Nombres distintos de hasta 40 caracteres, hasta 30 equipos; un alumno en dos equipos responde `equipos_invalidos` |
| `GET` / `POST /api/v1/alumnos/{id}/notas` | Lista y crea notas `{id, periodoId, texto, etiqueta, fecha}`. El `id` lo genera el cliente: reenviar la misma nota no la duplica |
| `GET /api/v1/grupos/{id}/participaciones` | Participaciones del grupo `{id, alumnoId, fecha}`, con `desde` y `hasta` opcionales (inclusive) |
| `POST /api/v1/grupos/{id}/participaciones` | Da una participación `{id, alumnoId, fecha}` (RN-122); sin fecha, hoy. Un `id` que ya llegó responde la guardada |
| `DELETE /api/v1/participaciones/{id}` | Quita una participación (RN-125). Una que no existe o es ajena responde 404 |
| `POST /api/v1/sincronizacion` | Recibe la cola sin conexión (hasta 100 cambios con UUID único; ver arquitectura). Responde `aplicadas` y `rechazadas` con su código y la celda vigente |

Códigos de error de este módulo: `pesos_no_suman_100`, `pesos_criterios_no_suman_100`, `rubros_invalidos`, `criterios_invalidos`, `tipo_rubro_invalido`, `forma_invalida`, `minima_invalida`, `forma_con_calificaciones`, `borrado_requiere_confirmacion`, `periodo_en_cierre`, `periodo_cerrado`, `periodo_con_rubros`, `actividad_invalida`, `fecha_fuera_de_unidad`, `rubro_sin_actividades`, `calificacion_fuera_de_rango`, `calificacion_invalida`, `calificacion_menor_a_minima`, `calificaciones_menores_a_minima`, `marca_invalida`, `criterio_inexistente`, `celda_inexistente`, `equipos_invalidos`, `nota_invalida`, `participacion_invalida`, `alumno_dado_de_baja`, `grupo_con_calificaciones` (borrar grupo, SPEC-02) y `unidad_con_datos` (quitar una unidad con rubros, SPEC-02).

## Casos borde

- Valor fuera de rango (por ejemplo 11 o -1): se rechaza en la UI y en la API con `calificacion_fuera_de_rango`.
- Dos dispositivos editan la misma celda sin conexión: gana la última que llega al servidor; el otro dispositivo ve el valor actualizado al sincronizar.
- Se borra una actividad con calificaciones: se pide confirmación y se recalculan promedios.
- Se toca dos veces por error a un alumno en Participaciones: "Quitar una" borra la más reciente de ese día, también sin conexión. Si aún no se enviaba, solo se quita de la cola.
- Un alumno con muchas participaciones: el tope de 10 aplica a la calificación, no al conteo; las participaciones siguen registradas.
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
- Dada una celda marcada "No entregó" en un rubro con mínima 5, entonces cuenta como 5 en el promedio del rubro.
- Dado un rubro por cumplimiento con mínima 5, cuando un alumno tiene 4 ✅ y 1 ❌, entonces su promedio del rubro es 9.00.
- Dado un rubro numérico con mínima 5, cuando el docente captura 3, entonces se rechaza con `calificacion_menor_a_minima`.
- Dado un criterio por cumplimiento con mínima 0 y peso 30, cuando el alumno obtiene ❌, entonces ese criterio aporta 0 a la actividad.
- Dado un grupo con valor de participación 0.10 y un alumno con calificación ordinaria 8.10, cuando recibe 4 participaciones en la unidad, entonces su calificación del periodo es 8.50.
- Dado un alumno con ordinaria 9.80 y 5 participaciones de 0.10, entonces su calificación del periodo es 10.00 (RN-124).
- Dado un alumno con ordinaria 4.00, mínima 5 y 5 participaciones de 0.10, entonces su calificación del periodo es 5.50.
- Dado un alumno sin calificaciones ordinarias y 3 participaciones, entonces no tiene calificación del periodo.
- Dado un valor de participación de 1.50 o de 0, cuando el docente lo guarda, entonces se rechaza con `configuracion_calificacion_invalida`.
- Dado un docente sin conexión, cuando da 5 participaciones y quita 1, entonces al volver la red el servidor tiene 4 y ninguna duplicada.
- Dada una boleta generada de un alumno con 3 participaciones de 0.25, entonces la boleta y el Excel muestran 3 participaciones y 0.75 puntos extra.
- Dada una unidad cerrada, cuando el docente intenta dar una participación con fecha en ella, entonces se rechaza con `periodo_cerrado`.
- Dada la vista por actividad en un Android de gama baja, entonces capturar 30 calificaciones con los botones rápidos toma menos de 90 segundos en prueba de usabilidad.

## Fuera de alcance

Ponderación por actividad dentro de un rubro, escalas cualitativas (A, B, C), evaluación por campos formativos de la NEM (pregunta abierta), rúbricas con descriptores.
