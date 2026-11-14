# Staging en el VPS de Oracle

Staging de Aulísima en `https://staging.aulisima.com`, en el VPS Always Free de Oracle (Ampere A1, ARM). El servidor ya atiende otra app con Docker y un Caddy en los puertos 80 y 443; Aulísima comparte ese Caddy y nada más. Tiene su propia base de datos, sus volúmenes y su red.

Esto reemplaza por ahora el plan con Coolify de `infra/coolify.md`: el servidor es chico y ya tiene un proxy, así que basta con Docker Compose. Las imágenes son las que publica CI en GHCR para `linux/arm64` (`ghcr.io/salcidoprc/aula-lista-api` y `-web`), públicas, sin iniciar sesión.

## Qué corre

| Contenedor | Imagen | Red |
| --- | --- | --- |
| `db` | `postgres:18-alpine`, volumen `aulisima_datos` | interna |
| `migraciones` | API con `--migrar`; corre y termina | interna |
| `api` | API, volumen `aulisima_archivos` | interna |
| `aulisima-web` | web (nginx), puerto 8080 | interna y la del Caddy |

Ningún contenedor publica puertos en el servidor. Caddy entra a `aulisima-web:8080` por su red (`deploy_default`; cámbiala con `RED_CADDY` si se llama distinto).

## Instalación (una vez)

1. **DNS en Cloudflare:** registro `A`, nombre `staging`, valor la IP pública del servidor, proxy apagado (nube gris, "solo DNS") para que Caddy pueda sacar el certificado.
2. **Espacio:** `docker builder prune -af` borra solo el caché de compilación. No borres volúmenes: pueden ser datos de la otra app.
3. **Código y secretos:**
   ```sh
   cd ~ && git clone https://github.com/Salcidoprc/Aula-lista.git aulisima
   cd ~/aulisima/infra/staging
   cat > .env <<EOF
   URL_WEB=https://staging.aulisima.com
   CLAVE_POSTGRES=$(openssl rand -hex 24)
   CLAVE_APP_MIGRACIONES=$(openssl rand -hex 24)
   CLAVE_APP_RUNTIME=$(openssl rand -hex 24)
   JWT_CLAVE_FIRMA=$(openssl rand -hex 32)
   EOF
   chmod 600 .env
   ```
   Las claves se generan en el servidor y no salen de ahí. `.env` está en `.gitignore`.
4. **Arrancar:** `docker compose up -d` y revisar con `docker compose ps` que `api` y `aulisima-web` estén `running` y `migraciones` haya salido con 0.
5. **Caddy:** respaldar y agregar al final del `Caddyfile` de la otra app (en este servidor, `~/tombola/deploy/Caddyfile`):
   ```
   staging.aulisima.com {
       reverse_proxy aulisima-web:8080
   }
   ```
   Recargar sin tumbar la otra app: `docker exec deploy-caddy-1 caddy reload --config /etc/caddy/Caddyfile`.
6. **Probar:** `curl -I https://staging.aulisima.com` responde 200 y la web abre con candado.

Los puertos 80 y 443 ya están abiertos en la Security List de Oracle y en iptables porque la otra app los usa; no hay que abrir nada.

## Servicios externos

Todo arranca en modo simulado (correo en buzón interno, IA, WhatsApp, push y pagos). Para encender uno, agregar sus variables a `.env` directamente en el servidor (nombres en `infra/.env.example`) y correr `docker compose up -d`. Los secretos nunca van al repositorio ni a un chat.

## Actualizar

Cada push a `main` publica imágenes nuevas con la etiqueta `main`:

```sh
cd ~/aulisima && git pull && cd infra/staging && docker compose pull && docker compose up -d && docker image prune -f
```

Para fijar una versión, `VERSION=<sha>` en `.env`.

## Respaldos

Mientras sea staging basta un volcado diario en el mismo servidor, con 7 días de retención (`crontab -e`):

```
0 8 * * * cd ~/aulisima/infra/staging && mkdir -p ~/respaldos-aulisima && docker compose exec -T db pg_dump -U postgres -Fc aulisima > ~/respaldos-aulisima/aulisima-$(date +\%F).dump && find ~/respaldos-aulisima -name '*.dump' -mtime +7 -delete
```

Las 08:00 UTC son las 02:00 del centro de México. Antes del piloto se cambia por el respaldo cifrado fuera del servidor de SPEC-09 (`infra/respaldos/`).

## Límites conocidos

- La API ve la IP de Caddy y no la del usuario, porque hay dos proxies (Caddy y nginx) y solo confía en uno. El límite de 5 inicios de sesión por minuto (RNF-004) se comparte entre todos. En staging no importa; se arregla antes del piloto.
- Con 1 OCPU y 6 GB alcanza para pruebas. La cuota gratuita permite subir la instancia hasta 4 OCPU y 24 GB, y el disco de arranque hasta 200 GB; requiere reiniciarla.
