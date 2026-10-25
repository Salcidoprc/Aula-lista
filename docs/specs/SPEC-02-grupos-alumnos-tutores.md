# SPEC-02: Grupos, alumnos y tutores

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-010 a RF-022, RNF-007, RNF-008

## Objetivo

Que el docente tenga su grupo completo, con tutores y sus WhatsApp, en menos de 10 minutos, sin capturar nombre por nombre.

## Historias de usuario

- Como docente de secundaria, quiero crear mis 6 grupos de una vez en lugar de uno por uno.
- Como docente, quiero importar mi lista desde el Excel que ya tengo.
- Como docente, quiero tomar foto de mi lista impresa y que la app saque los nombres.
- Como docente, quiero registrar el WhatsApp de los tutores para que reciban las boletas.
- Como docente, quiero definir de qué fecha a qué fecha va mi ciclo y en cuántas unidades lo divido (trimestres, bimestres, meses), una sola vez para todos mis grupos.

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-010 | Un grupo pertenece a un solo docente en el MVP. |
| RN-011 | Un grupo tiene máximo 50 alumnos activos. Un alumno dado de baja no cuenta para el límite. |
| RN-012 | Reemplazada por RN-022 a RN-027: las unidades se configuran en el ciclo escolar y los grupos las heredan. |
| RN-013 | El número de lista se asigna por orden alfabético de apellidos y el docente puede cambiarlo. |
| RN-014 | Los teléfonos se guardan en formato E.164. Si el docente escribe 10 dígitos, se asume +52 (México). |
| RN-015 | Un tutor se identifica por su teléfono. Si el teléfono ya existe, el sistema vincula al tutor existente y conserva su nombre registrado; no muestra al docente datos de otros grupos de ese tutor. |
| RN-016 | Un alumno tiene de 0 a 3 tutores. Un alumno sin tutor puede evaluarse, pero su boleta no se notifica. |
| RN-017 | Dar de baja a un alumno conserva sus calificaciones y boletas; deja de aparecer en captura y en el portal de tutores para periodos posteriores. |
| RN-018 | Un grupo archivado es de solo lectura: se puede consultar y exportar, no editar. |
| RN-019 | Cada alumno recibe un seudónimo aleatorio (por ejemplo "Alumno 7F3K") que se usa en todo lo que se envía a la IA. |
| RN-020 | El campo `trato` (neutro, él, ella) solo sirve para la concordancia gramatical de las observaciones. Por defecto es neutro. |
| RN-021 | La creación de varios grupos a la vez (RF-020) recibe datos comunes (nivel, grado, materia, ciclo) y una lista de nombres de grupo (hasta 15 por operación); las unidades vienen del ciclo (RN-025). Se crean todos o ninguno. Cada grupo queda en Gratis; el docente elige después cuáles pasan a Pro. |

## Ciclo escolar y unidades

El docente trabaja por unidades: el ciclo dura de una fecha a otra y se divide en unidades (trimestres, bimestres, meses o las que él defina). En la interfaz se llaman "unidades"; en el modelo, cada grupo tiene un periodo por cada unidad del ciclo (ver glosario).

| ID | Regla |
| --- | --- |
| RN-022 | El ciclo escolar pertenece al docente. Tiene nombre (por ejemplo "2026-2027"), fecha de inicio y fecha de fin, que el docente define. La fecha de fin es posterior a la de inicio y el ciclo dura como máximo 13 meses. Un docente puede tener varios ciclos (el actual y los anteriores). |
| RN-023 | El ciclo se divide en unidades consecutivas, cada una con nombre, fecha de inicio y fecha de fin. Las unidades no se traslapan, quedan dentro del ciclo y van en orden. Puede haber días entre unidades (vacaciones) que no pertenecen a ninguna. Una fecha pertenece como máximo a una unidad. |
| RN-024 | Al crear el ciclo el docente elige el tipo de unidad: bimestre, trimestre, cuatrimestre, semestre, mensual o personalizada. El sistema propone la cantidad que corresponde a la duración del ciclo (por ejemplo 3 trimestres en un ciclo de agosto a julio) y reparte las fechas en partes iguales por días; el docente edita nombres, fechas y cantidad (de 1 a 12). Por defecto: trimestre. |
| RN-025 | Las unidades se configuran una vez para todo el ciclo: todos los grupos del ciclo las heredan. Al crear un grupo en un ciclo se crea un periodo por cada unidad, con estado propio por grupo (abierto, en cierre, cerrado). |
| RN-026 | Cambiar las fechas de una unidad afecta a todos los grupos del ciclo. No se puede cambiar la fecha de una unidad que ya está cerrada en algún grupo. Si al mover fechas una actividad o toma de asistencia queda fuera de toda unidad, se avisa al docente antes de guardar. |
| RN-027 | Se puede agregar una unidad al final del ciclo en cualquier momento. Una unidad solo se puede borrar si ningún grupo tiene rubros, calificaciones o asistencia en ella. |

## Importación

### Excel o CSV (RF-013)

1. El docente sube un archivo .xlsx o .csv de hasta 2 MB.
2. El sistema detecta columnas por encabezado (nombre, apellidos, tutor, teléfono, correo, parentesco) y propone el mapeo; el docente lo corrige.
3. Vista previa con validaciones por fila: nombre vacío, teléfono inválido, duplicado dentro del archivo, exceso de 50 alumnos.
4. El docente corrige en la vista previa y confirma; se guardan solo las filas válidas y se informa cuántas se omitieron.

Plantilla descargable con las columnas esperadas.

### Texto pegado (RF-014)

Un alumno por línea. Se aceptan formatos "Apellidos Nombres", "Nombres Apellidos" o separados por coma; el docente elige el orden en la vista previa.

### Foto de lista (RF-015)

1. El docente toma o sube una foto (JPG o PNG, hasta 5 MB).
2. La imagen se envía a la API de Claude solo para extraer nombres; no se guarda en el servidor después de procesarla.
3. El resultado llega a la misma vista previa de la importación por texto para corregir y confirmar.
4. Requiere conexión; si la lectura falla, se ofrece pegar texto.

Decisiones de H6.3:
- La web reduce la foto a 1568 px por el lado largo y la vuelve a codificar como JPEG antes de subirla: menos datos, menos tokens y sin metadatos de la cámara (ubicación). La API acepta JPG o PNG por sus primeros bytes y rechaza lo que pase de 5 MB con `foto_invalida`.
- La lectura es la única llamada a la IA que lleva nombres reales, porque la foto los trae (`reglas/04-seguridad-privacidad.md`). Solo se piden los nombres de los alumnos; la imagen vive en memoria durante la petición y no se guarda ni se registra.
- Cada lectura se registra en `llamada_ia` (versión de prompt `lista-v1`) y cuenta para el límite por hora (RNF-004, `limite_lecturas`, 429) y para el costo del grupo en el panel (RNF-022). Costo estimado: unos 0.015 MXN por lectura con Haiku 5.5.
- Si la IA no lee ningún nombre, la vista previa llega sin filas (200) para no revertir el registro de la llamada; la web lo muestra como "No pudimos leer nombres" y ofrece pegar la lista.
- Disponible en todos los planes, como el resto de la importación.

## API

| Método y ruta | Descripción |
| --- | --- |
| `GET` / `POST /api/v1/grupos` | Lista y crea grupos |
| `POST /api/v1/grupos/lote` | Crea varios grupos con datos comunes (RN-021) |
| `GET` / `PUT` / `DELETE /api/v1/grupos/{id}` | Detalle, edición y borrado (solo si no tiene calificaciones) |
| `POST /api/v1/grupos/{id}/archivar` | Archiva el grupo |
| `GET` / `POST /api/v1/ciclos` | Lista y crea ciclos del docente con sus unidades (RN-022 a RN-024) |
| `GET` / `PUT /api/v1/ciclos/{id}` | Detalle y edición del ciclo (nombre, fechas) |
| `PUT /api/v1/ciclos/{id}/unidades` | Reemplaza las unidades del ciclo (valida RN-023, RN-026 y RN-027) |
| `GET /api/v1/grupos/{id}/periodos` | Periodos del grupo: una entrada por unidad con su estado |
| `GET` / `POST /api/v1/grupos/{id}/alumnos` | Lista y agrega alumnos |
| `GET` / `PUT` / `DELETE /api/v1/alumnos/{id}` | Ficha, edición o baja |
| `POST /api/v1/grupos/{id}/importaciones` | Sube archivo, texto o foto; devuelve vista previa |
| `POST /api/v1/importaciones/{id}/confirmar` | Guarda las filas confirmadas |
| `GET /api/v1/importaciones/plantilla` | Descarga la plantilla de Excel con las columnas que se detectan |
| `GET` / `POST /api/v1/alumnos/{id}/tutores` | Lista y vincula tutores |
| `PUT` / `DELETE /api/v1/alumnos/{id}/tutores/{tutorId}` | Edita parentesco o desvincula |

## Pantallas

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

- **Ciclo escolar:** nombre, fecha de inicio y de fin, tipo de unidad y cantidad; vista previa de las unidades con fechas editables en una línea de tiempo. Se pide la primera vez que el docente crea un grupo.
- **Crear grupo:** nombre, nivel, grado, materia (solo secundaria) y ciclo; las unidades vienen del ciclo. Opción "Crear varios": el docente escribe los nombres (por ejemplo A, B, C) y se crean con los mismos datos.
- **Configuración de calificación del grupo:** mínima, aprobatoria, decimales de la calificación final y sentido del redondeo (RN-035).
- **Alumnos del grupo:** lista con número, nombre, tutores y estado; botones de agregar e importar.
- **Importar:** pestañas Excel, Pegar lista, Foto; vista previa editable común.
- **Ficha del alumno (sección datos):** nombre, trato, tutores con WhatsApp.

## Casos borde

- Dos alumnos con el mismo nombre: se permite; se distinguen por número de lista.
- El mismo teléfono aparece como tutor de dos alumnos del grupo (hermanos): un solo tutor vinculado a ambos.
- El docente escribe un teléfono que ya pertenece a un tutor con otro nombre: se vincula y se avisa "Este número ya está registrado como tutor"; no se muestra el otro nombre.
- Importación que excede 50 alumnos: se importan los primeros hasta el límite y se informa el resto.
- Dos grupos con el mismo nombre en el ciclo: se permiten si difieren en grado o materia ("1° A" y "4° A"); con el mismo grado, nombre y materia (sin distinguir mayúsculas) se rechaza con `grupo_duplicado`.

## Criterios de aceptación

- Dado un ciclo del 25/08/2026 al 16/07/2027 con tipo trimestre, cuando el docente lo crea, entonces se proponen 3 unidades consecutivas sin traslape que cubren el ciclo y puede editarlas.
- Dado un ciclo con 3 unidades y 4 grupos, cuando el docente cambia la fecha de fin de la unidad 1, entonces el cambio se refleja en los 4 grupos.
- Dadas dos unidades que se traslapan, cuando el docente guarda, entonces se rechaza con `unidades_traslapadas`.
- Dada una unidad cerrada en un grupo, cuando el docente intenta cambiar sus fechas, entonces se rechaza con `unidad_cerrada`.

- Dado un Excel con 35 alumnos y sus tutores, cuando el docente lo importa y confirma, entonces el grupo tiene 35 alumnos con sus tutores y teléfonos en E.164.
- Dado un grupo con 50 alumnos activos, cuando el docente intenta agregar otro, entonces se rechaza con `limite_alumnos_grupo`.
- Dado un teléfono ya registrado por otro docente, cuando se captura como tutor, entonces se vincula al tutor existente y el docente no ve ningún dato del otro grupo.
- Dado un alumno dado de baja, cuando el tutor abre el portal, entonces ve las boletas ya publicadas de ese alumno pero no periodos nuevos.
- Dada una foto de lista legible con 30 nombres, cuando se procesa, entonces la vista previa muestra al menos 28 nombres correctos (meta de calidad, no bloqueante).
- Dada una foto JPG o PNG de una lista, cuando el docente la sube, entonces los nombres llegan a la misma vista previa de "Pegar lista", con un aviso de revisarlos, y la foto no queda guardada.
- Dado un archivo que no es JPG ni PNG o pesa más de 5 MB, cuando se sube como foto, entonces se rechaza con `foto_invalida` sin llamar a la IA.
- Dada una foto en la que la IA no lee nombres, o sin conexión, entonces se ofrece pegar la lista y la lectura fallida cuenta para el límite y el costo.

## Fuera de alcance

Grupos compartidos entre docentes, transferencia de alumnos entre grupos, fotos de alumnos, CURP.
