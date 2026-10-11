# ADR-0012: Tipos de rubro como catálogo administrable con comportamientos en código

**Estado:** Aceptada · **Fecha:** 11/10/2026

## Contexto

El dueño del producto quiere que el administrador agregue tipos de rubro nuevos sin depender de una lista fija. Algunos tipos solo cambian el nombre y los valores sugeridos (Participaciones se calcula igual que Tareas); otros necesitan una forma de calcular y una pantalla de captura propias (Asistencia). Calcular calificaciones con reglas definidas por el usuario en tiempo de ejecución (fórmulas libres) es riesgoso: rompe RN-036 (mismo cálculo en backend y frontend con vectores de prueba) y es difícil de probar.

## Decisión

- Se separa el **tipo de rubro** (dato) del **comportamiento** (código).
- `tipo_rubro` es un catálogo global en la base de datos que el administrador gestiona desde el panel de administración: nombre, descripción, icono, comportamiento, nombres de rubro sugeridos, orden y si está activo.
- Un **comportamiento** es una clave (`actividades`, `asistencia`) con su implementación en código: una estrategia de cálculo en el dominio del backend (`ICalculoRubro`), la misma en el frontend con los vectores de prueba compartidos, y un componente de captura registrado por clave en Angular.
- El administrador puede crear al instante tipos nuevos que usan un comportamiento existente. Un comportamiento nuevo se desarrolla: se documenta en la spec, se implementa (cálculo, captura, vectores de prueba), se despliega, y solo entonces aparece para elegirse en el panel.
- Los tipos se desactivan, no se borran, si algún rubro los usa.

## Alternativas

- Lista fija en el código (enum): cada tipo nuevo, aunque solo cambie el nombre, requiere un despliegue.
- Fórmulas configurables por el administrador: flexible, pero no se puede garantizar el mismo resultado en backend y frontend ni probarlo.

## Consecuencias

- El cálculo de RN-033 delega en la estrategia del comportamiento de cada rubro; agregar un comportamiento no toca los demás.
- La captura de la PWA elige la pantalla por la clave del comportamiento; un comportamiento sin pantalla en la versión instalada muestra "Actualiza la app" en lugar de fallar.
- El catálogo se descarga con los datos sin conexión del grupo.
