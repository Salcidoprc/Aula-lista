# ADR-0015: Observabilidad en Grafana Cloud

**Estado:** Aceptada · **Fecha:** 29/10/2026

## Contexto

SPEC-09 pedía logs, trazas, métricas, monitoreo de `/salud` y alertas, pero dejaba el destino "por definir". La API ya escribía logs JSON a la consola y tenía OpenTelemetry apagado mientras no hubiera a dónde mandar. El piloto necesita un destino gratis, sin tarjeta y sin otro servidor que mantener, que cubra también los errores del navegador.

Planes gratis revisados en las páginas oficiales (29/10/2026):

| Servicio | Lo que da gratis |
| --- | --- |
| Grafana Cloud | 50 GB de logs, 50 GB de trazas, 10 mil series de métricas, 14 días, 50 mil sesiones de Faro, 100 mil ejecuciones de Synthetic Monitoring, alertas, 3 usuarios, sin tarjeta |
| Sentry Developer | 5 mil errores, 5 GB de logs, 30 días, 1 usuario; sin métricas de Prometheus ni monitor de disponibilidad con alertas propias |
| Better Stack | 3 GB de logs y trazas por 3 días, 10 monitores |
| Axiom | 500 GB, 30 días, OTel; sin errores del navegador ni monitoreo de disponibilidad |

## Decisión

- **Grafana Cloud, plan gratis**, para logs (Loki), trazas (Tempo), métricas (Prometheus), errores del navegador (Faro), disponibilidad (Synthetic Monitoring) y alertas.
- **Solo OTLP estándar:** la API usa las variables `OTEL_EXPORTER_OTLP_*`; el sink de Serilog para logs y el exportador de OpenTelemetry para trazas y métricas las leen igual. Cambiar de proveedor es cambiar tres variables.
- **Métricas propias en vez de consultas a la base desde Grafana:** jobs fallidos y grupos con costo alto salen de la API como métricas. Grafana nunca se conecta a PostgreSQL.
- **Faro por el mismo origen:** nginx reenvía `/faro` al colector, así la CSP sigue en `connect-src 'self'`, los bloqueadores no lo cortan y Grafana no ve la IP de docentes ni tutores. La web carga Faro con `import()` solo si el servidor dice que está activo, y no pesa en el bundle inicial (RNF-011).
- **Sin datos personales (RN-019):** Faro no captura la consola ni identifica al usuario; quita consulta y fragmento de toda URL (el token del tutor viaja en `#`).
- **Alertas como código** en `infra/grafana/`, aplicadas con la API de aprovisionamiento desde un flujo manual de GitHub Actions con secretos del repositorio. Los datasources se nombran por su uid por defecto en Grafana Cloud (`grafanacloud-prom`, `grafanacloud-logs`).

## Consecuencias

- Grafana Labs es un encargado más para el aviso de privacidad: recibe IDs internos, rutas, tipos de error y métricas, nunca nombres, teléfonos ni correos. El abogado (PA-11) debe incluirlo.
- La retención es de 14 días; lo que deba durar más (errores de jobs, auditoría) sigue en la base y en el panel.
- Si el piloto supera el plan gratis, el primer límite que se toca son las 10 mil series; las métricas no llevan ids como etiqueta para no multiplicarlas.

## Alternativas

- **Grafana en el mismo servidor:** gratis pero consume la memoria del servidor del piloto y no avisa si el propio servidor se cae.
- **Sentry:** mejor para errores, pero no cubre métricas ni la alerta de 5xx por tasa, y tendríamos dos herramientas.
