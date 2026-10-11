# ADR-0007: Oracle solo para pruebas; producción en Azure o similar

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

El servidor de Oracle es una instancia de prueba gratuita. Sirve para validar el producto con docentes, pero no se quiere depender de él para la operación de pago (PA-10).

## Decisión

- Staging y pruebas con docentes corren en el servidor de prueba de Oracle con Coolify (ADR-0002).
- Al terminar las pruebas, la producción se migra a Azure o un proveedor similar. El destino exacto se decide en un ADR nuevo con costos reales.
- Las imágenes se construyen multiarquitectura (`linux/amd64` y `linux/arm64`): la instancia gratuita de Oracle suele ser ARM y Azure suele ser x86.
- Nada del código depende de servicios exclusivos de Oracle. Configuración por variables de entorno, archivos en almacenamiento de objetos con interfaz `IAlmacenArchivos` compatible con S3, respaldos con `pg_dump`.

## Alternativas

- Quedarse en Oracle también en producción: costo cero, pero es una instancia de prueba sin garantías.
- Ir directo a Azure: más caro antes de validar.

## Consecuencias

- La migración se ensaya restaurando un respaldo en el destino antes del corte, con ventana anunciada a los docentes.
- Los dominios apuntan al proveedor por DNS; cambiar de servidor no cambia los enlaces de los tutores.
