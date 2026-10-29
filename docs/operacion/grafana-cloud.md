# Monitoreo con Grafana Cloud

Cómo conectar Aula Lista a Grafana Cloud (plan gratis, ADR-0015) y qué hacer cuando llega una alerta. Lo que se mide y se alerta está en SPEC-09, sección Observabilidad.

Todo se puede preparar antes de tener servidor: la cuenta, las alertas y los avisos. Los datos empiezan a llegar cuando la API y la web corren en Coolify con sus variables.

## Qué se manda y qué no

| Señal | Desde | Lleva | Nunca lleva |
| --- | --- | --- | --- |
| Logs | API (Serilog) y respaldos | nivel, mensaje, IDs internos, ruta y código de respuesta | nombres, teléfonos, correos, notas, observaciones, tokens |
| Trazas | API | rutas con IDs, duración, consultas SQL sin valores | cadena de consulta, cuerpos |
| Métricas | API | solicitudes por ruta y código, jobs fallidos por origen, grupos con costo alto | IDs como etiqueta |
| Errores del navegador | Web (Faro), por el propio servidor | tipo y pila del error, ruta, navegador, Web Vitals | usuario, consola, consulta y fragmento de la URL, IP, cookies |

## 1. Crear la cuenta y el stack

1. Entra a [grafana.com](https://grafana.com/auth/sign-up/create-user) y crea una cuenta gratis (no pide tarjeta).
2. Al crear el stack, ponle un nombre corto (por ejemplo `aulalista`; quedará `https://aulalista.grafana.net`) y elige una región de Estados Unidos, la más cercana a México.
3. Invita a quien vaya a ver alertas (el plan gratis da 3 usuarios).

## 2. Logs, trazas y métricas de la API (OTLP)

1. En [grafana.com](https://grafana.com), entra a **My Account**, abre tu stack y en el recuadro **OpenTelemetry** pulsa **Configure**.
2. Pulsa **Generate now** para crear el token. La página muestra las variables listas: copia `OTEL_EXPORTER_OTLP_ENDPOINT` (termina en `/otlp`) y `OTEL_EXPORTER_OTLP_HEADERS` (empieza con `Authorization=Basic%20`). El token solo se ve una vez; guárdalo en tu gestor de contraseñas.
3. En Coolify, en el servicio `api`, agrega:

   | Variable | Valor |
   | --- | --- |
   | `OTEL_EXPORTER_OTLP_ENDPOINT` | el endpoint del paso 2 |
   | `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` |
   | `OTEL_EXPORTER_OTLP_HEADERS` | el encabezado del paso 2 (márcalo como secreto) |
   | `OTEL_RESOURCE_ATTRIBUTES` | `deployment.environment=staging` (o `produccion`) |

4. Pon las dos primeras y `OTEL_EXPORTER_OTLP_HEADERS` también en la tarea programada de respaldos (`infra/coolify.md`, paso 6), para que avise si un respaldo falla.
5. Redespliega. En Grafana, **Explore** → datasource de logs → `{service_name="aula-lista-api"}` debe mostrar líneas en un minuto.

## 3. Errores del navegador (Faro)

1. En tu stack de Grafana, menú **Frontend** → **Frontend Observability** → **Create new**.
2. Nombre `aula-lista-web`. En **CORS allowed origins** pon el dominio público de la web (por ejemplo `https://staging.aulalista.mx`).
3. Copia la **URL del colector** (`https://faro-collector-....grafana.net/collect/<clave>`).
4. En Coolify, en el servicio `web`, agrega `FARO_URL` con esa URL y `FARO_ENTORNO=staging`. Redespliega.
5. Comprueba: `https://<tu dominio>/observabilidad.json` responde `{"faro":true,...}`. Sin `FARO_URL` responde 404 y la web no carga nada de Faro.

El navegador nunca habla con Grafana: manda a `/faro` del mismo dominio y nginx lo reenvía sin cookies ni IP.

## 4. Monitor de `/salud` (Synthetic Monitoring)

1. Menú **Testing & synthetics** → **Synthetics** → **Add new check** → **API Endpoint** (HTTP).
2. **Job name:** `aula-lista-salud` (exacto: la alerta lo busca con ese nombre).
3. **Request target:** `https://<tu dominio>/salud`, método GET.
4. **Frequency:** 1 minuto. **Probe locations:** 2 de Norteamérica. Así son unas 87,600 ejecuciones al mes, dentro de las 100,000 gratis; con 3 sondas te pasarías.
5. Guarda. La alerta "/salud no responde" avisa cuando las dos sondas fallan 3 minutos seguidos.

## 5. A quién avisar: correo y Telegram

Correo: solo necesitas la dirección o direcciones (separadas por `;`).

Telegram (opcional, recomendado para lo urgente):

1. En Telegram, abre [@BotFather](https://t.me/BotFather), manda `/newbot`, ponle nombre y copia el **token** que te da.
2. Abre una conversación con tu bot y mándale cualquier mensaje (o agrégalo a un grupo y escribe ahí).
3. Abre `https://api.telegram.org/bot<TOKEN>/getUpdates` en el navegador y copia el número de `"chat":{"id": ...}` (en grupos empieza con `-`).

## 6. Aplicar las alertas desde GitHub

1. En Grafana: **Administration** → **Users and access** → **Service accounts** → **Add service account**, nombre `github-alertas`, rol **Admin** (la API de alertas como código lo pide). Luego **Add service account token** y copia el token.
2. En GitHub: **Settings** → **Secrets and variables** → **Actions** → **New repository secret**, uno por cada uno:

   | Secreto | Valor |
   | --- | --- |
   | `GRAFANA_URL` | `https://<tu stack>.grafana.net` |
   | `GRAFANA_TOKEN` | el token del paso 1 |
   | `ALERTA_CORREOS` | `tu@correo.mx` (o varios con `;`) |
   | `TELEGRAM_BOT_TOKEN` | el token del bot (opcional) |
   | `TELEGRAM_CHAT_ID` | el id del chat (opcional) |

3. **Actions** → **Configurar Grafana** → **Run workflow**. Termina con "Listo: 6 alertas en la carpeta Aula Lista".
4. En Grafana: **Alerting** → **Contact points** → "Aula Lista" → **Test**, para confirmar que llegan el correo y el mensaje.

Vuelve a correr el flujo cada vez que cambie `infra/grafana/` o los destinatarios. Reemplaza las alertas, los contactos y la política de avisos; lo que hayas cambiado a mano en esas partes se pierde.

## Las alertas

| Alerta | Cuándo | Repite | Qué hacer |
| --- | --- | --- | --- |
| API: más de 2 % de respuestas 5xx en 5 minutos | errores del servidor sobre el total, sin contar `/salud` | cada 4 h | Explore → logs de `aula-lista-api` con nivel Error; trazas con error en Tempo |
| Job fallido tras sus reintentos | algún trabajo (IA, WhatsApp, correo, push, pagos, boletas, exportación, retención) agotó sus reintentos | cada 4 h | `/admin` → Errores tiene docente, referencia y tipo |
| Respaldo fallido | `respaldar.sh` terminó con error | cada 4 h | salida de la tarea en Coolify; corregir y correrla a mano |
| Sin respaldo exitoso en 26 horas | no llegó `respaldo_ok` | una vez al día | la tarea no corrió o le faltan las variables `OTEL_*`. Mientras no haya respaldos programados, esta alerta sigue activa a propósito |
| Un grupo pasó 8 MXN de costo en el mes | `vigilar-costos` (cada hora) encontró grupos arriba de `Operacion:AlertaCostoGrupoMxn` | una vez al día | `/admin` → Costos; el id de cada grupo está en el log "Costo del mes arriba del umbral" |
| /salud no responde (3 fallas seguidas) | las sondas no abren `/salud` en 3 minutos | cada 4 h | Coolify: estado de `api`, `web` y la base |

## Límites del plan gratis

50 GB de logs y 50 GB de trazas al mes, 10 mil series de métricas, 50 mil sesiones de Faro, 100 mil ejecuciones de Synthetic Monitoring, 14 días de retención y 3 usuarios (revisado el 29/10/2026). En **Administration** → **Cost management** se ve el consumo. Para el piloto sobra; lo primero que se acercaría al límite son las series de métricas.
