# Staging en Coolify (servidor de prueba de Oracle)

Pendiente de configurar con acceso al servidor. Pasos:

1. En Coolify, crear el proyecto "Aula Lista" con el entorno `staging` en un subdominio aparte.
2. Recurso PostgreSQL 18 con un volumen persistente. Ejecutar `infra/postgres/roles.sql` una vez como superusuario con `psql -v base=aula_lista -v clave_migraciones=... -v clave_runtime=... -f roles.sql`.
3. Servicio `api` desde la imagen `ghcr.io/<dueño>/<repo>-api:main` con:
   - `ConnectionStrings__AulaLista` (rol `app_runtime`), `ConnectionStrings__Migraciones` (rol `app_migraciones`), `Jwt__ClaveFirma`.
   - `Cuentas__UrlWeb` (dominio público de la web), `Correo__Proveedor=brevo`, `Correo__BrevoApiKey` (secreto), `Correo__Remitente`, `Google__ClientId` y `Google__ClientSecret` (secretos; sin ellos no hay inicio con Google), `Ia__Proveedor=claude` e `Ia__ApiKey` (secreto de la API de Claude; sin él la API no arranca con ese proveedor).
   - Volumen persistente montado en `/var/lib/aula-lista/archivos` (`Almacen__Ruta`, ADR-0013).
   - Comando previo al despliegue: `dotnet AulaLista.Api.dll --migrar`.
   - Health check en `/salud/lista`, puerto 8080.
4. Servicio `web` desde `ghcr.io/<dueño>/<repo>-web:main`, puerto 8080, con `API_URL=http://<servicio-api>:8080`. El dominio público apunta aquí.
5. Copiar el webhook de despliegue del recurso y un token de API de Coolify a los secretos del repositorio `COOLIFY_WEBHOOK_STAGING` y `COOLIFY_TOKEN`. Cada push a `main` construye las imágenes y despliega solo.
6. Respaldos diarios (SPEC-09), pendiente para antes del piloto:
   - Correr `infra/postgres/respaldos.sql` una vez como superusuario (`psql -v clave_respaldos=... -f respaldos.sql`): crea `app_respaldos`, de solo lectura.
   - Crear un bucket en almacenamiento de objetos fuera del servidor (Oracle Object Storage u otro) y su remoto de rclone solo con variables `RCLONE_CONFIG_<REMOTO>_*`.
   - Generar la llave con `age-keygen` fuera del servidor; en el servidor solo va la pública (`AGE_RECIPIENT`). La privada se guarda aparte (gestor de contraseñas del dueño).
   - Tarea programada de Coolify, diaria a las 02:00 hora del centro de México, en un contenedor con `postgresql-client-18`, `age` y `rclone` que monte `infra/respaldos/`: `respaldar.sh` con `PGHOST`, `PGDATABASE`, `PGUSER=app_respaldos`, `PGPASSWORD`, `AGE_RECIPIENT` y `RESPALDO_DESTINO`. Retiene 30 días (`RESPALDO_DIAS`).
   - Alerta si la tarea falla (notificaciones de Coolify por correo).
   - Probar la restauración el primer lunes de cada mes y anotarla en `docs/operacion/restauraciones.md`.
7. Trazas (opcional): con `OTEL_EXPORTER_OTLP_ENDPOINT` (y `OTEL_EXPORTER_OTLP_HEADERS` si el servicio pide llave) la API manda trazas y métricas por OTLP; sin esa variable no se instrumenta.
