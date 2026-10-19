-- Filas por tabla del esquema public, una por renglón ("tabla filas"), ordenadas (SPEC-09). El rol debe saltarse
-- RLS (app_respaldos o un superusuario) o los conteos salen filtrados.
SELECT table_name,
       (xpath('/row/n/text()', query_to_xml(format('SELECT count(*) AS n FROM public.%I', table_name), false, true, '')))[1]::text
FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
ORDER BY table_name;
