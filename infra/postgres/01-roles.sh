#!/bin/sh
# Se ejecuta una sola vez al crear el volumen de PostgreSQL (docker-entrypoint-initdb.d).
set -eu
psql -v ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v base="$POSTGRES_DB" \
  -v clave_migraciones="$CLAVE_APP_MIGRACIONES" \
  -v clave_runtime="$CLAVE_APP_RUNTIME" \
  -f /docker-entrypoint-initdb.d/roles.sql.inc
