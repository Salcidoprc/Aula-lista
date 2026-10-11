# ADR-0011: Herramientas y decisiones de los cimientos (H0)

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

H0 deja listo el repositorio para construir los módulos. Varias piezas no estaban fijadas en los ADR anteriores: versiones, generación del cliente de la API, textos, tipografía y cómo se aplican las migraciones.

## Decisión

- **Versiones:** .NET SDK 10.0 (fijado en `apps/api/global.json`), Angular 22, Node 24 LTS (Angular 22 exige Node 22.22.3 o superior), PostgreSQL 18.
- **Contrato de la API:** el documento OpenAPI se genera al compilar la API (`Microsoft.Extensions.ApiDescription.Server`) en `apps/api/openapi/v1.json` y se versiona. El cliente Angular se genera de ese archivo con `ng-openapi-gen` en `src/app/api`. CI falla si alguno de los dos no está al día.
- **Textos:** Transloco con `public/i18n/es-MX.json`.
- **Tipografía:** Overpass (variable, autoalojada con Fontsource), con cifras tabulares y español completo, en línea con la regla 07.
- **Migraciones:** la imagen de la API acepta `--migrar`, que aplica las migraciones con el rol `app_migraciones` y termina. Coolify lo ejecuta antes de arrancar la nueva versión; docker-compose lo hace con el servicio `migraciones`.
- **RLS por solicitud:** cada solicitud autenticada a `/api` abre una transacción y un interceptor de EF Core fija `app.docente_id` y `app.tutor_id` con alcance local (equivalente a `SET LOCAL`).
- **Tokens de acceso:** JWT firmado con clave simétrica de configuración (`Jwt__ClaveFirma`). La emisión y la renovación llegan en H1 (SPEC-01).
- **Pruebas:** xUnit con Testcontainers (PostgreSQL real con los mismos roles), NetArchTest para capas, Vitest para unitarias de Angular y Playwright con axe para end-to-end, también con movimiento reducido.
- **Presupuesto del bundle:** Angular mide sin comprimir; `npm run bundle:verificar` suma con gzip lo que carga el inicio y falla sobre 250 KB (RNF-011).

## Alternativas

- NSwag para generar el cliente: más pesado y con plantillas propias; `ng-openapi-gen` produce funciones pequeñas que se cargan por partes.
- Migraciones al arrancar la API: mezcla roles y complica revertir; se prefiere un paso previo explícito.

## Consecuencias

- Al cambiar un endpoint hay que compilar la API y correr `npm run api:generar`, o CI falla.
- El desarrollo local necesita Node 24, .NET 10 y Docker.
