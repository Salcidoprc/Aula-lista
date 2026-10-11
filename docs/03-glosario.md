# Glosario

| Término | Significado | En código |
| --- | --- | --- |
| Docente | Usuario que crea grupos, captura y publica. Dueño de sus datos. | `Docente` |
| Tutor | Madre, padre o responsable de un alumno. Accede por enlace personal. | `Tutor` |
| Alumno | Estudiante de un grupo. No tiene cuenta. | `Alumno` |
| Grupo | Conjunto de hasta 50 alumnos de un docente en un ciclo (en secundaria, un grupo por materia). Unidad de cobro. | `Grupo` |
| Ciclo escolar | Año escolar del docente, por ejemplo 2026-2027, con fecha de inicio y de fin que él define. | `CicloEscolar` |
| Unidad | Tramo del ciclo con nombre y fechas (trimestre, bimestre, mes o personalizado). Se configura una vez por ciclo y la heredan todos sus grupos. Es el término que ve el docente. | `Unidad` |
| Periodo | La unidad vista desde un grupo: ahí viven los rubros, el cierre y las boletas. Estados: abierto, en cierre, cerrado. En la interfaz se dice "unidad". | `Periodo` |
| Rubro | Criterio de evaluación con peso porcentual dentro de un periodo (tareas, proyecto, examen). Tipo: actividades (promedia sus actividades) o asistencia. | `Rubro` |
| Actividad | Evaluación concreta dentro de un rubro (tarea del 12/09). | `Actividad` |
| Calificación | Valor de 0 a 10 de un alumno en una actividad, o marca "No entregó" o "Exento". | `Calificacion` |
| Calificación del periodo | Promedio ponderado de los rubros (RN-033). | `CalificacionPeriodo` |
| Toma de asistencia | La lista de un grupo en una fecha, con el estado de cada alumno (presente, retardo, falta, justificada). | `TomaAsistencia` |
| Nota | Apunte privado del docente sobre un alumno. Nunca lo ve el tutor. | `Nota` |
| Observación | Texto por alumno y periodo para la boleta; la IA propone y el docente aprueba. | `Observacion` |
| Borrador | Observación generada o editada sin aprobar. | estado `borrador` |
| Cierre | Proceso de revisar observaciones y publicar boletas de un periodo. | `Cierre` |
| Boleta | PDF con calificaciones y observación de un alumno en un periodo. Tiene versiones. | `Boleta` |
| Publicar | Hacer visible una boleta en el portal de tutores y notificarlos. | `Publicar` |
| Aviso | Mensaje del docente a tutores. Puede ser urgente. | `Aviso` |
| Enlace de tutor | URL personal con token para entrar al portal. | `EnlaceTutor` |
| Seudónimo | Identificador aleatorio del alumno usado ante la IA. | `Seudonimo` |
| Trato | Concordancia gramatical para redactar (neutro, él, ella). | `Trato` |
| Plan Gratis / Pro | Nivel de servicio por grupo. | `Plan` |
| Prueba Pro | Pro gratuito en 1 grupo, elegido por el docente, durante su primer cierre. | `PruebaPro` |
| Prepago OXXO | Pago único en OXXO que cubre 3 o 6 meses de Pro; vence en `pagado_hasta`. | `PrepagoOxxo` |
| Redondeo final | Decimales (0, 1 o 2) y sentido (arriba, abajo, cercano) con que el grupo muestra la calificación final. | `ReglaRedondeo` |
| CCT | Clave de Centro de Trabajo de la escuela (SEP). Opcional. | `EscuelaCct` |
| NEM | Nueva Escuela Mexicana, marco educativo vigente de la SEP. | — |
| LFPDPPP | Ley Federal de Protección de Datos Personales en Posesión de los Particulares. | — |
| ARCO | Derechos de acceso, rectificación, cancelación y oposición sobre datos personales. | — |
