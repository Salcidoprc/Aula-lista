# SPEC-10: Asistencia

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-100 a RF-103, RNF-014

## Objetivo

Que el docente pase lista en segundos desde el celular, aun sin señal, y que la asistencia de la unidad cuente como un rubro más de la calificación si así lo decide.

## Historias de usuario

- Como docente, quiero pasar lista cada día con un toque por alumno y marcar a todos presentes de una vez.
- Como docente, quiero que la asistencia valga un porcentaje de la calificación de la unidad (por ejemplo 10%) y que se calcule sola con las listas que tomé en esas fechas.
- Como docente, quiero justificar una falta sin que afecte el promedio del alumno.

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-100 | Una toma de asistencia pertenece a un grupo y a una fecha. Hay como máximo una toma por grupo y fecha. La fecha debe caer dentro de una unidad del ciclo (RN-023). |
| RN-101 | Cada alumno activo tiene un estado en la toma: presente, retardo, falta o justificada. Al crear la toma todos empiezan como presentes; el docente solo cambia las excepciones. |
| RN-102 | Valor de cada estado para el promedio: presente = 1, falta = 0, justificada no cuenta (ni suma ni resta tomas), retardo = el valor que configure el grupo (1, 0.5 o 0; por defecto 1). |
| RN-103 | Porcentaje de asistencia del alumno en la unidad = suma de valores / número de tomas que cuentan para ese alumno, con las tomas cuya fecha cae en la unidad. Si el alumno no tiene tomas que cuenten, el rubro no tiene datos para él (aplica RN-033). |
| RN-104 | Un rubro de tipo asistencia (RN-039) convierte el porcentaje a la escala de 0 a 10 multiplicando por 10 (95% = 9.5) y no tiene actividades. Solo puede haber un rubro de asistencia por unidad y grupo. |
| RN-105 | Un alumno dado de alta o de baja a mitad de la unidad solo cuenta las tomas en las que estaba activo. |
| RN-106 | Las tomas de una unidad `cerrada` son de solo lectura (RN-038). En una unidad `en_cierre` se pueden corregir y la observación del alumno afectado se marca "datos cambiaron, revisa" (RN-037). |

Pasar lista no exige tener un rubro de asistencia: el docente puede tomar asistencia solo como registro. La asistencia no se muestra a los tutores en el MVP.

## Captura

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md`. Pasar lista es una acción que se repite a diario: no lleva animación (solo el cambio de color de fondo de la fila en 160 ms, como la captura de calificaciones).

- **Pasar lista (celular):** fecha de hoy por defecto; lista de alumnos con su número; un toque cambia el estado en el orden presente → falta → retardo → justificada. Botón "Todos presentes" al crear la toma.
- **Historial de la unidad:** tabla de alumnos por fechas con el estado en cada toma y el porcentaje del alumno.
- Guarda al tocar, en IndexedDB primero y luego en el servidor, con la misma cola sin conexión que las calificaciones.

## API

| Método y ruta | Descripción |
| --- | --- |
| `GET /api/v1/grupos/{id}/asistencias?desde=&hasta=` | Tomas del grupo en un rango con el estado de cada alumno |
| `PUT /api/v1/grupos/{id}/asistencias/{fecha}` | Crea o reemplaza la toma de una fecha (lote de `{alumnoId, estado}` con `Idempotency-Key`) |
| `DELETE /api/v1/grupos/{id}/asistencias/{fecha}` | Borra la toma (pide confirmación en la UI) |

## Casos borde

- Toma en una fecha fuera de toda unidad del ciclo: se rechaza con `fecha_fuera_de_unidad`.
- Dos dispositivos pasan lista el mismo día sin conexión: por alumno gana la última escritura que llega al servidor, igual que una celda de calificación.
- Un día sin clases: simplemente no se toma lista; no cuenta.
- Se cambian las fechas de una unidad y una toma queda fuera: la toma se conserva y pasa a contar en la unidad que ahora contenga su fecha; si ninguna la contiene, se avisa al docente antes de guardar el cambio.

## Criterios de aceptación

- Dado un grupo con 30 alumnos, cuando el docente crea la toma de hoy y marca 2 faltas, entonces se guardan 28 presentes y 2 faltas con un solo "Todos presentes" y dos toques.
- Dadas 20 tomas en la unidad con 18 presentes, 1 falta y 1 justificada para un alumno, entonces su porcentaje es 18/19 = 94.74% y su promedio del rubro de asistencia es 9.47.
- Dado un grupo con retardo = 0.5, cuando un alumno tiene 9 presentes y 1 retardo en 10 tomas, entonces su porcentaje es 95%.
- Dado un docente sin conexión, cuando pasa lista y cierra la app, entonces al recuperar la señal la toma llega al servidor sin duplicados.
- Dados rubros Actividades 30%, Examen 60% y Asistencia 10%, cuando se calcula la unidad, entonces el resultado coincide con los vectores de prueba compartidos (RN-036), que incluyen casos con asistencia.

## Fuera de alcance

Asistencia visible para tutores, avisos automáticos por faltas, varias tomas por día o por sesión, justificantes con archivo, reportes de asistencia para la SEP.
