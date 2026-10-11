# Aula Lista: instrucciones para agentes de IA

App web (PWA) para que docentes de México capturen calificaciones, cierren periodos con observaciones redactadas por IA, publiquen boletas y avisen a tutores. Backend .NET 10, frontend Angular, PostgreSQL.

## Antes de escribir código

1. `docs/` es la fuente de verdad. Lee `docs/README.md` y la spec del módulo que vas a tocar.
2. Sigue las reglas de `docs/reglas/`; son obligatorias.
3. No implementes nada que no esté en un requerimiento o spec. Si falta, propón el cambio al documento primero.
4. Lo que dice "Fuera de alcance" no se programa.

## Diseño y movimiento

Impeccable es la base del diseño de toda pantalla. Se mejora con todos los skills de animación y diseño de la tabla de `docs/reglas/07-diseno-y-movimiento.md`: Emil Kowalski, Design Motion Principles y los de diseño de Claude. Esa regla dice cómo instalarlos, en qué orden usarlos y cuál gana si se contradicen. Lo mínimo:
- `/impeccable shape` antes de construir.
- `animate` para cada transición del catálogo.
- Antes del PR: `/impeccable audit`, `/impeccable critique`, `review-animations`, auditoría de `design-motion-principles`, `break-ui`, `mobile-native` y `accessibility-review`.

Sin 3D. Nunca animes acciones repetidas como capturar calificaciones.

## Reglas que no se rompen

- Datos de menores: nunca envíes nombres, teléfonos ni correos a la IA ni a los logs. Usa el seudónimo (RN-019).
- Aislamiento: todo dato se filtra por dueño en la aplicación y con RLS en PostgreSQL. Un recurso ajeno responde 404.
- Nada generado por IA llega a un tutor sin aprobación del docente (RN-042).
- Calificaciones y dinero con `decimal` / `numeric`, nunca `double`.
- Modelo de IA, precios y límites vienen de configuración, nunca escritos en el código.
- Secretos solo en variables de entorno.

## Comandos

Requisitos: .NET SDK 10, Node 24 y Docker.

| Qué | Comando |
| --- | --- |
| Levantar todo en local | `cp infra/.env.example infra/.env` y `docker compose -f infra/docker-compose.yml up --build` (web en :8080, API en :5080) |
| Solo la base | `docker compose -f infra/docker-compose.yml up -d db migraciones` |
| Compilar y probar la API | `cd apps/api && dotnet build && dotnet test` (las pruebas de integración usan Docker) |
| Formato de la API | `cd apps/api && dotnet format` |
| Nueva migración | `cd apps/api && dotnet tool restore && dotnet ef migrations add <NombreEnEspañol> -p src/AulaLista.Infrastructure -s src/AulaLista.Api -o Persistencia/Migraciones` |
| Aplicar migraciones | `dotnet run --project src/AulaLista.Api -- --migrar` con `ConnectionStrings__Migraciones` |
| API en local | `cd apps/api && dotnet run --project src/AulaLista.Api` con `ConnectionStrings__AulaLista` y `Jwt__ClaveFirma` |
| Web en local | `cd apps/web && npm ci && npm start` (proxy a la API en :5080) |
| Regenerar el cliente de la API | `cd apps/api && dotnet build` y luego `cd apps/web && npm run api:generar` |
| Lint, unitarias y build | `cd apps/web && npm run lint && npm test && npm run build && npm run bundle:verificar` |
| End-to-end | `cd apps/web && npx playwright test` (con la app en :8080, o `BASE_URL=...`) |
| Detector de Impeccable | `.claude/skills/impeccable/scripts/impeccable detect --json apps/web/src` |

Los skills de diseño y animación están instalados en `.claude/skills/` (versiones en `skills-lock.json`). `PRODUCT.md` y `DESIGN.md` están en la raíz; los tokens en `apps/web/src/styles/tokens.css`.

## Al terminar una tarea

- Pruebas que cubran los criterios de aceptación citados por ID.
- CI en verde.
- Documentación actualizada si cambió un comportamiento, un endpoint o una decisión.
- PR en español citando SPEC, RF y RN.
