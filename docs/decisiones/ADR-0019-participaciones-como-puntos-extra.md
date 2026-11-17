# ADR-0019: Participaciones diarias como puntos extra sobre la calificación del periodo

**Estado:** Aceptada · **Fecha:** 17/11/2026

## Contexto

El dueño del producto pidió (17/11/2026) que el docente pueda dar participaciones diarias en clase: busca rápido a un alumno del grupo y le suma una participación. Cada grupo configura cuánto vale una participación, de 0.01 a 1.00 punto, y eso se suma **encima** de la calificación ordinaria del periodo. Ya existe el tipo de rubro "Participaciones" (ADR-0012), pero se califica como tareas y pesa dentro de los rubros: es otra cosa.

Preguntas que había que cerrar: dónde entra el extra en el cálculo, si puede pasar de 10, qué pasa si el docente cambia el valor, cómo se guarda sin señal y cómo se muestra en boletas, Excel y el portal de tutores.

## Decisión

- **Registro, no contador.** Cada participación es un registro con id que genera el cliente, alumno, grupo, unidad (periodo) y fecha. Sumar es crear un registro y quitar es borrarlo por id. Así dos celulares sin señal nunca chocan (igual que las notas) y reenviar no duplica.
- **El valor es del grupo y no se congela.** `grupo.valor_participacion` (numeric 3,2, de 0.01 a 1.00, por defecto 0.10). Puntos extra = participaciones de la unidad × valor vigente. Cambiar el valor recalcula los promedios de todas las unidades del grupo; una boleta ya emitida no cambia (RN-051).
- **Se suma a la calificación ordinaria, con tope de 10.** Primero se calcula la ordinaria como hasta hoy (RN-033) y se le aplica la mínima reportable (RN-034). Encima se suman los puntos extra y el resultado nunca pasa de 10. Después se redondea con la configuración del grupo (RN-035). Sin puntos extra, el cálculo es idéntico al de antes.
- **Sin calificación ordinaria no hay calificación.** Si el alumno no tiene ningún rubro con datos, las participaciones se guardan pero no crean una calificación del periodo. Se suman en cuanto exista una.
- **Mismo cálculo en los dos lados (RN-036).** `Calculo.Periodo` y `calcularPeriodo` reciben el número de participaciones y el valor; los vectores compartidos tienen casos con participaciones.
- **Captura de un toque y sin señal.** Pestaña "Participaciones" en el grupo: buscador, la lista del día y un toque por alumno que suma una. Se guarda en IndexedDB y la cola la manda cuando hay red, como la asistencia. Quitar una es otro toque, también sin señal.
- **Se ve en todos lados con el mismo dato.** La matriz trae cuántas participaciones tiene cada alumno y los puntos que suman. La boleta guarda en su foto la cantidad y los puntos (renglón "Participaciones extra" bajo los rubros), el Excel agrega las columnas "Participaciones" y "Puntos extra", y el tutor ve el mismo renglón en la boleta publicada. El tutor nunca ve participaciones sueltas por fecha (RN-064).
- **Plan.** Capturar participaciones es Gratis, como toda la captura. Los puntos extra dentro del promedio se ven donde se ven los promedios (Pro, RN-097).

## Alternativas

- **Rubro con peso (el tipo "Participaciones" de hoy):** reparte el peso entre los demás rubros y no es "extra". Se queda como está para quien lo quiera así.
- **Contador por alumno y unidad:** más simple, pero dos dispositivos sin señal se pisan al sincronizar y se pierde la fecha.
- **Guardar el valor en cada participación:** respeta el valor del día en que se dio, pero el docente tendría participaciones de valores distintos y no podría corregir un valor mal elegido. Si se pide, se agrega después con la fecha de cada registro.
- **Sin tope de 10:** daría calificaciones fuera de la escala (RN-030).
- **Sumar los puntos antes de la mínima:** un alumno con 4.00, mínima 5 y 0.50 de extra quedaría en 5.00, igual que sin participar. El docente espera ver su extra encima de lo que reporta.

## Consecuencias

- Nueva tabla `participacion` con RLS por docente y una columna en `grupo`. La matriz, el cierre, las boletas y el Excel leen las participaciones de la unidad.
- `DatosBoleta` gana el campo opcional `Participaciones`; las boletas emitidas antes no lo tienen y se muestran igual que siempre.
- La IA de observaciones no recibe las participaciones en esta entrega; se puede agregar después como dato seudonimizado (RN-019).
