# Requerimientos

Fuente de verdad de lo que el sistema debe hacer (funcionales, `RF`) y de cómo debe comportarse (no funcionales, `RNF`). Cada requerimiento tiene un ID estable: no se reutiliza ni se renumera. Si un requerimiento se elimina, se marca como **Retirado** y se deja en la tabla.

- **Prioridad:** `M` = obligatorio para el MVP, `D` = deseable en el MVP, `F2` / `F3` = fases 2 y 3.
- **Spec:** documento en `docs/specs/` que lo detalla y define sus criterios de aceptación.

## Requerimientos funcionales

### Cuenta y acceso del docente (SPEC-01)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-001 | El docente se registra con correo y contraseña y verifica su correo antes de capturar datos de alumnos. | M |
| RF-002 | El docente puede registrarse e iniciar sesión con su cuenta de Google. | M |
| RF-003 | El docente puede recuperar su contraseña por correo. | M |
| RF-004 | El docente completa su perfil: nombre, nivel (primaria o secundaria), nombre de la escuela, CCT opcional y estado de la república. | M |
| RF-005 | Al registrarse, el docente acepta términos y aviso de privacidad; se guarda la versión aceptada y la fecha. | M |
| RF-006 | Antes de registrar alumnos, el docente declara que la escuela o los tutores autorizaron el uso de los datos. | M |
| RF-007 | El docente puede cerrar sesión en todos sus dispositivos. | M |
| RF-008 | El docente puede exportar todos sus datos (Excel y PDF) y solicitar la eliminación de su cuenta. | M |
| RF-009 | La app tiene tema claro y oscuro. Por defecto sigue el modo del celular o la computadora; en Ajustes el docente elige Automática, Clara u Oscura y la elección se guarda en ese dispositivo. | D |

### Grupos, alumnos y tutores (SPEC-02)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-010 | El docente crea un grupo con nombre, nivel, grado, materia (opcional, para secundaria) y ciclo escolar. | M |
| RF-011 | Cada grupo se evalúa por las unidades de su ciclo escolar (un periodo por unidad, con estado propio por grupo). | M |
| RF-012 | El docente agrega, edita y da de baja alumnos manualmente. La baja es lógica y conserva el historial. | M |
| RF-013 | El docente importa alumnos y tutores desde Excel (.xlsx) o CSV, con vista previa y corrección antes de guardar. | M |
| RF-014 | El docente importa alumnos pegando una lista de texto. | M |
| RF-015 | El docente importa alumnos desde una foto de la lista; la IA extrae los nombres y el docente confirma. | D |
| RF-016 | Cada alumno puede tener hasta 3 tutores con nombre, WhatsApp, correo opcional y parentesco. | M |
| RF-017 | Un tutor se identifica por su número de WhatsApp: si ya existe, se vincula al nuevo alumno en lugar de duplicarse. | M |
| RF-018 | Un grupo tiene como máximo 50 alumnos activos. | M |
| RF-019 | El docente archiva un grupo al terminar el ciclo; queda de solo lectura. | M |
| RF-020 | El docente crea varios grupos a la vez (por ejemplo 1°A a 1°F de la misma materia) con datos comunes y periodos iguales. | M |
| RF-021 | El docente define su ciclo escolar con nombre, fecha de inicio y fecha de fin. | M |
| RF-022 | El docente divide el ciclo en unidades (bimestre, trimestre, cuatrimestre, semestre, mensual o personalizada) con nombre y fechas editables; la configuración aplica a todos los grupos del ciclo. | M |

### Evaluación y captura (SPEC-03)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-030 | El docente crea los rubros que decida por periodo (unidad), con nombre libre y peso en porcentaje; los pesos deben sumar 100. Cada rubro tiene un tipo del catálogo que gestiona el administrador. | M |
| RF-031 | El docente copia los rubros de otro periodo o de otro grupo. | M |
| RF-032 | El docente crea las actividades que quiera dentro de un rubro (nombre y fecha dentro de la unidad); el rubro promedia sus actividades. | M |
| RF-033 | El docente captura calificaciones en una tabla alumnos por actividades, usable en celular y en computadora. | M |
| RF-034 | Al agregar un rubro o un criterio, el docente elige si se califica con número o por cumplimiento (✅ cumplió / ❌ no cumplió) y cuál es su calificación mínima. Una celda puede marcarse como "No entregó" (cuenta como la mínima) o "Exento" (no cuenta). | M |
| RF-035 | El sistema calcula el promedio de cada rubro y la calificación del periodo en tiempo real. Las calificaciones admiten hasta 2 decimales; cada grupo elige cuántos decimales muestra la calificación final (0, 1 o 2) y el sentido del redondeo (arriba, abajo o al más cercano). | M |
| RF-036 | El docente registra notas por alumno (texto o dictado del teclado del sistema) con etiqueta opcional: fortaleza, área de mejora o conducta. | M |
| RF-037 | La captura de calificaciones y notas funciona sin conexión y se sincroniza sola al recuperar la señal. | M |
| RF-038 | Un rubro puede tener criterios (subrubros) con nombre y peso que suman 100; cada actividad del rubro se califica por criterio. | M |
| RF-039 | El docente arma equipos en el grupo y califica una actividad por equipo, con la opción de ajustar a cada integrante por separado. | M |

### Asistencia (SPEC-10)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-100 | El docente pasa lista por grupo y fecha con un toque por alumno (presente, falta, retardo, justificada) y "Todos presentes". | M |
| RF-101 | El docente consulta y corrige el historial de asistencia de una unidad con el porcentaje de cada alumno. | M |
| RF-102 | Un rubro de tipo asistencia calcula su promedio con las tomas de lista de las fechas de la unidad. | M |
| RF-103 | Pasar lista funciona sin conexión y se sincroniza igual que la captura. | M |

### Cierre de periodo y observaciones con IA (SPEC-04)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-040 | El docente inicia el cierre de un periodo; desde ese momento los pesos de los rubros quedan fijos. | M |
| RF-041 | El sistema genera borradores de observación por alumno, para todo el grupo o para alumnos seleccionados. | M |
| RF-042 | El docente edita, regenera (con una instrucción opcional como "más breve") y aprueba cada observación. | M |
| RF-043 | El docente configura el estilo de las observaciones: longitud, tono y hasta 3 ejemplos propios. | D |
| RF-044 | La pantalla de cierre muestra el estado de cada alumno: sin generar, borrador, aprobada. | M |
| RF-045 | El sistema guarda el borrador original de la IA y el texto aprobado para medir cuánto se editó. | M |
| RF-046 | El docente puede reabrir una observación aprobada mientras la boleta no esté publicada. | M |

### Boletas (SPEC-05)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-050 | El sistema genera una boleta PDF por alumno con escuela, docente, grupo, periodo, calificaciones por rubro, calificación final y observación aprobada. | M |
| RF-051 | El docente descarga un solo PDF con las boletas de todo el grupo. | M |
| RF-052 | El docente publica las boletas en el portal de tutores, por grupo o por alumno. | M |
| RF-053 | Al publicar, el sistema notifica a los tutores por WhatsApp del sistema, push o correo; si se agotó el límite de WhatsApp, el docente comparte desde su propio WhatsApp. | M |
| RF-054 | El docente puede corregir y volver a emitir una boleta publicada; se guarda la versión anterior. | M |
| RF-055 | El docente exporta las calificaciones del periodo a Excel para llenar formatos oficiales. | M |

### Portal de tutores (SPEC-06)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-060 | El tutor entra con un enlace personal, sin contraseña ni instalar nada. | M |
| RF-061 | El tutor ve a todos sus hijos vinculados, aunque sean de distintos docentes. | M |
| RF-062 | Por cada hijo, el tutor ve calificaciones publicadas, observaciones publicadas, boletas PDF y avisos. | M |
| RF-063 | Un aviso se marca como leído cuando el tutor lo abre. | M |
| RF-064 | El tutor puede activar notificaciones push y registrar un correo. | M |
| RF-065 | El docente puede reenviar, revocar o regenerar el enlace de un tutor. | M |

### Avisos y notificaciones (SPEC-07)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-070 | El docente crea avisos para todo el grupo o para tutores seleccionados. | M |
| RF-071 | Los avisos se entregan por push y correo; WhatsApp del sistema solo si el aviso es urgente y el plan lo permite. | M |
| RF-072 | El docente siempre puede compartir un aviso o un enlace desde su propio WhatsApp. | M |
| RF-073 | El docente ve qué tutores leyeron cada aviso. | M |

### Planes y pagos (SPEC-08)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-080 | Todo docente inicia en plan Gratis: grupos, captura, notas, exportación a Excel y avisos. El cierre con IA y las boletas son Pro. | M |
| RF-081 | Cada docente tiene una prueba Pro gratuita en 1 grupo, que el docente elige, durante su primer cierre de periodo. | M |
| RF-082 | El plan Pro se contrata por grupo: un precio para el primer grupo y otro por cada grupo adicional, en un solo cobro mensual. | M |
| RF-083 | El docente activa o desactiva Pro por grupo; los cambios se prorratean. | M |
| RF-084 | El pago es con Stripe: tarjeta como suscripción mensual u OXXO como pago adelantado de 3 o 6 meses; el sistema avisa antes de que venza lo pagado. | M |
| RF-085 | Si falla un cobro hay 7 días de gracia; después los grupos pasan a Gratis sin borrar datos. | M |
| RF-086 | El sistema cuenta los mensajes de WhatsApp del sistema por grupo y mes, y bloquea al llegar al límite del plan. | M |
| RF-087 | El docente puede solicitar factura (CFDI). | F2 |

### Operación interna (SPEC-09)

| ID | Requerimiento | Prioridad |
| --- | --- | --- |
| RF-090 | El sistema registra eventos de producto para medir las métricas del piloto. | M |
| RF-091 | Existe un panel de administración con métricas del piloto, docentes, costos y consumo de IA y WhatsApp, y errores de jobs. | M |
| RF-092 | El administrador gestiona el catálogo de tipos de rubro: crea tipos con un comportamiento existente, los edita y los desactiva. | M |
| RF-093 | Las acciones de los administradores quedan en auditoría. | M |
| RF-094 | El panel abre con un resumen del negocio: docentes activos, registros, embudo de activación, retención por cohorte, MRR y pagos, costo y margen, y lo que la app entregó (boletas, observaciones y avisos), con su tendencia semanal. | M |

## Requerimientos no funcionales

| ID | Categoría | Requerimiento | Cómo se verifica |
| --- | --- | --- | --- |
| RNF-001 | Seguridad | Un docente solo accede a sus grupos y un tutor solo a sus hijos. Se aplica en la API y con Row Level Security en PostgreSQL. | Pruebas automáticas de aislamiento en CI |
| RNF-002 | Seguridad | Todo el tráfico usa HTTPS (TLS 1.2 o superior). Los respaldos se cifran. | Revisión de configuración |
| RNF-003 | Seguridad | Los tokens de enlace de tutor tienen al menos 256 bits de entropía y se guardan solo como hash SHA-256. | Prueba unitaria y revisión de código |
| RNF-004 | Seguridad | Límites de tasa: inicio de sesión 5 intentos por minuto por IP y cuenta; generación con IA 200 solicitudes por hora por docente. | Prueba de integración |
| RNF-005 | Seguridad | Bitácora de auditoría de acciones sensibles: publicar boletas, exportar datos, revocar enlaces, eliminar alumnos. | Prueba de integración |
| RNF-006 | Privacidad | Cumplimiento de la LFPDPPP: aviso de privacidad, minimización de datos y atención de derechos ARCO en máximo 20 días hábiles. | Checklist legal antes del lanzamiento |
| RNF-007 | Privacidad | A la IA no se envían nombres, teléfonos ni correos: solo un seudónimo, calificaciones y notas. | Prueba unitaria del armado del prompt |
| RNF-008 | Privacidad | No se recolecta CURP, fecha de nacimiento, domicilio ni fotos de alumnos en el MVP. | Revisión del modelo de datos |
| RNF-009 | Privacidad | Los grupos archivados se conservan 2 ciclos escolares; después se eliminan. Las cuentas eliminadas se borran en 30 días. | Job programado con prueba |
| RNF-010 | Rendimiento | La app carga en menos de 4 segundos en un Android de gama baja con 4G (Lighthouse móvil, Slow 4G). | Lighthouse en CI |
| RNF-011 | Rendimiento | El bundle inicial del frontend pesa máximo 250 KB comprimido. | Presupuesto de Angular en el build |
| RNF-012 | Rendimiento | Las lecturas de la API responden en menos de 300 ms en el percentil 95, sin contar la IA. | Métricas de OpenTelemetry |
| RNF-013 | Rendimiento | La generación de observaciones de un grupo de 50 alumnos termina en menos de 3 minutos. | Prueba de carga |
| RNF-014 | Disponibilidad | Captura de calificaciones, notas y asistencia sin conexión durante al menos 7 días sin pérdida de datos. | Prueba end-to-end |
| RNF-015 | Disponibilidad | Disponibilidad mensual de 99% en el piloto y 99.5% en producción. | Monitoreo externo |
| RNF-016 | Disponibilidad | Respaldo diario cifrado fuera del servidor, retención 30 días. RPO 24 h, RTO 4 h. Prueba de restauración mensual. | Registro de restauraciones |
| RNF-017 | Escalabilidad | Un servidor soporta 1,000 docentes y 50,000 alumnos sin cambiar la arquitectura. | Prueba de carga |
| RNF-018 | Usabilidad | Accesibilidad WCAG 2.1 AA y objetivos táctiles de al menos 44 px. | Auditoría con axe en CI |
| RNF-019 | Usabilidad | Compatibilidad: Chrome Android y Safari iOS 16.4 o superior (últimas 2 versiones), Chrome y Edge de escritorio. | Pruebas end-to-end en navegadores |
| RNF-020 | Usabilidad | Español de México, fechas dd/mm/aaaa, zona horaria America/Mexico_City por defecto. Todos los textos en archivos de traducción. | Revisión de código |
| RNF-021 | Operación | Logs estructurados sin datos personales, trazas y métricas con OpenTelemetry, alertas de errores. | Revisión de logs |
| RNF-022 | Costos | El costo variable por grupo Pro no supera 8 MXN al mes. Consumo de IA y WhatsApp medido por grupo. | Panel de administración |
| RNF-023 | Mantenibilidad | Cobertura de pruebas de al menos 80% en dominio y aplicación; CI obligatorio en cada PR. | Reporte de cobertura |
| RNF-024 | Mantenibilidad | Toda decisión técnica que cambie el stack o la arquitectura queda en un ADR en `docs/decisiones/`. | Revisión de PR |
| RNF-025 | Usabilidad | Cada pantalla se diseña y revisa con el skill Impeccable y sigue el sistema de diseño de `DESIGN.md` (reglas/07-diseno-y-movimiento.md). | `/impeccable audit` y `/impeccable critique` anotados en el PR |
| RNF-026 | Usabilidad | Las transiciones y el movimiento combinan los skills de animación disponibles (Emil Kowalski, Design Motion Principles, `/impeccable animate`) con estas reglas: duraciones menores a 300 ms, `ease-out`, solo `transform` y `opacity`, nada animado en acciones repetidas, y respeto a `prefers-reduced-motion`. | `review-animations` y auditoría de `design-motion-principles` en el PR; prueba end-to-end con movimiento reducido |
| RNF-027 | Usabilidad | El tema claro y el oscuro cumplen WCAG 2.1 AA cada uno: todos los pares de color de `tokens.css` se calculan en CI y la suite end-to-end, con sus revisiones de axe, corre también con el sistema en modo oscuro. | `npm run contraste:verificar` y proyecto `celular-oscuro` de Playwright en CI |
