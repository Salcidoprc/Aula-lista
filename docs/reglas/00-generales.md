# Reglas generales

Estas reglas aplican a todas las personas y agentes de IA que trabajen en el proyecto.

## Fuente de verdad

1. La carpeta `docs/` es la fuente de verdad del producto. Si el código y los documentos no coinciden, los documentos mandan hasta que se actualicen.
2. Jerarquía cuando dos documentos chocan: `01-requerimientos.md` > `specs/` > `00-prd.md` > `02-arquitectura.md`. Las reglas de esta carpeta aplican siempre. El conflicto se corrige en el mismo PR que lo descubre.
3. Ningún comportamiento se programa si no está en un requerimiento o una spec. Si falta, primero se agrega al documento (en el mismo PR o en uno previo) y después se programa.
4. Los IDs (`RF-`, `RNF-`, `RN-`, `SPEC-`, `ADR-`) son permanentes. No se renumeran ni se reutilizan; lo que se elimina se marca **Retirado**.
5. Las decisiones técnicas que cambian el stack, la arquitectura o un proveedor se registran como ADR en `docs/decisiones/` antes de implementarse.
6. Las preguntas sin resolver van a `docs/04-preguntas-abiertas.md`; al resolverse, se actualiza el documento afectado y se marca la pregunta como resuelta con la fecha.

## Idioma y nombres

- Documentos, mensajes de la interfaz, mensajes de error para el usuario y nombres de dominio en español de México.
- Código: identificadores del dominio en español (`Alumno`, `Periodo`, `CalcularPromedioRubro`), términos técnicos en inglés cuando son estándar (`Repository`, `Handler`, `Controller`, `Dto`). Sin acentos ni ñ en identificadores (`Calificacion`, `Ano` se evita: usar `Ciclo`).
- Commits y PRs en español.
- Glosario obligatorio en `docs/03-glosario.md`; si un término nuevo aparece, se agrega ahí.

## Definición de terminado

Una tarea está terminada cuando:

1. Cumple los criterios de aceptación de su spec, citados por ID en el PR.
2. Tiene pruebas automáticas que cubren la regla de negocio y los casos borde de la spec.
3. Pasa CI completo: compilación, lint, pruebas, pruebas de aislamiento, presupuesto de tamaño y accesibilidad.
4. No introduce datos personales en logs ni envíos a la IA.
5. Actualiza documentación si cambió un comportamiento, un endpoint o una decisión.
6. Si toca interfaz: pasó el flujo de revisión de `07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño) y el PR lo anota.
7. Fue revisada por otra persona o, si se trabaja solo, con la revisión automática de código antes de fusionar.

## Alcance

- Lo que está en "Fuera de alcance" de una spec no se programa, aunque parezca rápido.
- Una idea nueva se escribe como pregunta abierta o como propuesta de cambio a la spec; no se cuela en un PR.
