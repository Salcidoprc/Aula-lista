# Reglas del backend (.NET 10)

## Estructura

- Solución en `apps/api` con 4 proyectos: `Api`, `Application`, `Domain`, `Infrastructure` (ver `02-arquitectura.md`).
- Dependencias permitidas: `Api` → `Application` + `Infrastructure`; `Infrastructure` → `Application`; `Application` → `Domain`; `Domain` no depende de nada. Una prueba de arquitectura (NetArchTest o equivalente) lo verifica en CI.
- Dentro de cada proyecto, una carpeta por módulo: `Cuentas`, `Grupos`, `Evaluacion`, `Cierre`, `Boletas`, `Tutores`, `Avisos`, `Planes`, `Operacion`.
- Un módulo no usa entidades ni tablas de otro módulo directamente; llama a sus casos de uso o a una interfaz pública del módulo.

## Dominio

- Las reglas de negocio (`RN-`) viven en `Domain`: entidades con métodos que protegen sus invariantes (por ejemplo, `Periodo.IniciarCierre()` valida RN-040). No hay setters públicos para cambiar estado.
- Cada regla `RN-` implementada lleva un comentario con su ID solo donde se aplica: `// RN-031`.
- Cálculos con `decimal`. Prohibido `double` o `float` para calificaciones o dinero.
- Fechas en UTC con `DateTimeOffset`; la conversión a la zona del docente ocurre en el borde (API o frontend). Usar `TimeProvider` inyectado, nunca `DateTime.Now`.
- IDs con UUID v7 (`Guid.CreateVersion7()`).

## Aplicación

- Un caso de uso por clase (comando o consulta) con su validador (FluentValidation).
- Los errores de negocio esperados se devuelven como resultado (`Result<T>` con código de error), no como excepciones. Las excepciones son para lo inesperado.
- Códigos de error en `snake_case` español, definidos en un solo lugar y listados en el OpenAPI (`pesos_no_suman_100`, `limite_alumnos_grupo`, `requiere_pro`).
- Las integraciones externas se usan solo a través de interfaces de `Application` (`IRedactorObservaciones`, `IMensajeriaWhatsApp`, etc.).

## API

- Minimal APIs agrupadas por módulo, con `MapGroup("/api/v1/...")`.
- Toda ruta requiere autenticación salvo las listadas explícitamente como públicas (registro, inicio de sesión, canje de enlace de tutor, webhooks con firma).
- Respuestas de error con Problem Details y propiedad `codigo`.
- Validación de entrada en el borde; nunca confiar en el cliente (incluido el cálculo de promedios: el servidor recalcula).
- Endpoints de mutación que vienen de la cola sin conexión soportan `Idempotency-Key`.
- OpenAPI generado y versionado; el cliente Angular se genera de él.

## Persistencia

- EF Core con Npgsql. Migraciones con nombre descriptivo en español y revisadas en el PR; nunca se edita una migración ya aplicada en staging o producción.
- Filtros globales de consulta por dueño además de RLS (ver `reglas/03-base-de-datos.md`).
- Consultas de lectura con `AsNoTracking()` y proyección a DTOs.
- Sin consultas N+1: las pruebas de integración de listas verifican el número de consultas.

## Jobs

- Hangfire con almacenamiento en PostgreSQL. Cada job es idempotente: correrlo dos veces no duplica efectos.
- Los jobs no reciben entidades, solo IDs.

## Configuración y secretos

- Configuración con `IOptions<T>` validada al arrancar.
- Secretos solo en variables de entorno de Coolify o en el gestor de secretos; nunca en `appsettings.json` ni en el repositorio.
- Precios, límites de WhatsApp, modelo de IA y tarifas viven en configuración.

## Pruebas

- xUnit. Unitarias para dominio y aplicación; integración con Testcontainers (PostgreSQL real, con RLS activado).
- Cada criterio de aceptación de una spec tiene al menos una prueba con el ID en el nombre: `SPEC03_PesosQueNoSuman100_RechazaInicioDeCierre`.
- Pruebas de aislamiento: por cada endpoint, una prueba que intenta acceder a datos de otro docente u otro tutor y espera 404.
- Los clientes externos (Claude, Meta, Stripe, correo) se reemplazan por dobles en pruebas; nunca se llaman servicios reales en CI.

## Estilo

- `nullable` habilitado, advertencias como errores, analizadores de .NET activos.
- `dotnet format` en CI.
- Logs con Serilog y plantillas estructuradas; prohibido interpolar datos personales en logs.
