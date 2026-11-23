# Producción (beta) en el VPS de Oracle

Producción de Aulísima en `https://aulisima.com`, en el mismo VPS de Oracle que staging (ADR-0020). Es producción real: datos reales, Stripe en modo real, IA, correo y push reales. Va en un proyecto de Docker Compose aparte (`aulisima-prod`) con su propia base, volúmenes, claves y `.env`; con staging solo comparte el Caddy del servidor.

## Qué corre

| Contenedor | Imagen | Red |
| --- | --- | --- |
| `db` | `postgres:18-alpine`, volumen `aulisima-prod_datos` | interna |
| `migraciones` | API con `--migrar`; corre y termina | interna |
| `api` | API, volúmenes `aulisima-prod_archivos` y `aulisima-prod_claves` | interna |
| `aulisima-prod-web` | web (nginx), puerto 8080 | interna y la del Caddy |

Las imágenes son las mismas de staging, pero con una etiqueta fija: `VERSION` es el SHA de un commit de `main` ya probado en staging. Nunca `main`.

## Antes de instalar

1. **Servidor ampliado** dentro de la cuota gratuita (4 OCPU, 24 GB; disco de arranque de unos 100 GB) y cuenta de Oracle en pago por uso, con alerta de presupuesto de 1 USD. Tras ampliar el disco: `sudo growpart /dev/sda 1 && sudo resize2fs /dev/sda1` (revisa el nombre con `lsblk`).
2. **Stripe en modo real:** cuenta activada, clave `sk_live_…` y un webhook a `https://aulisima.com/api/v1/webhooks/pagos` (eventos como en staging) con su `whsec_…`. Los productos y precios se crean desde `/admin` → Productos y precios, ya en modo real.
3. **Clave de la API de Claude** con límite de gasto mensual en la consola de Anthropic.
4. **Google:** en el cliente OAuth agregar el origen `https://aulisima.com` y el retorno `https://aulisima.com/api/v1/cuentas/google/callback` (el mismo de staging con otro dominio), y publicar la app (salir de modo prueba).
5. **Push:** llaves VAPID nuevas, distintas de las de staging.

## Instalación (una vez)

1. **DNS en Cloudflare:** registros `A` para `aulisima.com` y `www`, valor la IP del servidor, proxy apagado (nube gris).
2. **Código y secretos** (las claves se generan en el servidor y no salen de ahí):
   ```sh
   cd ~/aulisima && git pull
   cd infra/produccion
   cat > .env <<EOT
   VERSION=<sha probado en staging>
   URL_WEB=https://aulisima.com
   CLAVE_POSTGRES=$(openssl rand -hex 24)
   CLAVE_APP_MIGRACIONES=$(openssl rand -hex 24)
   CLAVE_APP_RUNTIME=$(openssl rand -hex 24)
   JWT_CLAVE_FIRMA=$(openssl rand -hex 32)
   IA_PROVEEDOR=claude
   PUSH_PROVEEDOR=vapid
   PAGOS_PROVEEDOR=stripe
   CORREO_PROVEEDOR=brevo
   EOT
   chmod 600 .env
   ```
   Luego se agregan a mano, con un editor en el servidor, `IA_API_KEY`, `PUSH_CLAVE_PUBLICA`, `PUSH_CLAVE_PRIVADA`, `STRIPE_CLAVE_SECRETA`, `STRIPE_SECRETO_WEBHOOK`, `BREVO_API_KEY`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` y las de Grafana Cloud (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_HEADERS`, `FARO_URL`). Los nombres están en `infra/.env.example`. Nunca se pegan en un chat ni se suben al repositorio.
3. **Arrancar:** `docker compose up -d`; revisar con `docker compose ps` que `api` y `aulisima-prod-web` estén `running` y `migraciones` haya salido con 0. Si falta una variable obligatoria, Compose lo dice y no arranca nada.
4. **Caddy:** respaldar el `Caddyfile` y agregar al final, sin `sed -i` (Caddy monta el archivo suelto):
   ```
   aulisima.com {
       reverse_proxy aulisima-prod-web:8080
   }
   www.aulisima.com {
       redir https://aulisima.com{uri} permanent
   }
   ```
   Recargar: `docker exec deploy-caddy-1 caddy reload --config /etc/caddy/Caddyfile`.
5. **Administrador:** el dueño se registra en la web y se le da el rol desde el servidor, como en staging.
6. **Probar:** registro y verificación de correo, inicio con Google, un grupo de prueba con cierre e IA, una boleta, un aviso push y un pago real de un mes que luego se reembolsa desde Stripe.

## Respaldos

Mismo esquema que staging (script con `pg_dump` cifrado con la llave pública y subida a Azure con SAS de solo crear, restringida a la IP del servidor), pero con su propio contenedor de blobs (`aulisima-prod-db-backups`), su propio archivo de variables, sus propios checks en healthchecks.io y la prueba de restauración de los domingos. El volcado se hace con `docker compose -p aulisima-prod exec -T db pg_dump …`. La llave privada para descifrar vive solo fuera del servidor.

## Actualizar

Solo con un SHA ya probado en staging:

```sh
cd ~/aulisima && git pull && cd infra/produccion
# Cambiar VERSION en .env al SHA nuevo
docker compose pull && docker compose up -d --force-recreate api aulisima-prod-web
```

Para revertir, regresar `VERSION` al SHA anterior y repetir; las migraciones deben ser compatibles con la versión anterior (`docs/reglas/06-git-calidad-despliegue.md`).

## Límites conocidos

- Un solo servidor para staging, producción y otra app (ADR-0020): si se cae, se caen todos. RPO 24 h, RTO 4 h.
- El proxy de Cloudflare debe seguir apagado (nube gris): Caddy necesita recibir la conexión para el certificado, y con el proxy encendido la API vería la IP de Cloudflare.
