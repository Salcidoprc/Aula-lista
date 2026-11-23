# ADR-0020: Beta de producción en el VPS de Oracle, junto a staging

**Estado:** Aceptada, complementa [ADR-0007](ADR-0007-migracion-hosting.md) · **Fecha:** 23/11/2026

## Contexto

Aulísima está lista para un grupo controlado de docentes. Staging ya corre en el VPS Always Free de Oracle (`docs/operacion/staging-oracle.md`) con Docker Compose detrás del Caddy de otra app. ADR-0007 deja la producción en Azure o similar, con un costo estimado de 650 a 900 MXN al mes, y no hace falta pagarlo para unos cuantos docentes.

## Decisión

- La beta es **producción real** desde el primer día: datos reales de docentes y alumnos, Stripe en modo real, IA con la API de Claude, correo por Brevo y push con VAPID. WhatsApp sigue simulado hasta tener Meta Business.
- Corre en el mismo VPS de Oracle que staging, como un proyecto de Compose aparte (`infra/produccion/compose.yml`, proyecto `aulisima-prod`): base, volúmenes, claves y `.env` propios. Lo único compartido con staging es el Caddy del servidor.
- Dominio `aulisima.com` (y `www` que redirige a él). Staging sigue en `staging.aulisima.com`.
- Producción usa una versión fija (`VERSION` = commit ya probado en staging), nunca `main`.
- Los respaldos cifrados a Azure se repiten para producción con su propio contenedor de blobs, su healthcheck y su prueba de restauración semanal.
- Con dos proxies delante de la API (Caddy y nginx), `Proxies:Saltos=2` hace que el límite por IP de RNF-004 vea la IP del usuario.
- La instancia se amplía dentro de la cuota gratuita (hasta 4 OCPU, 24 GB y 200 GB de disco) y la cuenta de Oracle pasa a pago por uso, para que no la recuperen por inactividad.

## Alternativas

- VM en Azure (B2s, unos 650 a 750 MXN al mes): mismo esquema, con costo antes de tener ingresos.
- Servidor en Hetzner (unos 120 a 150 MXN al mes): barato, pero proveedor y cuenta nuevos.
- Servicios administrados de Azure (unos 700 a 900 MXN al mes): requiere guardar archivos fuera del disco antes de abrir.

## Consecuencias

- Un solo servidor para staging, producción y otra app: punto único de falla. Se acepta para la beta con RPO de 24 h y RTO de 4 h, como en ADR-0002.
- Un error en staging que agote memoria o disco puede afectar a producción; se vigila con las alertas de Grafana Cloud (ADR-0015).
- La migración a Azure (ADR-0007) sigue pendiente: se hace restaurando un respaldo de producción en el destino y cambiando el DNS, sin cambiar los enlaces de los tutores.
