# Staging en Coolify (servidor de prueba de Oracle)

Pendiente de configurar con acceso al servidor. Pasos:

1. En Coolify, crear el proyecto "Aula Lista" con el entorno `staging` en un subdominio aparte.
2. Recurso PostgreSQL 18 con un volumen persistente. Ejecutar `infra/postgres/roles.sql` una vez como superusuario con `psql -v base=aula_lista -v clave_migraciones=... -v clave_runtime=... -f roles.sql`.
3. Servicio `api` desde la imagen `ghcr.io/<dueño>/<repo>-api:main` con:
   - `ConnectionStrings__AulaLista` (rol `app_runtime`), `ConnectionStrings__Migraciones` (rol `app_migraciones`), `Jwt__ClaveFirma`.
   - `Cuentas__UrlWeb` (dominio público de la web), `Correo__Proveedor=brevo`, `Correo__BrevoApiKey` (secreto), `Correo__Remitente`, `Google__ClientId` y `Google__ClientSecret` (secretos; sin ellos no hay inicio con Google).
   - Volumen persistente montado en `/var/lib/aula-lista/archivos` (`Almacen__Ruta`, ADR-0013).
   - Comando previo al despliegue: `dotnet AulaLista.Api.dll --migrar`.
   - Health check en `/salud/lista`, puerto 8080.
4. Servicio `web` desde `ghcr.io/<dueño>/<repo>-web:main`, puerto 8080, con `API_URL=http://<servicio-api>:8080`. El dominio público apunta aquí.
5. Copiar el webhook de despliegue del recurso y un token de API de Coolify a los secretos del repositorio `COOLIFY_WEBHOOK_STAGING` y `COOLIFY_TOKEN`. Cada push a `main` construye las imágenes y despliega solo.
6. Respaldos diarios cifrados a almacenamiento externo (pendiente para antes del piloto).
