# Reglas de seguridad y privacidad

El sistema guarda datos de menores. Ante la duda, se recolecta menos y se comparte menos.

## Datos personales

- Solo se guardan los datos listados en el modelo de `02-arquitectura.md`. Agregar un campo personal nuevo requiere actualizar el aviso de privacidad y un ADR.
- Prohibido en el MVP: CURP, fecha de nacimiento, domicilio, fotos de alumnos, datos de salud, calificaciones de conducta numéricas.
- Las notas del docente son privadas: nunca se exponen a tutores ni en exportaciones para tutores.

## Seudonimización ante terceros

- A la API de Claude solo se envía el seudónimo del alumno, el trato gramatical, calificaciones, notas y estilo (SPEC-04). Nunca nombre, apellidos, teléfono, correo, escuela ni CCT.
- A WhatsApp solo se envía el nombre del tutor, el nombre corto del grupo y el enlace al portal; nunca calificaciones ni observaciones.
- Las fotos de listas (SPEC-02) se procesan y se descartan; no se guardan. Son la única excepción a la seudonimización ante la IA: la foto trae los nombres y no hay forma de quitarlos antes. Por eso solo se pide copiar los nombres, la imagen no se escribe a disco ni a la base, y ni ella ni los nombres leídos llegan a los logs (se registran tokens, renglones y resultado).

## Autenticación y sesiones

- Contraseñas con el hasher de ASP.NET Core Identity. Nunca se registran ni se devuelven.
- Tokens de enlace de tutor y de verificación: aleatorios criptográficos de 256 bits, guardados como hash SHA-256, comparación en tiempo constante.
- Cookies `HttpOnly`, `Secure`, `SameSite=Strict` (o `Lax` solo donde el flujo de Google lo requiera).
- Límites de tasa según RNF-004 con el middleware de rate limiting de ASP.NET Core.

## Autorización

- Toda consulta se filtra por dueño en la aplicación y con RLS en la base (doble barrera).
- Un recurso de otro dueño responde 404, no 403, para no revelar que existe.
- Los webhooks verifican firma (Stripe, Meta) antes de procesar.

## Infraestructura

- Solo HTTPS; HSTS activado. Puertos de la base no expuestos a internet.
- Secretos en variables de entorno de Coolify; rotación al menos anual y de inmediato si alguien sale del proyecto.
- Respaldos cifrados y fuera del servidor (SPEC-09).
- Dependencias revisadas con `dotnet list package --vulnerable` y `npm audit` en CI; las vulnerabilidades altas bloquean el merge.

## Cumplimiento (LFPDPPP)

- Aviso de privacidad visible en el registro y en el portal del tutor, con la versión vigente.
- Solicitudes ARCO por correo dedicado; respuesta en máximo 20 días hábiles (RNF-006).
- Registro de aceptación legal por docente (RF-005, RF-006).
- Revisión legal externa antes de abrir el piloto a docentes reales.

## Incidentes

Ante una sospecha de acceso indebido: revocar tokens afectados, preservar logs, evaluar el alcance en menos de 72 horas y notificar a los docentes afectados según lo que indique la revisión legal.
