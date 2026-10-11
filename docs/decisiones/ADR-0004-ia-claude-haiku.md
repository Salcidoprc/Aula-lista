# ADR-0004: API de Claude con Haiku 5.5 por defecto

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

Las observaciones son textos cortos en español a partir de datos estructurados. El costo por grupo debe mantenerse bajo (RNF-022).

## Decisión

- Proveedor: API de Claude con el SDK oficial de C#.
- Modelo por defecto: `claude-haiku-5-5` ($0.10 por millón de tokens de entrada y $0.50 por millón de salida, precios de octubre de 2026), esfuerzo `low`.
- `claude-sonnet-5-5` ($2 / $10) solo si la evaluación de calidad muestra que Haiku no alcanza; el cambio es de configuración.
- Estimación: unos 0.40 MXN por periodo para un grupo de 30 alumnos con Haiku.

## Alternativas

- Sonnet por defecto: unas 20 veces más caro, sin evidencia de que haga falta.
- Modelos abiertos alojados en el servidor: el servidor del piloto no tiene capacidad para ello y la calidad en español sería menor.

## Consecuencias

- Haiku 5.5 no tiene respaldo automático en el servidor ante rechazos; se maneja como fallo controlado (SPEC-04).
- La evaluación de calidad (reglas/05-ia.md) es obligatoria para cambiar modelo o prompt.
