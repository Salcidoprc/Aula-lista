import { expect, test } from '@playwright/test';
import { sinFallasDeAccesibilidad } from './cuenta';
import { NOMBRES, cerrarYGenerar, grupoCalificado, teclado } from './cierre';
import { docenteVerificado } from './grupos';

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

    // "Guardado" puede seguir visible del guardado anterior mientras la cola sube el cambio: se recarga hasta verlo.
    const primera = page.locator('.fila').first();
    await expect(async () => {
      await page.goto(cierre);
      await expect(primera).toHaveAttribute('data-estado', 'borrador', { timeout: 2_000 });
    }).toPass({ timeout: 20_000 });
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
