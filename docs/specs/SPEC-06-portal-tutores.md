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

## Fuera de alcance

Mensajes del tutor al docente, pagos, justificantes, acceso del alumno.
