# ADR-0002: Piloto en servidor Oracle con Coolify

**Estado:** Aceptada, complementada por [ADR-0007](ADR-0007-migracion-hosting.md) · **Fecha:** 11/10/2026

## Contexto

Ya existe un servidor de prueba en Oracle Cloud con Coolify. El piloto es de unos 30 docentes y el costo fijo debe ser casi cero.

## Decisión

- Web (nginx con Angular), API (.NET 10) y PostgreSQL corren como contenedores en ese servidor, administrados por Coolify, con TLS automático.
- Staging y producción del piloto conviven en el servidor con dominios y bases separadas.
- Respaldos diarios cifrados a almacenamiento de objetos externo (SPEC-09).
- Si el servidor es ARM (Ampere), se construyen imágenes `linux/arm64`; .NET 10 y PostgreSQL lo soportan.

## Alternativas

- Vercel para Angular y Railway para API y base: más simple de operar, con costo mensual.
- Azure completo: más integrado con .NET, más caro al inicio.

## Consecuencias

- Un solo servidor es punto único de falla: se acepta para el piloto con RPO 24 h y RTO 4 h.
- Antes de pasar de 100 docentes de pago, revisar mover PostgreSQL a un servicio administrado (nuevo ADR).
