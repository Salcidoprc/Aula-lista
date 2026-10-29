#!/usr/bin/env bash
# ADR-0015: aplica en Grafana Cloud las alertas de SPEC-09 (alertas.json), a quién avisar (correo y Telegram) y cada
# cuánto repetir. Se puede correr las veces que sea: crea lo que falta y reemplaza lo que ya existe.
#
# Variables:
#   GRAFANA_URL         la del stack, por ejemplo https://aulalista.grafana.net
#   GRAFANA_TOKEN       token de una cuenta de servicio con rol Admin (la API de aprovisionamiento lo pide; nunca en el repositorio)
#   ALERTA_CORREOS      uno o varios correos separados por ";" que reciben las alertas
#   TELEGRAM_BOT_TOKEN  opcional: token del bot de Telegram (BotFather)
#   TELEGRAM_CHAT_ID    opcional: chat al que escribe el bot
#   DS_PROM, DS_LOGS    opcionales: uid de los datasources (grafanacloud-prom y grafanacloud-logs si no se indican)
set -euo pipefail

: "${GRAFANA_URL:?Falta GRAFANA_URL}" "${GRAFANA_TOKEN:?Falta GRAFANA_TOKEN}" "${ALERTA_CORREOS:?Falta ALERTA_CORREOS}"
url="${GRAFANA_URL%/}"
ds_prom="${DS_PROM:-grafanacloud-prom}"
ds_logs="${DS_LOGS:-grafanacloud-logs}"
carpeta="aula-lista"
receptor="Aula Lista"
aqui="$(dirname "$0")"

# Llama a la API y deja el cuerpo en $respuesta y el código en $codigo. X-Disable-Provenance deja editar en la interfaz
# lo que aplica este script.
api() {
    local metodo="$1" ruta="$2" cuerpo="${3:-}" salida
    salida="$(curl --silent --show-error --max-time 30 --request "$metodo" \
        --header "Authorization: Bearer $GRAFANA_TOKEN" --header 'Content-Type: application/json' \
        --header 'X-Disable-Provenance: true' ${cuerpo:+--data "$cuerpo"} \
        --write-out $'\n%{http_code}' "$url$ruta")"
    codigo="${salida##*$'\n'}"
    respuesta="${salida%$'\n'*}"
}

exigir() {
    if [[ "$codigo" != 2* ]]; then
        echo "Falló $1 (HTTP $codigo): $respuesta" >&2
        exit 1
    fi
}

for ds in "$ds_prom" "$ds_logs"; do
    api GET "/api/datasources/uid/$ds"
    exigir "buscar el datasource $ds (en Grafana Cloud se llama así por defecto; si cambió, usa DS_PROM o DS_LOGS)"
done

api GET "/api/folders/$carpeta"
if [[ "$codigo" == 404 ]]; then
    api POST /api/folders "$(jq -n --arg uid "$carpeta" '{uid: $uid, title: "Aula Lista"}')"
    exigir "crear la carpeta"
fi

# Un receptor con dos integraciones: las dos llevan el mismo nombre.
contacto() {
    local uid="$1" cuerpo="$2"
    api PUT "/api/v1/provisioning/contact-points/$uid" "$cuerpo"
    if [[ "$codigo" == 404 || "$codigo" == 400 && "$respuesta" == *"not found"* ]]; then
        api POST /api/v1/provisioning/contact-points "$cuerpo"
    fi
    exigir "guardar el contacto $uid"
}

contacto aula-lista-correo "$(jq -n --arg uid aula-lista-correo --arg nombre "$receptor" --arg correos "$ALERTA_CORREOS" \
    '{uid: $uid, name: $nombre, type: "email", settings: {addresses: $correos, singleEmail: true}, disableResolveMessage: false}')"
if [[ -n "${TELEGRAM_BOT_TOKEN:-}" && -n "${TELEGRAM_CHAT_ID:-}" ]]; then
    contacto aula-lista-telegram "$(jq -n --arg uid aula-lista-telegram --arg nombre "$receptor" \
        --arg token "$TELEGRAM_BOT_TOKEN" --arg chat "$TELEGRAM_CHAT_ID" \
        '{uid: $uid, name: $nombre, type: "telegram", settings: {bottoken: $token, chatid: $chat}, disableResolveMessage: false}')"
else
    echo "Sin TELEGRAM_BOT_TOKEN y TELEGRAM_CHAT_ID: las alertas solo llegan por correo."
fi

# Lo urgente se repite cada 4 horas mientras siga; costo y respaldo ausente, una vez al día.
api PUT /api/v1/provisioning/policies "$(jq -n --arg r "$receptor" '{
    receiver: $r, group_by: ["alertname"], group_wait: "30s", group_interval: "5m", repeat_interval: "4h",
    routes: [{receiver: $r, object_matchers: [["frecuencia", "=", "diaria"]], repeat_interval: "24h"}]
}')"
exigir "guardar la política de avisos"

reglas="$(jq --arg prom "$ds_prom" --arg logs "$ds_logs" --arg carpeta "$carpeta" '
    .folderUid = $carpeta
    | .rules |= map(. + {folderUID: $carpeta, ruleGroup: "operacion"}
        | .data |= map(if .datasourceUid == "${DS_PROM}" then .datasourceUid = $prom
                       elif .datasourceUid == "${DS_LOGS}" then .datasourceUid = $logs else . end))
' "$aqui/alertas.json")"
api PUT "/api/v1/provisioning/folder/$carpeta/rule-groups/operacion" "$reglas"
exigir "guardar las alertas"

echo "Listo: $(jq '.rules | length' <<< "$reglas") alertas en la carpeta Aula Lista, avisos a \"$receptor\"."
