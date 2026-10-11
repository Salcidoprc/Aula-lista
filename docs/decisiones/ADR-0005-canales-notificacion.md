# ADR-0005: WhatsApp con número del sistema; avisos por push y correo

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

Los tutores viven en WhatsApp, pero la API oficial cobra por mensaje (0.0085 USD por mensaje de utilidad en México, unos 0.16 MXN, según la tabla de Meta vigente desde el 1/10/2026; confirmado el 11/10/2026 al cerrar PA-07). La API no puede enviar desde el WhatsApp personal de cada docente.

## Decisión

- Un número del sistema en WhatsApp Business Cloud API, con plantillas de utilidad (`boleta_disponible`, `aviso_urgente`, `enlace_acceso`).
- WhatsApp del sistema solo para boletas y avisos urgentes en grupos Pro, con límite mensual (RN-086).
- Avisos normales por Web Push y correo.
- El docente siempre puede compartir desde su propio WhatsApp con enlaces `wa.me`.

## Alternativas

- Todo por WhatsApp del sistema: unos 33 MXN al mes por grupo solo en avisos semanales; rompe el margen.
- Solo compartir manual: sin costo, pero vuelve al problema de usar el WhatsApp personal del docente.

## Consecuencias

- En iPhone, el push requiere que el tutor agregue el portal a su pantalla de inicio (iOS 16.4 o superior); el correo y el compartir manual cubren a quien no lo haga.
- Hay que registrar el negocio y verificar el número en Meta antes del piloto.
