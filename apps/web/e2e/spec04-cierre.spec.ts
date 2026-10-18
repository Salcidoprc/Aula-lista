import { Page, expect, test } from '@playwright/test';
import { sinFallasDeAccesibilidad } from './cuenta';
import { crearCicloYGrupo, docenteVerificado } from './grupos';

const NOMBRES = [
  'Cruz Ortiz Leonardo',
  'Flores Rojas Regina',
  'García López Ana Sofía',
  'Morales Reyes Renata',
];

const teclado = (page: Page) => page.getByRole('group', { name: 'Teclado rápido' });

/**
 * Grupo "4° B" con 4 alumnos, un rubro de 100 % y una actividad calificada: listo para iniciar el cierre
 * (RN-040). Termina en Calificaciones y devuelve la ruta del grupo.
 */
async function grupoCalificado(page: Page): Promise<string> {
  await crearCicloYGrupo(page);
  const grupo = new URL(page.url()).pathname.replace(/\/alumnos$/, '');
  await page.goto(grupo + '/importar?origen=texto');
  await page.getByLabel('Lista de alumnos').fill(NOMBRES.join('\n'));
  await page.getByRole('button', { name: 'Ver cómo queda' }).click();
  await page.getByRole('button', { name: `Importar ${NOMBRES.length} alumnos` }).click();
  await expect(page.getByText(`Se importaron ${NOMBRES.length} alumnos.`)).toBeVisible();
  await page.goto(grupo);
  await page.getByRole('link', { name: 'Crear rubros' }).click();
  await page.getByRole('button', { name: 'Agregar rubro' }).first().click();
  const bloque = page.locator('section.bloque').first();
  await bloque.getByLabel('Tipo').selectOption({ label: 'Actividades' });
  await bloque.getByLabel('Peso', { exact: true }).fill('100');
  await bloque.getByRole('radio', { name: 'Con número' }).check();
  await page.getByRole('button', { name: 'Guardar rubros' }).click();
  await expect(page).toHaveURL(new RegExp(`${grupo}\\?unidad=`));
  await page.getByRole('button', { name: 'Nueva actividad' }).first().click();
  await page.getByLabel('Rubro').selectOption({ label: 'Actividades' });
  await page.getByRole('button', { name: 'Crear y capturar' }).click();
  await expect(teclado(page)).toBeVisible();
  for (const n of ['9', '8', '10', '7']) {
    await teclado(page).getByRole('button', { name: n, exact: true }).click();
  }
  await expect(
    page.locator('app-indicador-sincronizacion').getByText('Guardado', { exact: true }),
  ).toBeVisible();
  await page.goto(grupo);
  await expect(page.getByRole('heading', { level: 1, name: 'Calificaciones' })).toBeVisible();
  return grupo;
}

/** Inicia el cierre desde Calificaciones y genera todas las observaciones; termina en la lista del cierre. */
async function cerrarYGenerar(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Cerrar la unidad' }).locator('visible=true').click();
  await expect(page.getByRole('heading', { name: 'Antes de iniciar' })).toBeVisible();
  await page.getByRole('button', { name: 'Iniciar cierre' }).click();
  await page
    .getByRole('button', { name: `Generar observaciones (${NOMBRES.length})` })
    .locator('visible=true')
    .click();
  await expect(page.locator('.fila[data-estado="borrador"]')).toHaveCount(NOMBRES.length, {
    timeout: 60_000,
  });
}

test.describe('SPEC-04: cierre de periodo y observaciones con IA', () => {
  test('RF-040 a RF-042, RN-042, RN-043: iniciar, generar con avance, regenerar, aprobar y siguiente, escribir a mano', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    await grupoCalificado(page);
    await cerrarYGenerar(page);
    await expect(
      page.getByText(`0 de ${NOMBRES.length} aprobadas`).locator('visible=true'),
    ).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    await page.locator('.fila').first().click();
    await expect(page.getByText(`1 de ${NOMBRES.length}`, { exact: true })).toBeVisible();
    const texto = page.getByLabel('Texto de la observación');
    await expect(texto).not.toHaveValue('');
    await expect(page.getByText(/palabras · Normal: 60 a 90/)).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    // RN-043: otra versión con una sugerencia rápida; quedan 4.
    await page.getByRole('button', { name: 'Más breve' }).click();
    await page.getByRole('button', { name: 'Regenerar' }).click();
    await expect(page.getByText('Te quedan 4')).toBeVisible({ timeout: 30_000 });

    // RN-042: el docente aprueba; pasa al siguiente sin aprobar.
    await page.getByRole('button', { name: 'Aprobar y siguiente' }).locator('visible=true').click();
    await expect(page.getByText(`2 de ${NOMBRES.length}`, { exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Escribir a mano' }).click();
    await expect(texto).toHaveValue('');
    await expect(texto).toBeFocused();
    await texto.fill(
      'Este periodo trabajó con constancia y ayudó a su equipo a terminar a tiempo.',
    );
    await texto.press('Control+Enter');
    await expect(page.getByText(`3 de ${NOMBRES.length}`, { exact: true })).toBeVisible();

    await page.getByRole('link', { name: 'Lista del cierre' }).click();
    await expect(
      page.getByText(`2 de ${NOMBRES.length} aprobadas`).locator('visible=true'),
    ).toBeVisible();
    await expect(page.locator('.fila[data-estado="aprobada"]')).toHaveCount(2);
  });

  test('RN-044: si cambia una calificación de una observación aprobada, vuelve a borrador con "Datos cambiaron"', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    const grupo = await grupoCalificado(page);
    await cerrarYGenerar(page);
    const cierre = page.url();

    await page.locator('.fila').first().click();
    await page.getByRole('button', { name: 'Aprobar y siguiente' }).locator('visible=true').click();
    await expect(page.getByText(`2 de ${NOMBRES.length}`, { exact: true })).toBeVisible();

    // En cierre se sigue capturando (RN-040): se corrige la calificación del primer alumno.
    await page.goto(grupo);
    await page
      .getByRole('link', { name: /Actividad/ })
      .first()
      .click();
    await expect(teclado(page)).toBeVisible();
    await page.locator('li.fila').first().getByRole('button').first().click();
    await teclado(page).getByRole('button', { name: '6', exact: true }).click();
    await expect(
      page.locator('app-indicador-sincronizacion').getByText('Guardado', { exact: true }),
    ).toBeVisible();

    await page.goto(cierre);
    const primera = page.locator('.fila').first();
    await expect(primera).toHaveAttribute('data-estado', 'borrador');
    await expect(primera.getByText('Datos cambiaron')).toBeVisible();
    await primera.click();
    await expect(page.getByText(/Datos cambiaron: sus calificaciones o notas/)).toBeVisible();
  });

  test('RF-043: el estilo (longitud, persona y ejemplos) se guarda y se conserva al volver', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    await page.goto('/ajustes');
    await page.getByRole('link', { name: 'Estilo de las observaciones' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Estilo de las observaciones' }),
    ).toBeVisible();
    await page.getByRole('radio', { name: /^Breve/ }).check();
    await page.getByRole('radio', { name: /^Segunda persona/ }).check();
    await page.getByRole('button', { name: 'Agregar ejemplo' }).click();
    await page
      .getByLabel('Ejemplo 1')
      .fill('Este trimestre mostraste constancia. Para el siguiente, repasa en casa las tablas.');
    await sinFallasDeAccesibilidad(page);
    await page.getByRole('button', { name: 'Guardar estilo' }).click();
    await expect(page.getByText('Estilo guardado.')).toBeVisible();

    await page.reload();
    await expect(page.getByRole('radio', { name: /^Breve/ })).toBeChecked();
    await expect(page.getByRole('radio', { name: /^Segunda persona/ })).toBeChecked();
    await expect(page.getByLabel('Ejemplo 1')).toHaveValue(/mostraste constancia/);
  });
});
