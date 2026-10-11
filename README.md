# Aula Lista

App web (PWA) para que docentes de primaria y secundaria en México capturen calificaciones, cierren periodos con observaciones redactadas por IA, publiquen boletas y avisen a los tutores sin que estos instalen nada.

- Producto y alcance: [`docs/00-prd.md`](docs/00-prd.md)
- Índice de la documentación (fuente de verdad): [`docs/README.md`](docs/README.md)
- Plan por hitos: [`docs/05-plan-de-desarrollo.md`](docs/05-plan-de-desarrollo.md)
- Instrucciones y comandos para agentes y personas: [`CLAUDE.md`](CLAUDE.md)
- Diseño: [`PRODUCT.md`](PRODUCT.md), [`DESIGN.md`](DESIGN.md) y [`docs/reglas/07-diseno-y-movimiento.md`](docs/reglas/07-diseno-y-movimiento.md)

## Estructura

```
apps/api   Solución .NET 10 (Api, Application, Domain, Infrastructure y pruebas)
apps/web   PWA Angular
infra      docker-compose, Dockerfiles, nginx y scripts de PostgreSQL
docs       PRD, requerimientos, specs, reglas y decisiones
```

## Arranque rápido

```bash
cp infra/.env.example infra/.env   # cambia las claves
docker compose -f infra/docker-compose.yml up --build
```

La web queda en http://localhost:8080 y la API en http://localhost:5080 (`/salud`, `/openapi/v1.json`).
