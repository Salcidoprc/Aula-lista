# SPEC-01: Cuenta y acceso del docente

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-001 a RF-008, RNF-002, RNF-004, RNF-005, RNF-006

## Objetivo

Que un docente cree su cuenta en menos de 2 minutos desde el celular, con la base legal necesaria para registrar datos de menores.

## Historias de usuario

- Como docente, quiero registrarme con mi correo o con Google para empezar sin trámites.
- Como docente, quiero recuperar mi contraseña si la olvido.
- Como docente, quiero exportar mis datos y borrar mi cuenta cuando ya no use la app.

## Reglas de negocio

| ID | Regla |
| --- | --- |
| RN-001 | El correo debe verificarse antes de crear el primer alumno. Se puede crear grupos y explorar sin verificar. |
| RN-002 | La contraseña tiene mínimo 10 caracteres; no se exigen símbolos, pero se rechazan contraseñas de listas filtradas comunes. |
| RN-003 | Al registrarse se aceptan términos y aviso de privacidad vigentes. Si cambia la versión, se pide aceptar de nuevo al iniciar sesión. |
| RN-004 | La declaración de autorización de datos (RF-006) se pide una vez, antes del primer alumno, y se guarda con fecha y versión. |
| RN-005 | Una cuenta de Google y una de correo con la misma dirección son la misma cuenta. |
| RN-006 | Al solicitar la eliminación, la cuenta se desactiva de inmediato, los enlaces de tutores de sus grupos se revocan y los datos se borran a los 30 días. |
| RN-007 | Los vínculos de tutores con alumnos de otros docentes no se tocan al borrar una cuenta. |

## Sesión

- Token de acceso JWT de 15 minutos y token de actualización rotativo de 30 días en cookie `HttpOnly`, `Secure`, `SameSite=Strict`.
- El token de actualización se invalida al usarse (rotación) y, si se detecta reutilización, se revocan todas las sesiones del docente y queda en auditoría.
- "Cerrar sesión en todos los dispositivos" revoca todos los tokens de actualización del docente.
- La sesión no expira mientras el docente use la app al menos una vez cada 30 días. Esto atiende la queja de sesiones que se cierran solas.

## API

Decisiones de implementación en [ADR-0013](../decisiones/ADR-0013-cuenta-docente.md).

| Método y ruta | Descripción |
| --- | --- |
| `POST /api/v1/cuentas/registro` | Crea cuenta con correo y contraseña; envía correo de verificación e inicia sesión (201) |
| `POST /api/v1/cuentas/verificar-correo` | Confirma el correo con el token recibido |
| `POST /api/v1/cuentas/verificar-correo/reenviar` | Reenvía el correo de verificación (autenticado; máximo 3 por hora) |
| `POST /api/v1/cuentas/sesion` | Inicia sesión con correo y contraseña |
| `POST /api/v1/cuentas/sesion/renovar` | Rota el token de actualización de la cookie y devuelve un token de acceso nuevo |
| `DELETE /api/v1/cuentas/sesion` | Cierra la sesión de este dispositivo y borra la cookie |
| `DELETE /api/v1/cuentas/sesiones` | Cierra todas las sesiones |
| `POST /api/v1/cuentas/recuperar` | Envía el enlace para restablecer la contraseña; responde 202 exista o no la cuenta |
| `POST /api/v1/cuentas/restablecer` | Define una contraseña nueva con el token recibido |
| `PUT /api/v1/cuentas/contrasena` | Cambia la contraseña; una cuenta solo con Google la define sin la actual |
| `GET /api/v1/cuentas/google/inicio?volver=` | Redirige a Google (OpenID Connect); `volver` es una ruta de la web a la que se regresa al terminar |
| `GET /api/v1/cuentas/google/callback` | Retorno de Google, lo atiende el middleware de autenticación |
| `GET /api/v1/cuentas/google/completar` | Crea o vincula la cuenta (RN-005), deja la cookie de sesión y redirige a `volver` en la web; fuera del OpenAPI |
| `GET` / `PUT /api/v1/docente/perfil` | Lee y actualiza el perfil |
| `POST /api/v1/docente/aceptaciones` | Registra aceptación de un documento legal |
| `POST /api/v1/docente/exportacion` | Encola la exportación de datos; se envía por correo un enlace válido 24 h |
| `GET /api/v1/docente/exportaciones/{id}` | Estado de una exportación propia (`pendiente`, `lista`, `fallida`) y su vencimiento |
| `GET /api/v1/docente/exportaciones/{id}/{formato}` | Descarga el archivo `excel` o `pdf` de una exportación vigente |
| `DELETE /api/v1/docente` | Solicita eliminación de la cuenta (pide contraseña o reautenticación con Google) |

Google se activa solo si la API tiene `Google__ClientId` y `Google__ClientSecret`; sin ellos, `/callback` y `/completar` no existen y `/inicio` regresa a `/cuenta/entrar?error=google_no_disponible`. `volver` solo acepta rutas locales de la web. El token de actualización viaja en la cookie `aula_lista_sesion` (`HttpOnly`, `Secure`, `SameSite=Strict`, ruta `/api/v1/cuentas`); vence a los 30 días y cada renovación vuelve a contar 30 días. El token de acceso va en el cuerpo de la respuesta y se manda en `Authorization: Bearer`.

## Pantallas

Diseño y transiciones según `reglas/07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño); los movimientos de este módulo están en su catálogo.

1. **Registro e inicio de sesión:** correo y contraseña, botón de Google, enlace a recuperar.
2. **Bienvenida:** perfil (nombre, nivel, escuela, CCT opcional, estado) y aceptación legal en un solo paso.
3. **Ajustes de cuenta:** perfil, cerrar sesiones, exportar datos, eliminar cuenta, plan.

## Casos borde

- El correo de verificación no llega: botón para reenviar, máximo 3 por hora.
- El docente se registra con Google y luego quiere contraseña: puede definirla desde Ajustes.
- Token de verificación vencido (24 h): se ofrece reenviar.

## Criterios de aceptación

- Dado un docente nuevo, cuando se registra con correo, entonces recibe un correo de verificación y puede crear un grupo, pero no alumnos, hasta verificar.
- Dado un docente con sesión iniciada, cuando no abre la app en 29 días y vuelve, entonces sigue con su sesión activa.
- Dado un token de actualización ya usado, cuando alguien lo reutiliza, entonces se revocan todas las sesiones de ese docente y queda en auditoría.
- Dado un docente que pide eliminar su cuenta, cuando confirma, entonces no puede iniciar sesión, los enlaces de sus tutores dejan de funcionar para sus grupos y a los 30 días no quedan datos suyos en la base.
- Dados 6 intentos fallidos de inicio de sesión en un minuto, entonces el sexto se rechaza con `demasiados_intentos`.

## Fuera de alcance

Inicio de sesión con Microsoft o Apple, autenticación de dos factores, cuentas de escuela.
