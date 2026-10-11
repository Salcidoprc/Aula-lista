# SPEC-02: Grupos, alumnos y tutores

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-010 a RF-020, RNF-007, RNF-008

## Objetivo

Que el docente tenga su grupo completo, con tutores y sus WhatsApp, en menos de 10 minutos, sin capturar nombre por nombre.

## Historias de usuario

- Como docente de secundaria, quiero crear mis 6 grupos de una vez en lugar de uno por uno.
- Como docente, quiero importar mi lista desde el Excel que ya tengo.
- Como docente, quiero tomar foto de mi lista impresa y que la app saque los nombres.
- Como docente, quiero registrar el WhatsApp de los tutores para que reciban las boletas.

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-010 | Un grupo pertenece a un solo docente en el MVP. |
| RN-011 | Un grupo tiene máximo 50 alumnos activos. Un alumno dado de baja no cuenta para el límite. |
| RN-012 | Al crear un grupo se proponen 3 periodos (Trimestre 1, 2 y 3) con fechas del ciclo; el docente puede editar nombres, fechas y cantidad (de 1 a 6). |
| RN-013 | El número de lista se asigna por orden alfabético de apellidos y el docente puede cambiarlo. |
| RN-014 | Los teléfonos se guardan en formato E.164. Si el docente escribe 10 dígitos, se asume +52 (México). |
| RN-015 | Un tutor se identifica por su teléfono. Si el teléfono ya existe, el sistema vincula al tutor existente y conserva su nombre registrado; no muestra al docente datos de otros grupos de ese tutor. |
| RN-016 | Un alumno tiene de 0 a 3 tutores. Un alumno sin tutor puede evaluarse, pero su boleta no se notifica. |
| RN-017 | Dar de baja a un alumno conserva sus calificaciones y boletas; deja de aparecer en captura y en el portal de tutores para periodos posteriores. |
| RN-018 | Un grupo archivado es de solo lectura: se puede consultar y exportar, no editar. |
| RN-019 | Cada alumno recibe un seudónimo aleatorio (por ejemplo "Alumno 7F3K") que se usa en todo lo que se envía a la IA. |
| RN-020 | El campo `trato` (neutro, él, ella) solo sirve para la concordancia gramatical de las observaciones. Por defecto es neutro. |
| RN-021 | La creación de varios grupos a la vez (RF-020) recibe datos comunes (nivel, grado, materia, ciclo, periodos) y una lista de nombres de grupo (hasta 15 por operación). Se crean todos o ninguno. Cada grupo queda en Gratis; el docente elige después cuáles pasan a Pro. |

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

## API

| Método y ruta | Descripción |
| --- | --- |
| `GET` / `POST /api/v1/grupos` | Lista y crea grupos |
| `POST /api/v1/grupos/lote` | Crea varios grupos con datos comunes (RN-021) |
| `GET` / `PUT` / `DELETE /api/v1/grupos/{id}` | Detalle, edición y borrado (solo si no tiene calificaciones) |
| `POST /api/v1/grupos/{id}/archivar` | Archiva el grupo |
| `GET` / `PUT /api/v1/grupos/{id}/periodos` | Lee y reemplaza los periodos |
| `GET` / `POST /api/v1/grupos/{id}/alumnos` | Lista y agrega alumnos |
| `PUT` / `DELETE /api/v1/alumnos/{id}` | Edita o da de baja |
| `POST /api/v1/grupos/{id}/importaciones` | Sube archivo, texto o foto; devuelve vista previa |
| `POST /api/v1/importaciones/{id}/confirmar` | Guarda las filas confirmadas |
| `GET` / `POST /api/v1/alumnos/{id}/tutores` | Lista y vincula tutores |
| `PUT` / `DELETE /api/v1/alumnos/{id}/tutores/{tutorId}` | Edita parentesco o desvincula |

## Pantallas

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

- **Crear grupo:** nombre, nivel, grado, materia (solo secundaria), ciclo; periodos propuestos editables. Opción "Crear varios": el docente escribe los nombres (por ejemplo A, B, C) y se crean con los mismos datos.
- **Configuración de calificación del grupo:** mínima, aprobatoria, decimales de la calificación final y sentido del redondeo (RN-035).
- **Alumnos del grupo:** lista con número, nombre, tutores y estado; botones de agregar e importar.
- **Importar:** pestañas Excel, Pegar lista, Foto; vista previa editable común.
- **Ficha del alumno (sección datos):** nombre, trato, tutores con WhatsApp.

## Casos borde

- Dos alumnos con el mismo nombre: se permite; se distinguen por número de lista.
- El mismo teléfono aparece como tutor de dos alumnos del grupo (hermanos): un solo tutor vinculado a ambos.
- El docente escribe un teléfono que ya pertenece a un tutor con otro nombre: se vincula y se avisa "Este número ya está registrado como tutor"; no se muestra el otro nombre.
- Importación que excede 50 alumnos: se importan los primeros hasta el límite y se informa el resto.

## Criterios de aceptación

- Dado un Excel con 35 alumnos y sus tutores, cuando el docente lo importa y confirma, entonces el grupo tiene 35 alumnos con sus tutores y teléfonos en E.164.
- Dado un grupo con 50 alumnos activos, cuando el docente intenta agregar otro, entonces se rechaza con `limite_alumnos_grupo`.
- Dado un teléfono ya registrado por otro docente, cuando se captura como tutor, entonces se vincula al tutor existente y el docente no ve ningún dato del otro grupo.
- Dado un alumno dado de baja, cuando el tutor abre el portal, entonces ve las boletas ya publicadas de ese alumno pero no periodos nuevos.
- Dada una foto de lista legible con 30 nombres, cuando se procesa, entonces la vista previa muestra al menos 28 nombres correctos (meta de calidad, no bloqueante).

## Fuera de alcance

Grupos compartidos entre docentes, transferencia de alumnos entre grupos, fotos de alumnos, CURP.
