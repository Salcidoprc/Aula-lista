-- Rol de solo lectura para los respaldos (SPEC-09). Se ejecuta una vez como superusuario:
--   psql -v clave_respaldos=... -f respaldos.sql
-- BYPASSRLS porque pg_dump no respalda tablas filtradas por RLS; no puede escribir ni cambiar el esquema.
CREATE ROLE app_respaldos LOGIN BYPASSRLS PASSWORD :'clave_respaldos';
GRANT pg_read_all_data TO app_respaldos;
