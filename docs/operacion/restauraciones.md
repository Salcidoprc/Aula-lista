# Restauraciones de respaldos

SPEC-09: el primer lunes de cada mes se restaura el respaldo del día anterior en staging y se anota aquí. La prueba pasa
si `restaurar.sh` dice que los conteos son iguales y la API arranca contra esa base con `/salud/lista` en `Healthy`.

## Cómo

1. En el servidor de staging, crear una base vacía y correr en ella `infra/postgres/roles.sql` y
   `infra/postgres/respaldos.sql` como superusuario (si los roles ya existen en el servidor, basta crear la base).
2. Con `PG*` apuntando a esa base con un superusuario, `RESPALDO_DESTINO` y `RCLONE_CONFIG_*`:
   `infra/respaldos/restaurar.sh <respaldo sin extensión> <archivo de la llave privada de age>`.
   La llave privada se trae solo para esto y se borra al terminar.
3. Arrancar la API contra la base restaurada (`ConnectionStrings__AulaLista` con `app_runtime`) y revisar `/salud/lista`.
4. Borrar la base restaurada y anotar el resultado.

## Registro

| Fecha | Respaldo | Tablas | Conteos | App arranca | Quién | Notas |
| --- | --- | --- | --- | --- | --- | --- |
| 2026-10-19 | `aula_lista-2026-10-19T002757Z` | 46 | Iguales | Sí (`Healthy`) | Claude | Prueba en local, no en staging: respaldo tomado con pruebas end-to-end escribiendo en la base, restaurado en una base vacía del mismo servidor. Falta la primera en staging. |
