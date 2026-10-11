# Reglas de Git, calidad y despliegue

## Ramas y commits

- Rama principal `main`, siempre desplegable. Trabajo en ramas cortas: `feat/spec-03-captura-por-actividad`, `fix/...`, `docs/...`.
- Commits en español con formato Conventional Commits: `feat(evaluacion): captura por actividad sin conexión`.
- Un PR por historia o criterio de aceptación; idealmente menos de 400 líneas cambiadas.
- Prohibido hacer push directo a `main` y reescribir la historia de `main`.

## Pull requests

La descripción incluye:

1. Qué cambia, en lenguaje simple.
2. Specs y criterios que cubre (`SPEC-03`, `RF-033`, `RN-032`).
3. Cómo se probó.
4. Cambios a documentación, migraciones o configuración.

## CI (GitHub Actions)

En cada PR:

1. Backend: `dotnet build` con advertencias como errores, `dotnet format --verify-no-changes`, pruebas unitarias e integración con Testcontainers, prueba de arquitectura, revisión de paquetes vulnerables.
2. Frontend: lint, pruebas unitarias, build de producción con presupuestos, `npm audit`.
3. End-to-end con Playwright contra la API y una base efímera, incluida la auditoría de accesibilidad con axe.
4. Verificación de que toda tabla con datos personales tiene RLS.

Un PR no se fusiona con CI en rojo.

## Despliegue

- Imágenes Docker de `web` y `api` construidas en CI y publicadas en un registro (GitHub Container Registry), etiquetadas con el SHA del commit.
- Coolify despliega automáticamente `main` a staging. Producción se despliega a mano desde Coolify eligiendo una etiqueta ya probada en staging.
- Las migraciones se aplican como paso previo al arranque de la nueva versión, con el rol `app_migraciones`.
- Toda versión en producción se puede revertir a la etiqueta anterior en menos de 15 minutos; las migraciones deben ser compatibles con la versión anterior durante un despliegue.

## Versionado

- La API se versiona en la ruta (`/api/v1`). Un cambio incompatible crea `/api/v2` y mantiene la anterior mientras haya clientes (la PWA se actualiza sola, así que el periodo de convivencia puede ser corto).
- Cambios visibles para los usuarios se anotan en `CHANGELOG.md`.
