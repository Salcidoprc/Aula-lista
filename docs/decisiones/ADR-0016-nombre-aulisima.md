# ADR-0016: El producto se llama Aulísima

**Estado:** Aceptada · **Fecha:** 14/11/2026

## Contexto

El nombre "Aula Lista" (PA-04) resultó ocupado: `aulalista.com` está registrado desde febrero de 2025 y la variante `aulista.com` / `aulista.app` desde diciembre de 2025. El docente eligió **Aulísima**: el superlativo de "aula", conserva la idea del nombre anterior y tiene más personalidad. `aulisima.com` estaba libre el 14/11/2026 (consulta RDAP de Verisign). El dominio `.mx` y la marca en el IMPI (clases 9, 41 y 42) quedan por confirmar.

## Decisión

- La marca visible es **Aulísima** (con acento) en la app, el manifiesto de la PWA, correos y documentos.
- En identificadores se usa la forma sin acento: proyectos y namespaces `Aulisima.*`, base de datos `aulisima`, servicios `aulisima-api`, `aulisima-web` y `aulisima-respaldos`, cookies `aulisima_*`, claves de almacenamiento local `aulisima.*` y métricas `aulisima.*`.
- La cadena de conexión pasa a `ConnectionStrings__Aulisima`. Los correos y URLs de ejemplo usan `aulisima.com`.
- El repositorio de GitHub sigue como `aula-lista` hasta que el dueño lo renombre; los enlaces a él no cambian.

## Consecuencias

- Al no haber staging ni producción, no hace falta migrar datos. Un entorno ya creado debe renombrar la base o fijar `Database=aula_lista` en sus cadenas de conexión, y cambiar `ConnectionStrings__AulaLista` por `ConnectionStrings__Aulisima`.
- En los navegadores de prueba se pierden el tema elegido y la cola sin conexión guardados con las claves anteriores, y la sesión se cierra porque cambia el nombre de la cookie.
- En Grafana, la carpeta, los contactos, el monitor de Synthetics (`aulisima-salud`) y las consultas de las alertas usan el nombre nuevo.
