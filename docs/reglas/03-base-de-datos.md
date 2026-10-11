# Reglas de base de datos (PostgreSQL)

- PostgreSQL 18. Tablas y columnas en español y `snake_case`; tablas en singular (`alumno`, `calificacion`).
- Llave primaria `id uuid` generada por la aplicación (UUID v7). Llaves foráneas con índice.
- Toda tabla tiene `created_at` y `updated_at` (`timestamptz`, UTC).
- Calificaciones `numeric(4,2)`, pesos `numeric(5,2)`, dinero `numeric(10,2)` en MXN.
- Estados como `text` con `CHECK` de valores permitidos, no como enteros mágicos.
- Restricciones en la base para las reglas críticas: unicidad de `calificacion (actividad_id, alumno_id)`, de `observacion (alumno_id, periodo_id)`, de `tutor (telefono_e164)`; `CHECK (valor BETWEEN 0 AND 10)`.

## Row Level Security

- Activada en todas las tablas con datos de alumnos, tutores o docentes.
- Políticas basadas en `current_setting('app.docente_id', true)` y `current_setting('app.tutor_id', true)`, fijadas con `SET LOCAL` en cada transacción.
- La aplicación usa el rol `app_runtime` sin `BYPASSRLS` y sin permisos de DDL. Las migraciones usan `app_migraciones`. Nadie usa el superusuario desde la aplicación.
- Toda tabla nueva con datos personales llega con su política RLS en la misma migración; una prueba de CI falla si encuentra una tabla con `docente_id` o `alumno_id` sin RLS.

## Migraciones

- Solo con EF Core Migrations, revisadas en el PR (incluido el SQL generado).
- Cambios que borran o renombran columnas se hacen en dos despliegues (agregar, migrar datos, después quitar).
- Nunca se modifica una migración ya aplicada fuera de local.

## Datos

- Prohibido copiar datos reales a local o staging. Los datos de prueba se generan con un seed de nombres ficticios.
- Borrado lógico (`baja_en`) para alumnos; borrado físico solo por los jobs de retención (RNF-009) o por solicitud ARCO.
- Las tablas `auditoria` y `aceptacion_legal` solo admiten `INSERT` para `app_runtime`.
