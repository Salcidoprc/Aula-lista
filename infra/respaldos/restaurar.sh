#!/usr/bin/env bash
# SPEC-09: restaura un respaldo en una base vacía (staging) y compara los conteos con los del día del respaldo.
#
#   restaurar.sh <nombre-del-respaldo sin extensión> <archivo de la llave privada de age>
#
# Antes: crear la base vacía y correr postgres/roles.sql y postgres/respaldos.sql en ella como superusuario.
# PG* apunta a esa base con un superusuario. RESPALDO_DESTINO y RCLONE_CONFIG_* como en respaldar.sh.
set -euo pipefail

nombre="${1:?Uso: restaurar.sh <respaldo> <llave-age>}"
llave="${2:?Uso: restaurar.sh <respaldo> <llave-age>}"
: "${RESPALDO_DESTINO:?Falta RESPALDO_DESTINO}" "${PGDATABASE:?Falta PGDATABASE}"
trabajo="$(mktemp -d)"
trap 'rm -rf "$trabajo"' EXIT

rclone copy "$RESPALDO_DESTINO/$nombre.dump.age" "$trabajo/"
rclone copy "$RESPALDO_DESTINO/$nombre.conteos.age" "$trabajo/"

age --decrypt --identity "$llave" "$trabajo/$nombre.dump.age" | pg_restore --exit-on-error --single-transaction --dbname "$PGDATABASE"
age --decrypt --identity "$llave" "$trabajo/$nombre.conteos.age" > "$trabajo/esperados.txt"
"$(dirname "$0")/conteos.sh" > "$trabajo/restaurados.txt"

if diff -u "$trabajo/esperados.txt" "$trabajo/restaurados.txt"; then
    echo "Conteos iguales en $(wc -l < "$trabajo/esperados.txt") tablas. Falta arrancar la app contra esta base y revisar /salud/lista."
else
    echo "Los conteos no coinciden; la restauración no sirve." >&2
    exit 1
fi
