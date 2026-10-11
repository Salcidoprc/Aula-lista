# ADR-0001: .NET 10, Angular PWA y PostgreSQL

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

El equipo domina .NET y Angular. El producto necesita funcionar en celulares de gama baja, sin pasar por tiendas de apps y con captura sin conexión.

## Decisión

- Backend: .NET 10 (versión LTS) con ASP.NET Core Minimal APIs, EF Core y ASP.NET Core Identity.
- Frontend: Angular como PWA (service worker e IndexedDB), Angular Material.
- Base de datos: PostgreSQL 18 con Row Level Security.
- PDF: QuestPDF (licencia comunitaria gratuita mientras los ingresos anuales sean menores a 1 millón de USD; revisar al crecer).

## Alternativas

- Next.js con Supabase: más rápido para una persona, pero fuera del dominio del equipo.
- App nativa: obliga a tiendas e instalación, justo lo que el producto evita.

## Consecuencias

- Vercel no aplica para la API .NET; ver ADR-0002.
- La lógica de cálculo debe existir en C# y en TypeScript; se controla con vectores de prueba compartidos (RN-036).
