# SPEC-09: Operación y métricas

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-090 a RF-093, RNF-005, RNF-009, RNF-015, RNF-016, RNF-021, RNF-022

## Objetivo

Poder medir las métricas del piloto, vigilar costos y operar el servicio sin entrar a la base de datos.

## Eventos de producto (RF-090)

Se guardan en `evento_producto` sin datos personales (solo IDs internos).

| Evento | Propiedades | Métrica que alimenta |
| --- | --- | --- |
| `docente_registrado` | origen (correo, google) | Registro |
| `grupo_creado` | nivel, alumnos | Activación |
| `calificacion_capturada` | vista (actividad, tabla, alumno), sin_conexion | Docentes activos |
| `cierre_iniciado` | grupo_id, alumnos | Cierre |
| `observacion_aprobada` | distancia_edicion (0 a 1), regeneraciones | Aceptación de la IA |
| `boletas_publicadas` | grupo_id, boletas, minutos_desde_inicio_cierre | Tiempo de cierre |
| `enlace_tutor_abierto` | primera_vez | Apertura de tutores |
| `suscripcion_activada` | grupos_pro, modalidad (tarjeta, prepago_oxxo) | Conversión |

## Panel de administración (RF-091 a RF-093)

Área separada de la app del docente (`/admin`), solo para usuarios con rol `admin`. El rol se asigna por configuración o por un comando de la API, nunca desde la interfaz.

### Secciones

| Sección | Qué hace | Puede editar |
| --- | --- | --- |
| Métricas | Las 6 métricas del piloto de `00-prd.md` sección 4, por semana | No |
| Docentes | Lista con grupos, plan, último acceso y consumo del mes | No |
| Costos | Costo estimado del mes por grupo (tokens de IA × precio y mensajes de WhatsApp × tarifa) contra ingreso | No |
| Errores | Errores recientes de jobs (IA, WhatsApp, correo, pagos) | No |
| Tipos de rubro | Catálogo de tipos de rubro que ven los docentes (RF-092) | Sí |
| Auditoría | Acciones de los administradores (RF-093) | No |

### Reglas

| ID | Regla |
| --- | --- |
| RN-090 | El panel no muestra nombres de alumnos, teléfonos, correos de tutores, notas ni observaciones. Los docentes se muestran con su correo y nombre. |
| RN-091 | Un tipo de rubro tiene nombre, descripción, icono, comportamiento, forma de calificar sugerida (numérica o por cumplimiento), nombres de rubro sugeridos, orden y estado (activo o inactivo). El comportamiento se elige de los que existen en el código desplegado (en el MVP: `actividades` y `asistencia`, ADR-0012). |
| RN-092 | El administrador crea un tipo nuevo con un comportamiento existente y queda disponible para los docentes al instante (por ejemplo "Participaciones" o "Laboratorio" con comportamiento `actividades`). |
| RN-093 | Un comportamiento nuevo (otra forma de calcular o de capturar, con su propia pantalla) se desarrolla: primero se documenta en la spec que corresponda, luego se implementa con su cálculo, su pantalla de captura y sus vectores de prueba, y al desplegarse aparece como opción en el panel. |
| RN-094 | Un tipo con rubros que lo usan no se borra: se desactiva. Desactivarlo lo oculta al crear rubros nuevos; los rubros existentes siguen funcionando. |
| RN-095 | Toda acción de edición en el panel queda en la auditoría con administrador, fecha, acción y valores antes y después (RNF-005). |

### API

| Método y ruta | Descripción |
| --- | --- |
| `GET /api/v1/tipos-rubro` | Tipos activos para los docentes (se guarda sin conexión) |
| `GET` / `POST /api/v1/admin/tipos-rubro` | Lista todos y crea (solo `admin`) |
| `PUT /api/v1/admin/tipos-rubro/{id}` | Edita o desactiva (solo `admin`) |
| `GET /api/v1/admin/comportamientos` | Comportamientos disponibles en el código desplegado |
| `GET /api/v1/admin/metricas`, `/docentes`, `/costos`, `/errores`, `/auditoria` | Lecturas del panel |

## Observabilidad

- Logs estructurados con Serilog en JSON; nunca se registran nombres, teléfonos, correos, textos de notas u observaciones, ni tokens.
- Trazas y métricas con OpenTelemetry; se exportan a un servicio gestionado (por definir) o a Grafana en el mismo servidor.
- Monitoreo externo de disponibilidad cada minuto sobre `/health` y alerta por correo y Telegram si falla 3 veces seguidas.
- Alertas: tasa de errores 5xx mayor a 2% en 5 minutos, job fallido tras sus reintentos, respaldo fallido, costo de un grupo mayor a 8 MXN en el mes.

## Respaldos y retención

- `pg_dump` diario cifrado (age o GPG) subido a almacenamiento de objetos fuera del servidor (Oracle Object Storage u otro proveedor), retención 30 días.
- Prueba de restauración el primer lunes de cada mes en staging, registrada en `docs/operacion/restauraciones.md`.
- Job diario de retención (RNF-009): elimina grupos archivados con más de 2 ciclos y cuentas con eliminación solicitada hace más de 30 días.

## Decisiones de implementación (H6.2)

- **Eventos sin duplicados:** cada evento lleva una `clave` y `(docente_id, nombre, clave)` es única con `ON CONFLICT DO NOTHING`, así un reintento o la cola sin conexión no cuentan dos veces. Claves: el id del grupo, del periodo, de la observación o del evento de pago; `calificacion_capturada` una por día, vista y si fue sin conexión; `enlace_tutor_abierto` el id del enlace la primera vez y luego uno por día.
- **Qué se cuenta:** `observacion_aprobada` solo cuando hubo borrador de la IA (`distancia_edicion` es la distancia de Levenshtein normalizada entre `borrador_ia` y `texto_final`); `boletas_publicadas` la primera publicación del periodo, con los minutos desde `periodo.cierre_iniciado_en`; `suscripcion_activada` cuando el docente pasa de cero grupos pagados a alguno.
- **Métricas por semana:** de lunes a domingo, hora del centro de México, las últimas `Operacion:SemanasMetricas` (12). Docentes activos: capturaron en los 14 días que terminan con la semana. Sin datos se muestra "sin datos", nunca 0 %. Las metas viven en `Operacion:Metas`.
- **Lecturas del panel:** funciones `admin_*` (`SECURITY DEFINER`, del rol de migraciones) que primero llaman a `exigir_admin()`; ese rol solo tiene políticas de lectura para ellas. Nunca devuelven alumnos, tutores, notas ni observaciones (RN-090).
- **Telemetría de solo inserción:** `evento_producto`, `error_trabajo` y `auditoria_admin` tienen RLS forzado; `app_runtime` solo inserta y nadie más que las funciones del panel las lee.
- **Errores de procesos:** `error_trabajo` guarda origen, trabajo, docente, referencia y el tipo de la excepción o un código (`push_fallido`, `whatsapp_fallido`), nunca el mensaje, que puede traer datos de alumnos. Se registran al agotar los reintentos: IA, envíos a tutores (push, correo, WhatsApp), boletas, exportación, revisión de pagos y limpieza.
- **Rol admin:** por configuración (`Operacion__CorreosAdmin__0`) o con `dotnet AulaLista.Api.dll --admin <correo>`, que pide una cuenta activa y verificada y deja `rol_admin_asignado` en la auditoría. La app del docente no tiene enlace al panel; quien no es admin y abre `/admin` vuelve a sus grupos.
- **Costos:** IA = tokens × precio del modelo (`Ia:Precio*UsdPorMillon`) × `Operacion:TipoCambioUsdMxn`; WhatsApp = mensajes × tarifa. Se marca un grupo arriba de `Operacion:AlertaCostoGrupoMxn` (8) o con IA arriba de `AlertaCostoIaGrupoMxn` (2).
- **Trazas:** OpenTelemetry (ASP.NET Core sin `/salud`, HttpClient y Npgsql) se exporta por OTLP solo si existe `OTEL_EXPORTER_OTLP_ENDPOINT`; sin esa variable no se instrumenta nada.
- **Logs sin datos personales:** la prueba automática corre los flujos con nombres y teléfonos marcados y nivel `Debug`, y revisa cada línea del log.
- **Respaldos:** `infra/respaldos/respaldar.sh` (`pg_dump` → `age` → `rclone`, con el conteo de filas de la misma instantánea, cifrado al lado) con el rol `app_respaldos` (`infra/postgres/respaldos.sql`, solo lectura). `restaurar.sh` restaura en una base vacía y compara conteos. Cómo programarlo en `infra/coolify.md`; cada prueba se anota en `docs/operacion/restauraciones.md`.
- **Retención (RNF-009):** el trabajo diario borra primero las cuentas vencidas y luego los grupos archivados cuyo ciclo terminó hace más de `Operacion:AniosGruposArchivados` (2) años; si falla, queda en errores y Hangfire reintenta.

## Criterios de aceptación

- Dado un administrador, cuando crea el tipo "Participaciones" con comportamiento `actividades`, entonces los docentes lo ven al crear un rubro sin desplegar nada.
- Dado un tipo usado por algún rubro, cuando el administrador intenta borrarlo, entonces solo se permite desactivarlo y los rubros existentes siguen calculándose.
- Dado un docente sin rol `admin`, cuando llama a `/api/v1/admin/...`, entonces recibe 404.
- Dado cualquier cambio en tipos de rubro, entonces aparece en la auditoría con valores antes y después.

- Dado el panel de administración, entonces muestra las 6 métricas del piloto calculadas desde los eventos.
- Dado cualquier log de la API, entonces no contiene nombres ni teléfonos (prueba automática con datos marcados).
- Dado un respaldo del día anterior, cuando se restaura en staging, entonces la app arranca y los conteos coinciden.
