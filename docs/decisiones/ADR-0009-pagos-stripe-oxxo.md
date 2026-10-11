# ADR-0009: Stripe con tarjeta como suscripción y OXXO como prepago

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

Muchos docentes no pagan con tarjeta en línea. El dueño del producto quiere ofrecer tarjeta y OXXO con Stripe (PA-08). Según la documentación de Stripe (docs.stripe.com/payments/oxxo), OXXO:

- No admite pagos recurrentes ni se puede usar con Billing, Invoicing o el portal de clientes.
- Solo funciona como pago único en Checkout o Payment Intents, de 10 a 10,000 MXN.
- El ticket vence de 1 a 7 días; el dinero se liquida en T+4; no hay reembolsos ni disputas.
- Está prohibido para el giro MCC 6538 (Software).

## Decisión

- **Tarjeta:** suscripción mensual con Stripe Billing, por grupo (RN-082).
- **OXXO:** pago único de 3 o 6 meses con Checkout en modo pago. La app guarda `pagado_hasta`, recuerda antes del vencimiento y aplica la gracia al vencer (RN-084, RN-089).
- El estado solo cambia por webhooks verificados e idempotentes (RN-087).

## Alternativas

- Solo tarjeta: más simple, deja fuera a quien paga en efectivo.
- Mercado Pago: admite efectivo, pero requiere otro proveedor e integración.

## Consecuencias

- Hay dos caminos de cobro que probar y un job diario que revisa vencimientos de prepago.
- Antes del piloto se confirma con Stripe que el giro de la cuenta permite OXXO. Si no, se ofrece solo tarjeta y se reevalúa Mercado Pago en un ADR nuevo.
