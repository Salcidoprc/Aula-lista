# ADR-0017: Catálogo de productos y precios administrable, sincronizado con Stripe

**Estado:** Aceptada · **Fecha:** 14/11/2026

## Contexto

Pro tenía un solo precio (79 MXN el primer grupo y 29 MXN cada adicional) en la configuración (`Planes:PrecioPrimerGrupo`, `Planes:PrecioGrupoAdicional`) y dos `price_` de Stripe en variables de entorno. Habrá más cobros por funciones distintas, así que el administrador debe crear y cambiar productos y precios desde el panel, sin desplegar. Los precios siguen viviendo en Stripe, y en Stripe un precio no se edita: se crea otro y el viejo se archiva.

## Decisión

- **Funciones en código, productos en datos.** Cada función que se cobra es una clave del código (`cierre_ia`, `boletas`, `whatsapp_boletas`, `avisos_urgentes`), como los comportamientos de ADR-0012. Un **producto** (`producto`) junta funciones, tiene nombre, descripción, orden, los meses que se pueden prepagar en OXXO y un estado: `borrador`, `activo` o `archivado`. En el MVP el alcance es `grupo` (se cobra por grupo, RN-082) y solo uno puede estar a la venta a la vez.
- **Precios por versión.** `precio_producto` guarda cada versión con el precio del primer grupo, el del adicional, la moneda y sus `price_` de Stripe. Cambiar el precio crea la versión siguiente como `pendiente`; cuando sus dos precios existen en Stripe pasa a `vigente` y la anterior a `archivado` (y se archiva en Stripe con `active=false`). Así la venta nunca se queda sin precio.
- **Quien ya paga conserva su precio.** `suscripcion.precio_id` y `pago.precio_id` guardan la versión con que se contrató; viaja en la metadata del Checkout (`precio_id`) y vuelve en el webhook. Cambiar grupos de una suscripción usa los precios de su versión. Mover suscriptores a un precio nuevo queda fuera de alcance.
- **Sincronizar sin perder nada.** Cada cambio se guarda primero en la base con su auditoría (RN-095) y luego se lleva a Stripe (productos y precios por la API REST, con `Idempotency-Key` por versión). Si Stripe falla, el error queda en el producto y el panel ofrece reintentar; lo que ya se creó no se duplica.
- **Las funciones deciden los candados.** `AccesoPro.TieneFuncionAsync` responde si el grupo tiene Pro y si el producto que paga (o el que se vende, en la prueba) incluye la función. Quitar una función de un producto la bloquea para quien lo tenga.
- **Siembra y compatibilidad.** La migración crea el producto "Pro" con las cuatro funciones, OXXO por 1, 3 o 6 meses y la versión 1 a 79 y 29 MXN, y apunta a ella todas las suscripciones y pagos existentes. Mientras esa versión no tenga sus `price_` propios, usa los de `Pagos:PrecioPrimerGrupoId` y `Pagos:PrecioGrupoAdicionalId` si están; la primera sincronización los adopta (y adopta el producto de Stripe al que pertenecen). Desde entonces esas dos variables son opcionales.

## Consecuencias

- Los precios y los meses de OXXO salen de `Planes` en la configuración; el panel es la única forma de cambiarlos. Moneda, días de prueba y gracia, límites y montos de OXXO siguen en `Planes`.
- Con `Pagos:Proveedor=simulado` el catálogo crea ids `prod_sim_` y `price_sim_` y todo el flujo se prueba sin Stripe.
- Un producto nuevo con alcance distinto de `grupo` (por docente o por escuela) necesita su propio cálculo de cobro: se documenta primero en SPEC-08.
