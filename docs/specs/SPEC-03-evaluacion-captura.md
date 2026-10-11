# SPEC-03: Evaluación y captura

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-030 a RF-037, RNF-010, RNF-014

## Objetivo

Que capturar una calificación tome un toque y que el docente nunca pierda lo capturado, aunque no tenga señal.

## Historias de usuario

- Como docente, quiero definir mis rubros (tareas 30%, proyecto 40%, examen 30%) una vez y copiarlos a otros grupos.
- Como docente, quiero aplicar las actividades que necesite en cada rubro (por ejemplo 5 actividades en la unidad) y que se promedien solas para dar el porcentaje del rubro.
- Como docente, quiero capturar las calificaciones de una tarea para todo el grupo en un minuto desde el celular.
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

## Notas por alumno

- Texto libre de hasta 1,000 caracteres, con fecha y etiqueta opcional (fortaleza, área de mejora, conducta).
- El dictado por voz usa el micrófono del teclado del sistema operativo; la app no graba audio.
- Las notas son privadas del docente: nunca se muestran a tutores. Solo alimentan el borrador de observación.

## Captura

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

- **Vista por actividad (celular):** lista de alumnos con un campo numérico grande por alumno; al escribir y confirmar pasa al siguiente. Botones rápidos 10, 9, 8, 7, 6, 5, "No entregó" y "Exento".
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
| `PUT /api/v1/calificaciones` | Lote de celdas `{actividadId, alumnoId, valor, marca, version}` con `Idempotency-Key` |
| `GET` / `POST /api/v1/alumnos/{id}/notas` | Lista y crea notas |
| `POST /api/v1/sincronizacion` | Recibe la cola sin conexión (ver arquitectura) |

## Casos borde

- Valor fuera de rango (por ejemplo 11 o -1): se rechaza en la UI y en la API con `calificacion_fuera_de_rango`.
- Dos dispositivos editan la misma celda sin conexión: gana la última que llega al servidor; el otro dispositivo ve el valor actualizado al sincronizar.
- Se borra una actividad con calificaciones: se pide confirmación y se recalculan promedios.
- El docente sin conexión por más de 7 días: la cola se conserva; se muestra una advertencia a partir del día 3.

## Criterios de aceptación

- Dados rubros 30/40/30 y calificaciones conocidas, cuando se calcula el periodo, entonces el resultado coincide con todos los casos del archivo de vectores de prueba, en backend y frontend.
- Dado un rubro Actividades de 30% con 5 actividades calificadas 8, 9, 10, 7 y 6, cuando se calcula, entonces el promedio del rubro es 8.00 y aporta 2.40 a la unidad.
- Dado un rubro de tipo asistencia, cuando el docente abre la captura, entonces no puede crear actividades en él y ve el porcentaje calculado de cada alumno.
- Dado un docente sin conexión, cuando captura 30 calificaciones y cierra la app, entonces al reabrirla con conexión las 30 llegan al servidor sin duplicados.
- Dados pesos que suman 95, cuando el docente intenta iniciar el cierre, entonces se rechaza con `pesos_no_suman_100`.
- Dada una celda marcada "No entregó" en un grupo con mínima 5, entonces cuenta como 5 en el promedio del rubro.
- Dada la vista por actividad en un Android de gama baja, entonces capturar 30 calificaciones con los botones rápidos toma menos de 90 segundos en prueba de usabilidad.

## Fuera de alcance

Ponderación por actividad dentro de un rubro, escalas cualitativas (A, B, C), evaluación por campos formativos de la NEM (pregunta abierta), rúbricas con descriptores.
