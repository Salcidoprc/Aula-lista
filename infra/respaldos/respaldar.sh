#!/usr/bin/env bash
# SPEC-09: respaldo diario de PostgreSQL, cifrado con age y subido fuera del servidor con rclone. Junto al
# volcado va el conteo de filas por tabla, para comparar al restaurar. Retiene 30 días.
#
# Variables:
#   PGHOST, PGPORT, PGDATABASE, PGUSER, PGPASSWORD  conexión con el rol app_respaldos (postgres/respaldos.sql)
#   AGE_RECIPIENT                                   llave pública de age; la privada nunca está en el servidor
#   RESPALDO_DESTINO                                remoto de rclone, por ejemplo "objetos:aula-lista-respaldos"
#   RCLONE_CONFIG_*                                 configuración del remoto, solo en variables de entorno
#   RESPALDO_DIAS                                   días que se conservan (30 si no se indica)
#   OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS
#                                                   opcionales: las mismas de la API; al terminar se manda respaldo_ok o
#                                                   respaldo_fallido a Grafana Cloud para su alerta (ADR-0015)
set -euo pipefail

# Un log OTLP en JSON del servicio aula-lista-respaldos. Si no se puede mandar, el respaldo no falla por eso.
avisar() {
    [[ -n "${OTEL_EXPORTER_OTLP_ENDPOINT:-}" ]] || return 0
    local evento="$1" severidad="$2" numero="$3" encabezados=() par
    IFS=',' read -ra pares <<< "${OTEL_EXPORTER_OTLP_HEADERS:-}"
    for par in "${pares[@]}"; do
        [[ -n "$par" ]] && encabezados+=(--header "${par%%=*}: ${par#*=}")
    done
    encabezados=("${encabezados[@]//%20/ }")
    curl --silent --show-error --fail --max-time 20 "${encabezados[@]}" --header 'Content-Type: application/json' \
        --data "{\"resourceLogs\":[{\"resource\":{\"attributes\":[{\"key\":\"service.name\",\"value\":{\"stringValue\":\"aula-lista-respaldos\"}}]},\"scopeLogs\":[{\"logRecords\":[{\"timeUnixNano\":\"$(date +%s%N)\",\"severityText\":\"$severidad\",\"severityNumber\":$numero,\"body\":{\"stringValue\":\"$evento\"},\"attributes\":[{\"key\":\"base\",\"value\":{\"stringValue\":\"${PGDATABASE:-}\"}}]}]}]}]}" \
        "${OTEL_EXPORTER_OTLP_ENDPOINT%/}/v1/logs" > /dev/null || echo "No se pudo avisar $evento a OTLP" >&2
}

terminar() {
    local estado=$?
    rm -rf "${trabajo:-}"
    if [[ $estado -eq 0 ]]; then avisar respaldo_ok INFO 9; else avisar respaldo_fallido ERROR 17; fi
}
trap terminar EXIT

: "${PGDATABASE:?Falta PGDATABASE}" "${AGE_RECIPIENT:?Falta AGE_RECIPIENT}" "${RESPALDO_DESTINO:?Falta RESPALDO_DESTINO}"
dias="${RESPALDO_DIAS:-30}"
fecha="$(date -u +%Y-%m-%dT%H%M%SZ)"
trabajo="$(mktemp -d)"

# Volcado y conteos salen de la misma instantánea: una transacción REPEATABLE READ la exporta, cuenta las filas y
# pg_dump la usa, así lo que se escribe mientras tanto no hace que los conteos dejen de coincidir al restaurar.
coproc PSQL { psql --no-psqlrc --quiet --tuples-only --no-align --field-separator ' ' --set ON_ERROR_STOP=1; }
echo "BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY; SELECT pg_export_snapshot();" >&"${PSQL[1]}"
read -r -u "${PSQL[0]}" instantanea
echo "\\i $(dirname "$0")/conteos.sql" >&"${PSQL[1]}"
echo "SELECT 'fin-de-conteos';" >&"${PSQL[1]}"
: > "$trabajo/conteos.txt"
while read -r -u "${PSQL[0]}" renglon && [[ "$renglon" != "fin-de-conteos" ]]; do
    echo "$renglon" >> "$trabajo/conteos.txt"
done
age --recipient "$AGE_RECIPIENT" --output "$trabajo/$PGDATABASE-$fecha.conteos.age" "$trabajo/conteos.txt"
rm "$trabajo/conteos.txt"

# Sin pasar por disco sin cifrar: el volcado sale de pg_dump directo a age. Conserva dueños y permisos, que son
# parte del aislamiento (RLS, tablas de solo inserción).
pg_dump --format=custom --snapshot="$instantanea" | age --recipient "$AGE_RECIPIENT" --output "$trabajo/$PGDATABASE-$fecha.dump.age"
echo "COMMIT;" >&"${PSQL[1]}"
exec {PSQL[1]}>&-
wait "$PSQL_PID"

rclone copy "$trabajo" "$RESPALDO_DESTINO/" --no-traverse
rclone delete "$RESPALDO_DESTINO/" --min-age "${dias}d"
echo "Respaldo $PGDATABASE-$fecha subido a $RESPALDO_DESTINO"
