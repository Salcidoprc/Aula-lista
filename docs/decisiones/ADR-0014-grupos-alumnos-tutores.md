# ADR-0014: Decisiones de grupos, alumnos y tutores (H2)

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

H2 implementa SPEC-02: ciclo escolar del docente con sus unidades, grupos con un periodo por unidad, alumnos, tutores e importación. Varias piezas no estaban fijadas: cómo un docente vincula un tutor que ya registró otro docente sin ver sus datos, cómo se proponen las unidades, cómo se numeran los alumnos que llegan después, y dónde vive la vista previa de una importación.

## Decisión

- **Ciclo por docente:** `ciclo_escolar` deja de ser un catálogo global (H0) y pasa a tener `docente_id`, `tipo_unidad` y RLS. El nombre es único por docente. La tabla no tenía datos fuera de local.
- **Unidades propuestas:** al crear el ciclo, la API propone las unidades si el docente no manda la cantidad. La cantidad es `piso(meses / meses_por_unidad + 0.25)`, entre 1 y 12, con meses = días del ciclo / 30.44 (bimestre 2, trimestre 3, cuatrimestre 4, semestre 6, mensual 1; personalizada propone 1). Así un ciclo de agosto a julio da 5 bimestres, 3 trimestres y 2 semestres. Las fechas se reparten en partes iguales por días, sin huecos, y los nombres son "Trimestre 1", "Bimestre 2", "Mes 3" o "Unidad 1". El docente las edita después con `PUT /ciclos/{id}/unidades`.
- **Traslape en la base:** `unidad` tiene una restricción de exclusión sobre `daterange(inicio, fin, '[]')` por ciclo (extensión `btree_gist`), además de la validación en el dominio que responde `unidades_traslapadas`.
- **Periodos:** se crean al crear el grupo (uno por unidad) y al agregar una unidad al ciclo (uno por grupo). Borrar una unidad borra sus periodos. Llevan `docente_id` para RLS.
- **Tutor compartido:** `tutor` guarda solo lo que identifica al tutor ante el sistema: teléfono E.164 (único), el nombre y el correo con que se registró la primera vez. Un docente nunca lee ni escribe `tutor` directamente: la función `vincular_tutor(telefono, nombre, correo)` (`SECURITY DEFINER`, solo `EXECUTE` para `app_runtime`) devuelve el id del tutor existente o lo crea, y dice si ya existía. Lo que cada docente captura (nombre, correo, parentesco y una copia del teléfono E.164, para mostrarlo y exportarlo sin leer `tutor`) vive en `vinculo_tutor`, que tiene `docente_id` y RLS. Así el docente ve su propio dato aunque el tutor exista con otro nombre (RN-015) y nunca ve lo que capturó otro docente. `tutor` tiene RLS que solo deja ver la fila al propio tutor (`app.tutor_id`) o al docente que tiene un vínculo con él.
- **Aviso de número registrado:** al vincular, la API responde `yaRegistrado = true` si el tutor existía y el docente no tenía ningún vínculo con él. Con hermanos del mismo grupo no se avisa.
- **Número de lista (RN-013):** cada alta o importación numera su lote por orden alfabético de apellidos y nombres a partir del último número del grupo. Los alumnos que ya estaban no se renumeran, como en una lista impresa donde los que llegan tarde van al final. El docente cambia un número con `PUT /alumnos/{id}`; si el número ya lo tiene otro alumno activo, se intercambian. Un alumno dado de baja conserva su número y no lo ocupa.
- **Límite de 50 (RN-011):** el alta bloquea la fila del grupo (`SELECT ... FOR UPDATE`) antes de contar, para que dos altas simultáneas no pasen el límite.
- **Seudónimo (RN-019):** "Alumno " más 4 caracteres de un alfabeto sin caracteres ambiguos (sin 0, O, 1, I, L), único por grupo.
- **Importación:** `POST /grupos/{id}/importaciones` lee el archivo (.xlsx o .csv de hasta 2 MB) o el texto pegado y devuelve la tabla cruda (encabezados y celdas) con el mapeo de columnas propuesto. El archivo no se guarda. La tabla `importacion` solo guarda metadatos sin datos personales (grupo, origen, estado) para que una importación no se confirme dos veces. La web aplica el mapeo y la orden de nombres y valida en la vista previa; `POST /importaciones/{id}/confirmar` recibe las filas corregidas, las vuelve a validar, guarda las válidas hasta llenar el grupo y responde cuántas guardó y cuántas omitió por error o por límite. Los CSV se leen como UTF-8 y, si no lo son, como Windows-1252 (lo que exporta Excel en México); el separador puede ser coma o punto y coma.
- **Plantilla:** `GET /api/v1/importaciones/plantilla` genera el .xlsx con las columnas esperadas.
- **Foto de lista (RF-015):** queda para H6, como dice el plan de desarrollo.

## Alternativas

- Que el docente edite el nombre del tutor compartido: el otro docente vería un nombre que no capturó y se filtraría información entre grupos.
- Renumerar a todo el grupo en cada alta: cambiaría los números que el docente ya usa en sus listas de papel.
- Guardar la vista previa de la importación en la base: obliga a guardar nombres de menores fuera de su tabla, con RLS y retención propias, sin ganar nada.
- Proponer unidades con redondeo normal: un ciclo de agosto a julio daría 4 trimestres en lugar de los 3 que usa la SEP.

## Consecuencias

- Cualquier consulta de un docente sobre tutores pasa por `vinculo_tutor`; la columna `tutor.nombre` solo la usa el portal de tutores (SPEC-06).
- `eliminar_cuentas_vencidas` borra también ciclos, grupos y alumnos del docente (en cascada) y los tutores que se quedan sin ningún vínculo.
- RN-026 pide avisar si una actividad o toma de asistencia queda fuera de toda unidad al mover fechas; H3 y SPEC-10 lo agregan cuando existan esas tablas. Lo mismo para RN-027 (no borrar una unidad con rubros, calificaciones o asistencia).
