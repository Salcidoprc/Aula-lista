import { Page, expect, test } from '@playwright/test';
import { NOMBRES, aprobarTodas, cerrarYGenerar, grupoCalificado } from './cierre';
import { sinFallasDeAccesibilidad } from './cuenta';
import { docenteVerificado } from './grupos';

/** Se espera a que se vayan los avisos: axe mediría su contraste a media salida. */
async function accesible(page: Page): Promise<void> {
  await expect(page.locator('app-avisos li')).toHaveCount(0, { timeout: 10_000 });
  await sinFallasDeAccesibilidad(page);
}

const visible = (page: Page, nombre: string | RegExp) =>
  page.getByRole('button', { name: nombre }).locator('visible=true');

/** Del cierre con todo aprobado a la pantalla de boletas, y genera las boletas hasta tenerlas listas. */
async function generarBoletas(page: Page): Promise<void> {
  await page
    .getByRole('link', { name: `Ir a boletas (${NOMBRES.length} aprobadas)` })
    .locator('visible=true')
    .click();
  await expect(page.getByRole('heading', { level: 1, name: 'Boletas' })).toBeVisible();
  await expect(
    page.getByText(`0 de ${NOMBRES.length} publicadas`).locator('visible=true'),
  ).toBeVisible();
  await visible(page, `Generar boletas (${NOMBRES.length})`).click();
  await expect(page.locator('.fila[data-estado="lista"]')).toHaveCount(NOMBRES.length, {
    timeout: 60_000,
  });
}

/** Grupo calificado, cierre generado y aprobado, boletas listas. */
async function boletasListas(page: Page): Promise<string> {
  const grupo = await grupoCalificado(page);
  await cerrarYGenerar(page);
  await aprobarTodas(page);
  await generarBoletas(page);
  return grupo;
}

test.describe('SPEC-05: boletas y envío', () => {
  test('RF-050 a RF-055, RN-048: generar, ver, descargar, exportar, elegir, publicar con resumen y cierre automático', async ({
    page,
    request,
  }) => {
    test.setTimeout(180_000);
    await docenteVerificado(page, request);
    const grupo = await boletasListas(page);
    await accesible(page);

    // RF-051: la vista previa abre el PDF del alumno en otra pestaña.
    const [vista, respuesta] = await Promise.all([
      page.waitForEvent('popup'),
      page.waitForResponse(/\/api\/v1\/boletas\/[^/]+\/pdf$/),
      page.locator('.fila button.abrir').first().click(),
    ]);
    expect(respuesta.status()).toBe(200);
    expect(respuesta.headers()['content-type']).toContain('application/pdf');
    await vista.close();

    // RF-053 y RF-055: el PDF del grupo y la exportación a Excel se descargan con su nombre.
    const [pdf] = await Promise.all([
      page.waitForEvent('download'),
      visible(page, 'Descargar PDF del grupo').click(),
    ]);
    expect(pdf.suggestedFilename()).toMatch(/\.pdf$/);
    const [excel] = await Promise.all([
      page.waitForEvent('download'),
      visible(page, 'Exportar a Excel').click(),
    ]);
    expect(excel.suggestedFilename()).toMatch(/\.xlsx$/);

    // Selección por alumnos: el botón dice cuántas elegidas se publican.
    await page.getByRole('checkbox', { name: `Elegir la boleta de ${NOMBRES[0]}` }).check();
    await expect(visible(page, 'Publicar las elegidas y avisar (1)')).toBeVisible();
    await page.getByRole('checkbox', { name: `Elegir la boleta de ${NOMBRES[0]}` }).uncheck();

    // RF-052: resumen previo con tutores por WhatsApp, por correo y alumnos sin contacto.
    await visible(page, `Publicar y avisar a tutores (${NOMBRES.length})`).click();
    const hoja = page.getByRole('dialog', { name: `Vas a publicar ${NOMBRES.length} boletas` });
    await expect(hoja).toBeVisible();
    await expect(hoja.getByText('Alumnos sin tutor registrado')).toBeVisible();
    await expect(hoja.getByText('No es un documento oficial.')).toBeVisible();
    await accesible(page);
    await hoja.getByRole('button', { name: 'Publicar y avisar', exact: true }).click();
    await expect(page.getByText(`${NOMBRES.length} boletas publicadas`)).toBeVisible();
    await expect(page.locator('.fila[data-estado="publicada"]')).toHaveCount(NOMBRES.length);
    await expect(page.locator('.sello').locator('visible=true')).toBeVisible();

    // RN-048: con todas publicadas la unidad se cierra sola; Calificaciones lleva a las boletas.
    await expect(page.getByText(/Esta unidad está cerrada/)).toBeVisible();
    await expect(visible(page, 'Reabrir la unidad')).toBeVisible();
    await page.goto(grupo);
    await page.getByRole('link', { name: 'Ver boletas' }).locator('visible=true').click();
    await expect(page.getByRole('heading', { level: 1, name: 'Boletas' })).toBeVisible();
  });

  test('RN-048, RN-038, RN-052: cerrar a mano con pendientes, reabrir y corregir una publicada con versión nueva', async ({
    page,
    request,
  }) => {
    test.setTimeout(180_000);
    await docenteVerificado(page, request);
    await boletasListas(page);
    const boletas = page.url();

    // Se publica solo la del primer alumno: las demás quedan pendientes y la unidad sigue en cierre.
    await page.getByRole('checkbox', { name: `Elegir la boleta de ${NOMBRES[0]}` }).check();
    await visible(page, 'Publicar las elegidas y avisar (1)').click();
    const hoja = page.getByRole('dialog', { name: 'Vas a publicar 1 boleta' });
    await hoja.getByRole('button', { name: 'Publicar y avisar', exact: true }).click();
    await expect(page.getByText('1 boleta publicada')).toBeVisible();
    await expect(page.locator('.fila[data-estado="publicada"]')).toHaveCount(1);

    // RN-048: cerrar a mano dice cuántos quedan sin publicar y pide confirmación.
    await visible(page, 'Dar por cerrada la unidad').click();
    const cerrar = page.getByRole('dialog', { name: '¿Dar por cerrada la unidad?' });
    await expect(
      cerrar.getByText(`Quedan ${NOMBRES.length - 1} alumnos sin boleta publicada.`, {
        exact: false,
      }),
    ).toBeVisible();
    await accesible(page);
    await cerrar.getByRole('button', { name: 'Cerrar la unidad' }).click();
    await expect(page.getByText(/Esta unidad está cerrada/)).toBeVisible();

    // RN-038: reabrir pide confirmación, avisa que queda registrado y deja la unidad en cierre.
    await visible(page, 'Reabrir la unidad').click();
    const reabrir = page.getByRole('dialog', { name: '¿Reabrir la unidad?' });
    await expect(reabrir.getByText('queda registrada', { exact: false })).toBeVisible();
    await reabrir.getByRole('button', { name: 'Reabrir', exact: true }).click();
    await expect(page.getByText('Unidad reabierta')).toBeVisible();
    await expect(visible(page, 'Dar por cerrada la unidad')).toBeVisible();

    // Se corrige el texto de la observación ya publicada.
    await page.goto(boletas.replace(/\/boletas$/, '/cierre'));
    await page.locator('.fila').first().click();
    await page.getByRole('button', { name: 'Reabrir', exact: true }).click();
    const texto = page.getByLabel('Texto de la observación');
    await texto.fill('Este periodo leyó en voz alta cada semana y mejoró mucho su comprensión.');
    await page
      .getByRole('button', { name: /^Aprobar( y siguiente)?$/ })
      .locator('visible=true')
      .click();

    // RN-052: la publicada que cambió ofrece la versión corregida; la nueva queda publicada como versión 2.
    await page.goto(boletas);
    const primera = page.locator('.fila').first();
    await expect(primera.getByText('Cambió después de publicar')).toBeVisible();
    await primera.getByRole('button', { name: /Emitir versión corregida/ }).click();
    const corregir = page.getByRole('dialog', { name: 'Emitir versión corregida' });
    await expect(corregir.getByText(/la versión 1 queda reemplazada/)).toBeVisible();
    await corregir.getByRole('button', { name: 'Emitir y publicar' }).click();
    await expect(primera.getByText('Versión 2')).toBeVisible();
    await expect(primera.getByText('Cambió después de publicar')).toHaveCount(0);
    await expect(primera).toHaveAttribute('data-estado', 'publicada');
    await accesible(page);
  });
});
