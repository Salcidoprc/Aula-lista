# ADR-0013: Decisiones de la cuenta docente (H1)

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

H1 implementa SPEC-01 en la API: registro, verificación de correo, sesión con renovación, Google, recuperación de contraseña, perfil, aceptaciones legales, exportación y eliminación de la cuenta. Varias piezas no estaban fijadas: qué tablas llevan Row Level Security cuando el docente todavía no se conoce, cómo se guardan los tokens, cómo corren los trabajos en segundo plano sin romper los roles de la base, cómo se borra lo vencido y dónde viven los archivos y el correo mientras no hay almacenamiento de objetos.

## Decisión

- **Tablas sin RLS:** `cuenta`, `token_cuenta` y `sesion` se leen antes de saber quién es el docente (inicio de sesión, verificación, recuperación, renovación). No tienen RLS. El aislamiento ahí es por la clave de búsqueda: el hash del token o el correo normalizado (en minúsculas y sin espacios al inicio o al final). Solo se alcanzan desde los casos de uso de `Cuentas` en Application; ningún otro módulo las consulta.
- **Tablas con RLS:** `docente` (política `docente_propio` sobre `id`), `aceptacion_legal` y `exportacion` (política por `docente_id`) tienen RLS activado y forzado con `app.docente_id` y además filtro global de EF Core, como el resto de las tablas con datos de un docente. `docente.id` es el mismo `id` de la cuenta.
- **Tablas solo de inserción:** a `app_runtime` se le revoca `UPDATE` y `DELETE` en `aceptacion_legal`, y `SELECT`, `UPDATE` y `DELETE` en `auditoria`. `auditoria` no tiene RLS; la API solo puede escribir en ella.
- **Tokens:** todo token que viaja (verificación de correo, restablecimiento, actualización de sesión) son 32 bytes aleatorios (256 bits) en Base64URL. En la base solo se guarda su SHA-256 en hexadecimal, con índice único. Verificación vence a las 24 h; restablecimiento a 1 h. Cada token es de un solo uso.
- **Sesión:** token de acceso JWT de `Cuentas__MinutosTokenAcceso` (15) minutos con la familia de sesión como claim. El token de actualización va en la cookie `aula_lista_sesion` (`HttpOnly`, `Secure`, `SameSite=Strict`, ruta `/api/v1/cuentas`) y vence a los `Cuentas__DiasSesion` (30) días desde la última renovación. Cada renovación marca la fila usada y emite otra de la misma familia. Si llega un token ya usado y no revocado, se revocan **todas** las sesiones de la cuenta y se registra `reutilizacion_token_actualizacion` en `auditoria`.
- **Límites de inicio de sesión:** 5 intentos por minuto por IP con el limitador de ASP.NET Core y 5 por minuto por correo con un limitador en memoria (basta con un solo servidor, ADR-0003). Ambos responden `demasiados_intentos`.
- **Trabajos:** Hangfire guarda su estado en PostgreSQL en el esquema `hangfire`. Lo instala el migrador (`--migrar`) con el rol `app_migraciones` y da a `app_runtime` permisos de datos sobre ese esquema; en ejecución la API arranca con `PrepareSchemaIfNecessary = false`, sin DDL. Los casos de uso piden trabajos a `IColaTrabajos`; `IDespachoTrabajos` los retiene y los encola solo después de que la transacción de la solicitud se confirma, para que el trabajo nunca corra sin ver sus datos. Cada trabajo abre su propio alcance y su transacción, y asume al docente dueño para que RLS aplique igual que en una solicitud. Solo viajan IDs. `Trabajos__Servidor=false` apaga el servidor de Hangfire en una instancia.
- **Retención:** la función `eliminar_cuentas_vencidas(limite)` es `SECURITY DEFINER` (corre como el dueño del esquema) y `app_runtime` solo tiene `EXECUTE` sobre ella. Por cada cuenta con eliminación pedida hace 30 días o más fija `app.docente_id`, borra sus exportaciones, aceptaciones, perfil y cuenta (las sesiones y tokens caen en cascada) y anula la IP de sus filas de auditoría. La llama el trabajo recurrente `retencion-cuentas` todos los días a las 03:00 de `America/Mexico_City`, que además borra los archivos de exportación con más de 24 h.
- **Archivos:** las exportaciones se guardan en disco del servidor bajo `Almacen__Ruta` (un volumen) detrás de `IAlmacenArchivos`, y se ofrecen 24 h. La implementación local se cambia por almacenamiento de objetos al pasar a Azure (ADR-0007) sin tocar Application.
- **Correo:** `Correo__Proveedor=brevo` envía por la API transaccional de Brevo (ADR-0008) con `Correo__BrevoApiKey` por variable de entorno; si falta, la validación de `OpcionesCorreo` falla al primer envío (hoy no se valida al arrancar). `Correo__Proveedor=buzon` guarda los correos en memoria para local, end-to-end y pruebas. Con el buzón, y nunca en el entorno `Production`, existe `GET /api/v1/desarrollo/correos?para=...` (anónimo y fuera del OpenAPI) para leer los correos enviados.
- **Contraseñas:** se usa el `PasswordHasher` de ASP.NET Core Identity (PBKDF2) sin el resto de Identity. Su adaptador vive en Api porque las pruebas de arquitectura prohíben que Infrastructure dependa de `Microsoft.AspNetCore`; Application solo conoce `IHasherContrasenas`.
- **Google:** se registra el esquema OpenID Connect solo si hay `Google__ClientId` y `Google__ClientSecret`. Sin ellos, `GET /api/v1/cuentas/google/inicio` redirige a la web con `error=google_no_disponible` y las demás rutas de Google no existen. Al terminar, la API deja la cookie de sesión y redirige a `Cuentas__UrlWeb` más la ruta `volver`, que solo acepta rutas locales para no abrir redirecciones a otros sitios.

## Alternativas

- RLS también en `cuenta`, `token_cuenta` y `sesion` con una variable fijada antes de conocer al docente: obliga a abrir la política para la búsqueda anónima y no aísla más que buscar por el hash.
- Revocar solo la familia del token reutilizado: SPEC-01 pide revocar todas las sesiones del docente ante una reutilización; se elige lo más seguro.
- Encolar en Hangfire dentro de la transacción: el trabajo puede correr antes del commit y no encontrar la exportación.
- Borrado por retención desde la API con `BYPASSRLS`: daría a `app_runtime` un permiso que reglas/03 prohíbe; la función acota ese poder a una sola operación.
- Identity completo para cuentas: trae tablas y flujos que no se usan y mezcla nombres en inglés en el modelo.

## Consecuencias

- Toda consulta nueva a `cuenta`, `token_cuenta` o `sesion` debe pasar por `IRepositorioCuentas` y buscar por hash, correo normalizado o `cuenta_id` del token; las pruebas de integración lo cubren.
- Cada cambio en las tablas de un docente que se borran por retención debe actualizar `eliminar_cuentas_vencidas` en la misma migración.
- El buzón y el limitador por correo viven en memoria: se pierden al reiniciar y no sirven con varias instancias de la API.
- El volumen de `Almacen__Ruta` debe existir y ser escribible por el usuario del contenedor.
- Al migrar a Azure se agrega una implementación de `IAlmacenArchivos` sobre almacenamiento de objetos.
