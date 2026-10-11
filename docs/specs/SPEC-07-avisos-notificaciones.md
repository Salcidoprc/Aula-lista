# SPEC-07: Avisos y notificaciones

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-070 a RF-073, RNF-022

## Objetivo

Que el docente avise a los tutores sin usar su WhatsApp personal y sin que el costo de WhatsApp se dispare.

## Canales

| Canal | Costo | Cuándo se usa |
| --- | --- | --- |
| Push del navegador (Web Push con VAPID) | Gratis | Siempre que el tutor lo haya activado |
| Correo | Casi gratis | Si el tutor registró correo |
| WhatsApp del sistema (Cloud API, plantillas de utilidad) | 0.0085 USD por mensaje de utilidad, unos 0.16 MXN (tabla de Meta vigente desde 1/10/2026; ya se cobra también dentro de la ventana de 24 h) | Boleta disponible y avisos urgentes en grupos Pro, dentro del límite |
| WhatsApp del docente (enlace `wa.me` con texto prellenado) | Gratis | Siempre disponible; lo envía el docente manualmente |

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-070 | Un aviso tiene título (hasta 80 caracteres) y cuerpo (hasta 1,000). No admite archivos adjuntos en el MVP. |
| RN-071 | Destinatarios: todos los tutores del grupo o tutores seleccionados. |
| RN-072 | Orden de entrega por tutor: push y correo si los tiene. WhatsApp del sistema solo si el aviso está marcado como urgente, el grupo es Pro y hay saldo en el límite mensual (RN-086). |
| RN-073 | Tutores sin push, correo ni WhatsApp disponible aparecen en una lista "Compartir manualmente" con botón `wa.me` por tutor. |
| RN-074 | El texto de WhatsApp del sistema usa plantillas aprobadas por Meta; el contenido libre del aviso no va en el mensaje, solo un enlace al portal. |
| RN-075 | Un aviso se marca leído cuando el tutor lo abre en el portal (RF-063). Abrir el WhatsApp no cuenta. |
| RN-076 | Los avisos no se editan después de enviados; se pueden borrar y queda en auditoría. |

## Plantillas de WhatsApp (por registrar en Meta)

| Nombre | Categoría | Texto propuesto |
| --- | --- | --- |
| `boleta_disponible` | Utilidad | "Hola {{1}}, ya está disponible el reporte de {{2}} del {{3}}. Consúltalo aquí: {{4}}" |
| `aviso_urgente` | Utilidad | "Hola {{1}}, hay un aviso importante del grupo {{2}}. Léelo aquí: {{3}}" |
| `enlace_acceso` | Utilidad | "Hola {{1}}, este es tu enlace para ver el avance escolar de tus hijos: {{2}}" |

El remitente es el número del sistema con el nombre comercial verificado. Los webhooks de estado de Meta actualizan `mensaje_whatsapp.estado` (enviado, entregado, leído, fallido).

## API

| Método y ruta | Descripción |
| --- | --- |
| `GET` / `POST /api/v1/grupos/{id}/avisos` | Lista y crea avisos (envío inmediato) |
| `GET /api/v1/avisos/{id}` | Detalle con entregas y lecturas por tutor |
| `DELETE /api/v1/avisos/{id}` | Borra el aviso |
| `GET /api/v1/avisos/{id}/compartir` | Enlaces `wa.me` por tutor para envío manual |
| `POST /api/v1/webhooks/whatsapp` | Estados de Meta (firma verificada) |

## Pantalla

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

**Avisos:** lista con fecha, título y "leído por 18 de 30". Botón Nuevo aviso: destinatarios, título, cuerpo, interruptor Urgente (solo Pro, muestra saldo de mensajes). Detalle con estado por tutor y la lista de compartir manualmente.

## Criterios de aceptación

- Dado un aviso no urgente a 30 tutores, entonces no se envía ningún WhatsApp del sistema y los tutores con push o correo lo reciben.
- Dado un aviso urgente en un grupo Pro sin saldo de WhatsApp, entonces se envía por push y correo, y el resto queda en "Compartir manualmente" con un mensaje que explica el límite.
- Dado un tutor que abre el aviso en el portal, entonces el docente lo ve como leído en menos de 1 minuto.
- Dado un webhook de WhatsApp con firma inválida, entonces se rechaza con 401 y no cambia ningún estado.

## Fuera de alcance

Avisos programados, adjuntos, respuestas de tutores, chat.
