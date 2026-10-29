# Registro de decisiones (ADR)

Cada decisión técnica o de producto que cambie el stack, la arquitectura, un proveedor o el modelo de negocio se registra aquí. Una decisión aceptada no se edita: si cambia, se escribe un ADR nuevo que la reemplaza y se marca la anterior como **Reemplazada por ADR-XXXX**.

Formato: contexto, decisión, alternativas consideradas, consecuencias.

| ADR | Decisión | Estado | Fecha |
| --- | --- | --- | --- |
| [ADR-0001](ADR-0001-stack.md) | .NET 10, Angular PWA y PostgreSQL | Aceptada | 11/10/2026 |
| [ADR-0002](ADR-0002-hosting-piloto.md) | Piloto en servidor Oracle con Coolify | Aceptada, complementada por ADR-0007 | 11/10/2026 |
| [ADR-0003](ADR-0003-monolito-modular.md) | Monolito modular con Hangfire | Aceptada | 11/10/2026 |
| [ADR-0004](ADR-0004-ia-claude-haiku.md) | API de Claude con Haiku 5.5 por defecto | Aceptada | 11/10/2026 |
| [ADR-0005](ADR-0005-canales-notificacion.md) | WhatsApp con número del sistema; avisos por push y correo | Aceptada | 11/10/2026 |
| [ADR-0006](ADR-0006-precio-por-grupo.md) | Precio Pro por grupo de hasta 50 alumnos | Aceptada | 11/10/2026 |
| [ADR-0007](ADR-0007-migracion-hosting.md) | Oracle solo para pruebas; producción en Azure o similar | Aceptada | 11/10/2026 |
| [ADR-0008](ADR-0008-correo-brevo.md) | Correo transaccional con Brevo | Aceptada | 11/10/2026 |
| [ADR-0009](ADR-0009-pagos-stripe-oxxo.md) | Stripe: tarjeta como suscripción y OXXO como prepago | Aceptada | 11/10/2026 |
| [ADR-0010](ADR-0010-diseno-y-movimiento.md) | Diseño con Impeccable mejorado con los skills de animación y diseño | Aceptada | 11/10/2026 |
| [ADR-0011](ADR-0011-cimientos-h0.md) | Herramientas y decisiones de los cimientos (H0) | Aceptada | 11/10/2026 |
| [ADR-0012](ADR-0012-tipos-de-rubro-extensibles.md) | Tipos de rubro como catálogo administrable con comportamientos en código | Aceptada | 11/10/2026 |
| [ADR-0013](ADR-0013-cuenta-docente.md) | Cuenta docente: tablas sin RLS por clave de búsqueda, tokens con hash, Hangfire tras el commit y retención | Aceptada | 11/10/2026 |
| [ADR-0014](ADR-0014-grupos-alumnos-tutores.md) | Grupos, alumnos y tutores: tutor compartido por función, unidades sin traslape, numeración de lista e importación sin guardar archivos | Aceptada | 11/10/2026 |
| [ADR-0015](ADR-0015-observabilidad-grafana-cloud.md) | Observabilidad en Grafana Cloud: OTLP, Faro por el mismo origen y alertas como código | Aceptada | 29/10/2026 |
