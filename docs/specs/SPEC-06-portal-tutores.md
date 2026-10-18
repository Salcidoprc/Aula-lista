# SPEC-06: Portal de tutores

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-060 a RF-065, RNF-001, RNF-003, RNF-019

## Objetivo

Que un tutor vea a todos sus hijos con un solo enlace, sin contraseña ni app, y que su acceso nunca exponga datos de otros alumnos.

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-060 | Cada tutor tiene un enlace personal con un token de 256 bits. Solo se guarda el hash. |
| RN-061 | El tutor ve a todos los alumnos vinculados a su teléfono, de cualquier docente. Cada docente solo ve el vínculo con sus propios alumnos. |
| RN-062 | El enlace vence al terminar el ciclo escolar más 30 días; se puede revocar en cualquier momento. Al abrirlo, se crea una sesión en el navegador (cookie `HttpOnly` de 90 días renovable) para no depender del enlace cada vez. |
| RN-063 | El docente puede reenviar el enlace vigente o regenerarlo. Regenerar revoca el anterior y cierra las sesiones abiertas con él. Queda en auditoría. |
| RN-064 | El tutor solo ve lo publicado: boletas publicadas, observaciones publicadas y avisos dirigidos a él. Nunca ve notas del docente, borradores ni calificaciones de actividades individuales. |
| RN-065 | Un enlace usado desde más de 5 dispositivos distintos en 30 días se marca como sospechoso y se avisa al docente (posible reenvío del enlace). |
| RN-066 | El portal funciona en el navegador; instalarlo como app (PWA) es opcional y se sugiere solo para activar notificaciones en iPhone. |

## Pantallas

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

1. **Mis hijos:** tarjeta por alumno con nombre, grupo, docente, último aviso y boleta más reciente. Si hay un solo hijo, se abre directo su detalle.
2. **Detalle del alumno:** pestañas Boletas (por periodo, con PDF), Avisos (con fecha, leído automáticamente) y Calificaciones (finales publicadas por periodo).
3. **Notificaciones:** activar push, registrar o cambiar correo.
4. **Enlace inválido o vencido:** mensaje claro: "Pide a la maestra o al maestro un enlace nuevo", sin revelar si el enlace existió.

## API

| Método y ruta | Descripción |
| --- | --- |
| `POST /api/v1/tutor/sesion` | Canjea el token del enlace por una sesión |
| `GET /api/v1/tutor/alumnos` | Hijos vinculados |
| `GET /api/v1/tutor/alumnos/{id}` | Detalle publicado del alumno |
| `GET /api/v1/tutor/alumnos/{id}/avisos` | Avisos del alumno; marca como leídos |
| `GET /api/v1/boletas/{id}/pdf` | PDF (verifica vínculo) |
| `PUT /api/v1/tutor/contacto` | Correo y suscripción push |
| `POST /api/v1/alumnos/{id}/tutores/{tutorId}/enlace` | (Docente) reenviar o regenerar enlace |

## Casos borde

- El tutor cambia de número: el docente edita el teléfono; se genera un enlace nuevo y se revoca el anterior.
- Dos tutores del mismo alumno: cada uno tiene su enlace.
- El tutor abre el enlace en otro celular: funciona, salvo que exceda RN-065.
- Alumno dado de baja: el tutor conserva sus boletas ya publicadas (RN-017).

## Criterios de aceptación

- Dado un tutor con dos hijos de docentes distintos, cuando abre su enlace, entonces ve a ambos hijos.
- Dado un docente, cuando consulta a ese tutor, entonces solo ve su vínculo con sus propios alumnos.
- Dado un token alterado o revocado, entonces la API responde 401 sin indicar si existió.
- Dado un tutor, cuando intenta pedir el PDF de una boleta de un alumno no vinculado, entonces recibe 404.
- Dado un tutor que abrió el enlace una vez, cuando vuelve a entrar a la app desde el mismo navegador dentro de 90 días, entonces entra sin el enlace.
- Dado el portal en un Android de gama baja con 4G, entonces "Mis hijos" carga en menos de 4 segundos.

## Decisiones de implementación (H5.1)

- **Partido en dos entregas:** H5.1 trae el enlace, la sesión, Mis hijos, el detalle con Boletas y Calificaciones, la boleta en HTML y el envío del enlace desde la ficha. La pestaña Avisos, el "último aviso" de la tarjeta y la pantalla Notificaciones (con `GET .../avisos` y `PUT /tutor/contacto`) llegan en H5.2 junto con SPEC-07: sin avisos serían pantallas vacías.
- **H5.2:** la pestaña Avisos (tercera del detalle, con el número de no leídos) los marca leídos al abrirla; la tarjeta de Mis hijos muestra el último aviso con un punto si no se ha leído; `/tutor/notificaciones` activa el push en ese navegador (en iPhone primero explica cómo agregar Aula Lista a la pantalla de inicio, RN-066) y guarda el correo. Los enlaces de avisos y boletas llevan `?alumno=<id>&ver=avisos` y el portal abre directo en ese hijo y pestaña.
- **El token viaja en el fragmento:** el enlace es `{UrlWeb}/tutor#<token>`; el fragmento no llega a bitácoras ni a cabeceras `Referer`. La web lo canjea con `POST /tutor/sesion` y lo borra de la barra de direcciones. Token de 256 bits en base64url; se guarda su SHA-256.
- **Reenviar crea un enlace nuevo** sin revocar los anteriores: el token original no se puede recuperar porque solo se guarda el hash. **Regenerar** revoca todos los enlaces y sesiones del tutor y queda en auditoría (`enlace_tutor_regenerado`; también `enlace_tutor_enviado` y `enlace_tutor_sospechoso`).
- **Vigencia (RN-062):** el enlace vence 30 días después del fin del ciclo del alumno, o 30 días desde hoy si eso es más tarde. La sesión es la cookie `aula_lista_tutor` (`HttpOnly`, `Secure`, `SameSite=Strict`, ruta `/api/v1`) de 90 días; se renueva con el uso, a lo más una escritura al día. Abrir el enlace en un navegador que ya tiene sesión del mismo tutor la reutiliza.
- **Sospechoso (RN-065):** más de 5 sesiones nuevas de un enlace en 30 días lo marcan una sola vez, se audita y se avisa por correo al docente que lo creó, sin nombres, con liga a la ficha. La ficha muestra el aviso y "Regenerar enlace". El canje tiene límite de 10 por minuto por IP (`Tutores:CanjesPorMinuto`).
- **Respuestas:** token alterado, vencido o revocado responden igual, 401 `enlace_invalido`, y la web lleva a "Este enlace ya no funciona". Un alumno o boleta no vinculado responde 404. El PDF de la boleta es el mismo endpoint del docente y acepta cualquiera de las dos sesiones.
- **Aislamiento (RNF-001):** `enlace_tutor` y `sesion_tutor` tienen RLS forzado. Antes de conocer al tutor, las funciones `tutor_del_enlace(hash)` y `tutor_de_la_sesion(hash)` (invoker) fijan `app.token_hash` solo dentro de la búsqueda. Con `app.tutor_id` fijo, políticas de lectura del tutor en `vinculo_tutor`, `alumno`, `grupo`, `docente`, `boleta` (solo publicadas y vigentes), `periodo` y `unidad`.
- **Configuración:** sección `Tutores` (`DiasSesion` 90, `DiasGraciaEnlace` 30, `DispositivosPorEnlace` 5, `DiasVentanaDispositivos` 30, `CanjesPorMinuto` 10).
- **Lo que ve el tutor (RN-064):** la boleta vigente publicada con su final, el promedio y peso de cada rubro y la observación; nunca actividades individuales, notas ni borradores. La versión corregida dice "Versión corregida el dd/mm/aaaa" (RN-052).
- **Texto del WhatsApp:** el docente lo manda desde su propio WhatsApp con `wa.me` y el mensaje prellenado; el servidor no envía nada en H5.1.

## Fuera de alcance

Mensajes del tutor al docente, pagos, justificantes, acceso del alumno.
