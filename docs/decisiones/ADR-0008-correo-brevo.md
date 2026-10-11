# ADR-0008: Correo transaccional con Brevo

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

Se necesita correo para verificación de cuenta, recuperación de contraseña, avisos a tutores y recordatorios de pago. El dueño del producto ya tiene cuenta de Brevo (PA-09).

## Decisión

- Brevo como proveedor de correo transaccional, vía su API HTTP.
- Dominio propio con SPF, DKIM y DMARC configurados antes del piloto.
- El proveedor queda detrás de `IEnvioCorreo`; la llave de API solo vive en variables de entorno.

## Alternativas

- Amazon SES: más barato a gran volumen, más configuración inicial.
- Resend: buena experiencia de desarrollo, cuenta nueva.

## Consecuencias

- Los rebotes y quejas llegan por webhook de Brevo y marcan el correo del tutor como no válido.
- Vigilar el límite diario del plan de Brevo contratado al crecer el piloto.
