#!/bin/sh
# ADR-0015: con FARO_URL (la del colector de Grafana Faro), nginx reenvía /faro y la web se entera por
# /observabilidad.json. Sin FARO_URL, /observabilidad.json responde 404 y la web no carga Faro.
set -eu
mkdir -p /etc/nginx/faro
if [ -z "${FARO_URL:-}" ]; then
  echo 'location = /observabilidad.json { return 404; }' > /etc/nginx/faro/faro.conf
  exit 0
fi
cat > /etc/nginx/faro/faro.conf <<EOF
location = /observabilidad.json {
  default_type application/json;
  add_header Cache-Control "no-store" always;
  return 200 '{"faro":true,"entorno":"${FARO_ENTORNO:-produccion}"}';
}

# Solo POST, sin cookies ni la IP de quien navega: Grafana recibe el envío ya limpio desde el servidor (RN-019).
location = /faro {
  limit_except POST { deny all; }
  client_max_body_size 256k;
  proxy_pass ${FARO_URL};
  proxy_ssl_server_name on;
  proxy_set_header Cookie "";
  proxy_set_header Authorization "";
  proxy_set_header X-Forwarded-For "";
  proxy_set_header X-Real-IP "";
}
EOF
