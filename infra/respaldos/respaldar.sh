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
set -euo pipefail

: "${PGDATABASE:?Falta PGDATABASE}" "${AGE_RECIPIENT:?Falta AGE_RECIPIENT}" "${RESPALDO_DESTINO:?Falta RESPALDO_DESTINO}"
dias="${RESPALDO_DIAS:-30}"
fecha="$(date -u +%Y-%m-%dT%H%M%SZ)"
trabajo="$(mktemp -d)"
trap 'rm -rf "$trabajo"' EXIT

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
