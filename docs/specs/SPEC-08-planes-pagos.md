# SPEC-08: Planes y pagos

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-080 a RF-087, RF-095, RNF-022

## Objetivo

Cobrar por grupo de forma simple, que el docente pruebe lo que paga en su primer cierre y que el costo de cada grupo nunca supere su ingreso.

## Planes

| Plan | Qué incluye | Precio (hipótesis del piloto) |
| --- | --- | --- |
| Gratis | Grupos ilimitados, captura, notas, cálculo de promedios, exportación a Excel, avisos por push y correo, compartir por WhatsApp del docente | 0 |
| Prueba Pro | Todo Pro en 1 grupo, elegido por el docente, durante su primer cierre | 0 |
| Pro por grupo | Cierre de periodo con observaciones de IA, boletas PDF publicadas en el portal, notificación de boletas por WhatsApp del sistema, avisos urgentes por WhatsApp con límite | 79 MXN al mes el primer grupo, 29 MXN cada grupo adicional, IVA incluido |

Los precios, las funciones que incluye cada producto y los meses de OXXO viven en el catálogo de productos que el administrador gestiona en el panel (SPEC-09, RF-095) y en Stripe, nunca en el código ni en la configuración (ADR-0017). Las cifras de arriba son las de la versión 1 sembrada.

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-080 | Todo docente nuevo está en Gratis. |
| RN-081 | La prueba Pro cubre 1 grupo, que el docente elige al iniciar su primer cierre de periodo; los demás grupos siguen en Gratis. Se activa al iniciar ese cierre y dura 14 días o hasta publicar boletas de ese periodo, lo que ocurra después, con máximo 30 días. Se usa una sola vez por docente. |
| RN-082 | Pro se contrata por grupo. El cobro mensual = precio del primer grupo + precio adicional × (grupos Pro − 1), en una sola suscripción. |
| RN-083 | Activar un grupo Pro a mitad de mes se prorratea; desactivarlo aplica al siguiente corte, sin reembolso. |
| RN-084 | Hay dos modalidades de pago con Stripe. **Tarjeta:** suscripción mensual recurrente (Stripe Billing). **OXXO:** pago único por los meses que ofrece el producto (en la siembra: 1, 3 o 6) del total mensual vigente (Checkout en modo pago); OXXO no admite cobros recurrentes, por eso es prepago. El ticket vence en 3 días y el pago se confirma con el webhook; mientras tanto los grupos no cambian de plan. Montos permitidos por Stripe: de 10 a 10,000 MXN; OXXO no tiene reembolsos. |
| RN-089 | En prepago OXXO el sistema guarda `pagado_hasta`. Avisa en la app y por correo 7 días y 1 día antes; al vencer aplica la gracia de RN-085. Para agregar un grupo Pro durante un prepago, el docente paga ese grupo hasta `pagado_hasta` (tarjeta u OXXO); quitar grupos no genera reembolso. El docente puede cambiar a tarjeta al vencer su prepago. |
| RN-085 | Si un cobro falla, hay 7 días de gracia con aviso diario en la app. Después, los grupos pasan a Gratis; no se borra ningún dato y lo ya publicado sigue visible para los tutores. |
| RN-086 | Límite de WhatsApp del sistema por grupo Pro y mes: 1 mensaje de boleta por tutor por periodo publicado y hasta 4 avisos urgentes por tutor. Al llegar al límite se bloquea el envío por WhatsApp y se ofrece compartir manualmente. Los límites son configurables. |
| RN-087 | El estado de la suscripción en la base solo cambia por webhooks verificados del proveedor, procesados de forma idempotente. |
| RN-088 | Se guarda el consumo de tokens de IA y mensajes de WhatsApp por grupo y mes (`consumo_mensual`) para vigilar RNF-022. |

## Flujo de contratación

1. El docente elige qué grupos quiere en Pro y ve el total mensual.
2. Elige modalidad: tarjeta (mensual) u OXXO (1, 3 o 6 meses por adelantado, según el producto).
3. Se abre Stripe Checkout (pagos alojados por Stripe; la app nunca recibe datos de tarjeta). Con OXXO, Checkout muestra el ticket con la referencia para pagar en tienda.
4. Tarjeta: `checkout.session.completed` y los webhooks de suscripción actualizan `suscripcion` y `grupo.plan`. OXXO: `checkout.session.async_payment_succeeded` activa los grupos y fija `pagado_hasta`; `checkout.session.async_payment_failed` o el vencimiento del ticket no cambian nada.
5. Con tarjeta, el docente administra su método de pago y facturas desde el portal de clientes de Stripe. Con OXXO no hay portal (Stripe no lo permite); la app muestra el historial de pagos.

**Por confirmar con Stripe antes del piloto:** que el giro (MCC) de la cuenta permita OXXO; Stripe lo prohíbe para el MCC 6538. Si no se aprueba, solo se ofrece tarjeta.

## API

| Método y ruta | Descripción |
| --- | --- |
| `GET /api/v1/planes` | Precios y límites vigentes |
| `GET /api/v1/docente/suscripcion` | Estado, grupos Pro, próximo cobro, prueba |
| `POST /api/v1/docente/suscripcion/checkout` | Crea sesión de Checkout con los grupos elegidos y la modalidad (`tarjeta` o `oxxo` con `meses` de los que ofrece el producto) |
| `PUT /api/v1/docente/suscripcion/grupos` | Cambia los grupos Pro (prorrateo) |
| `POST /api/v1/docente/suscripcion/portal` | Enlace al portal de clientes |
| `POST /api/v1/webhooks/pagos` | Webhooks del proveedor (firma verificada) |
| `GET /api/v1/pagos-simulados/{id}`, `POST …/{id}/pagar`, `POST …/{id}/rechazar`, `POST /api/v1/pagos-simulados/renovacion` | Solo sin Stripe (`Pagos__Proveedor=simulado`): la página "Pago de prueba" y el cobro mensual simulado. Una sesión ajena responde 404 |

`POST /api/v1/periodos/{id}/cierre/iniciar` acepta `{ "usarPrueba": true }` para activar la prueba en ese grupo (RN-081).

## Pantallas

- **Plan (`/plan`):** el estado en una frase ("Pro en 3 grupos con tarjeta. Próximo cobro: 137 MXN el …", "Prueba Pro en 4° B: quedan 9 días.", "Pro en 2 grupos, pagado con OXXO hasta el …" o "Todos tus grupos están en Gratis."), la banda de pago si aplica, los grupos con su tapa y una casilla, el total al mes en vivo con su desglose ("79 primer grupo + 2 × 29"), la forma de pago (tarjeta cada mes, OXXO por cada mes que ofrece el producto, con su monto) y una sola acción. Con tarjeta: "Guardar cambios" y "Tarjeta y facturas" (portal). Durante un prepago: agregar grupos se paga hasta `pagado_hasta` y quitar no reembolsa. Abajo, qué incluye Gratis contra Pro (con el nombre del producto), su precio y, si el docente paga una versión anterior, "Tú conservas el precio con el que contrataste", y el historial de pagos. Quien ya paga ve su total y cambia grupos con el precio de su versión. Sin un producto a la venta, la acción de contratar se desactiva con "Por ahora no se puede contratar Pro". En celular la acción va fija abajo; en escritorio, en el panel lateral. `?grupo=` llega desde un candado con ese grupo marcado.
- **Candados:** en el cierre de un grupo Gratis, "El cierre con observaciones es de Pro" con "Usar mi prueba Pro en este grupo" (si no la ha usado) o "Activar Pro en este grupo"; en las boletas, "Las boletas son de Pro"; en un aviso nuevo, el interruptor Urgente apagado con "Con Pro, los urgentes también llegan por WhatsApp". El grupo de la prueba lleva "Prueba Pro · quedan n días" en el cierre y en las boletas, y "Pro" o "Prueba Pro" en Mis grupos.
- **Banda de pago** (Inicio y Plan): prepago por vencer en tinte de aviso (7 y 1 días antes); cobro fallido o prepago vencido, con los días de gracia que quedan, en tinte de peligro.
- **Pago de prueba (`/plan/pago-simulado/{id}`):** página propia de Aulísima, sin imitar a Stripe. Tarjeta: "Pagar" o "Simular tarjeta rechazada". OXXO: la ficha con su referencia y "Simular pago en tienda" o "Dejar vencer la ficha".

## Criterios de aceptación

- Dado un docente con 3 grupos Pro, entonces su cobro mensual es 79 + 2 × 29 = 137 MXN.
- Dado un docente nuevo con 3 grupos que inicia su primer cierre, cuando elige el grupo de prueba, entonces solo ese grupo tiene Pro y ve cuántos días le quedan.
- Dado un docente con 2 grupos Pro que paga 3 meses en OXXO, entonces paga 3 × 108 = 324 MXN y `pagado_hasta` queda 3 meses después de la confirmación del pago.
- Dado un ticket OXXO que vence sin pago, entonces los grupos siguen en su plan anterior.
- Dado un prepago OXXO que vence en 7 días, entonces el docente recibe aviso en la app y por correo.
- Dado un docente que ya usó su prueba, cuando inicia otro cierre, entonces no se activa una nueva prueba.
- Dado un cobro fallido, cuando pasan 7 días sin pago, entonces sus grupos pasan a Gratis y los tutores siguen viendo lo publicado.
- Dado el mismo webhook recibido dos veces, entonces el estado cambia una sola vez.
- Dado un docente que paga OXXO por 1 mes con 2 grupos, entonces paga 108 MXN y `pagado_hasta` queda un mes después de la confirmación (ADR-0017).
- Dado un docente que contrató a 79 + 29, cuando el administrador cambia el precio a 89 + 39, entonces el docente sigue pagando 79 + 29 al cambiar grupos y uno nuevo contrata a 89 + 39 (ADR-0017).
- Dado un producto al que el administrador le quita una función, entonces los grupos que pagan ese producto ya no la tienen (ADR-0017).
- Dado un grupo Pro que llegó al límite de WhatsApp del mes, cuando el docente publica boletas, entonces no se envían WhatsApp del sistema y se muestra la lista para compartir manualmente.

## Decisiones de implementación (H6.1)

- **La prueba se elige en el cierre** (decisión del usuario, 2026-10-18): no hay pantalla aparte; el candado del cierre la ofrece en ese grupo.
- **Webhook en línea:** el evento se verifica (firma `Stripe-Signature`, tolerancia de 300 s), se traduce a un evento propio y se aplica en la misma solicitud con `app.docente_id` del docente del evento. `evento_pago.id_externo` único lo vuelve idempotente (RN-087); un evento repetido responde 200 sin cambiar nada.
- **Pasarela simulada:** sin claves de Stripe (`Pagos__Proveedor=simulado`, el valor por omisión) el Checkout lleva a "Pago de prueba", que produce los mismos eventos que Stripe y los aplica en la solicitud. Así se prueba todo el flujo, incluida la gracia, sin cobrar.
- **Revisión diaria** (06:00, hora del centro de México): vence prepagos y pone la gracia, manda el correo de 7 y 1 días antes, termina la gracia y pasa a Gratis los grupos cuyo `pro_hasta` ya pasó. Lo publicado no se toca (RN-085).
- **Quitar grupos** con tarjeta los deja en Pro hasta el siguiente corte (`grupo.pro_hasta`); en prepago, hasta `pagado_hasta`. No hay reembolsos.
- **Agregar grupos a un prepago** (RN-089): diferencia del total mensual × días que faltan ÷ 30, en centavos, con tarjeta u OXXO en un pago único.
- **Consumo** (RN-088): `consumo_mensual` por grupo y mes lo suman triggers en `llamada_ia` y `mensaje_whatsapp`, no la aplicación.
- **Configuración:** días de prueba y gracia, avisos, vigencia de la ficha, montos de OXXO y si OXXO está habilitado viven en `Planes`; proveedor y claves de Stripe en `Pagos` (variables `PAGOS_PROVEEDOR`, `STRIPE_CLAVE_SECRETA`, `STRIPE_SECRETO_WEBHOOK`, `OXXO_HABILITADO`). Precios y meses de prepago están en el catálogo (ADR-0017); `STRIPE_PRECIO_*` solo sirven para que la versión 1 adopte los precios que ya existían en Stripe.

## Catálogo de productos (ADR-0017)

- **Producto:** nombre, descripción, funciones que incluye (`cierre_ia`, `boletas`, `whatsapp_boletas`, `avisos_urgentes`), meses de prepago OXXO (de 1 a 12; ninguno = solo tarjeta), orden y estado (`borrador`, `activo`, `archivado`). Se cobra por grupo (RN-082). Solo un producto puede estar a la venta.
- **Versión de precio:** primer grupo (1 a 10,000) y adicional (0 a 10,000), con 2 decimales, IVA incluido. Cada cambio es una versión nueva; la anterior se archiva en Stripe cuando la nueva ya existe allá.
- **Suscripción y pago** guardan su versión (`precio_id`). Quien ya paga conserva su precio; la prueba y las contrataciones nuevas usan el que se vende.
- **Candados por función:** el grupo tiene la función si tiene Pro y su producto la incluye.

## Fuera de alcance

Factura CFDI (F2), planes de escuela (F2), cupones y referidos, mover suscriptores a un precio nuevo, varios productos a la venta a la vez y productos cobrados por docente o por escuela.
