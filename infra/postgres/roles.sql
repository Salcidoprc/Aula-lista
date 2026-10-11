-- Roles de Aula Lista (reglas/03-base-de-datos.md).
-- app_migraciones: dueño del esquema, aplica migraciones.
-- app_runtime: usado por la API; sin BYPASSRLS ni DDL.
-- Las contraseñas llegan como variables de psql (-v) desde el entorno; nunca se escriben aquí.

CREATE ROLE app_migraciones LOGIN PASSWORD :'clave_migraciones';
CREATE ROLE app_runtime LOGIN NOBYPASSRLS PASSWORD :'clave_runtime';

ALTER DATABASE :"base" OWNER TO app_migraciones;
ALTER SCHEMA public OWNER TO app_migraciones;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO app_runtime;

-- Todo lo que cree app_migraciones queda disponible para app_runtime sin DDL.
ALTER DEFAULT PRIVILEGES FOR ROLE app_migraciones IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_runtime;
ALTER DEFAULT PRIVILEGES FOR ROLE app_migraciones IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO app_runtime;
