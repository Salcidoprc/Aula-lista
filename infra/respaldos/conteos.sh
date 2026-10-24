#!/usr/bin/env bash
# Conteo de filas por tabla (conteos.sql) con la conexión de PG*. Lo usa restaurar.sh para comparar.
set -euo pipefail

psql --no-psqlrc --quiet --tuples-only --no-align --field-separator ' ' --set ON_ERROR_STOP=1 \
    --file "$(dirname "$0")/conteos.sql"
