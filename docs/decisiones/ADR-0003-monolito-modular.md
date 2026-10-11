# ADR-0003: Monolito modular con Hangfire

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

Equipo pequeño, un solo servidor y un dominio acotado. Hay trabajo asíncrono: generación con IA, PDFs, notificaciones y webhooks.

## Decisión

- Una sola API desplegable, dividida en módulos con límites claros (ver `02-arquitectura.md`), verificados con pruebas de arquitectura.
- Trabajo asíncrono con Hangfire sobre PostgreSQL, dentro del mismo proceso.

## Alternativas

- Microservicios: costo operativo injustificado para el tamaño actual.
- Cola externa (RabbitMQ, SQS): otra pieza que operar; no hace falta con este volumen.

## Consecuencias

- Si los jobs afectan la latencia de la API, se separan en un segundo contenedor con el mismo código (sin cambiar la arquitectura).
