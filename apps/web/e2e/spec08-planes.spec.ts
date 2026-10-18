import { expect, test } from '@playwright/test';
import { agregarTutor, grupoConAlumnos } from './avisos';
import { grupoCalificado } from './cierre';
import { sinFallasDeAccesibilidad } from './cuenta';
import { crearCicloYGrupo, docenteVerificado } from './grupos';

test.describe('SPEC-08: planes y pagos', () => {
  test('RN-080, RN-081: un grupo Gratis pide Pro en el cierre y el docente elige ahí su prueba', async ({
    page,
    request,
  }) => {
    test.setTimeout(120_000);
    await docenteVerificado(page, request);
    await grupoCalificado(page);
    await page.getByRole('link', { name: 'Cerrar la unidad' }).locator('visible=true').click();

    // RN-080: en Gratis el cierre no se inicia; el candado ofrece la prueba.
    await expect(
      page.getByRole('heading', { name: 'El cierre con observaciones es de Pro' }),
    ).toBeVisible();
    await expect(page.getByRole('button', { name: 'Iniciar cierre' })).toHaveCount(0);
    await expect(page.getByText(/dura 14 días o hasta que publiques sus boletas/)).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    // RN-081: la prueba queda en este grupo y dice cuántos días le quedan.
    await page.getByRole('button', { name: 'Usar mi prueba Pro en este grupo' }).click();
    await expect(page.getByText('Prueba Pro · quedan 14 días')).toBeVisible();
    await expect(
      page.getByRole('button', { name: /^Generar observaciones/ }).locator('visible=true'),
    ).toBeVisible();

    await page.goto('/plan');
    await expect(page.getByText('Prueba Pro en 4° B: quedan 14 días.')).toBeVisible();
  });

  test('RN-082, RN-084, RN-089: paga 3 meses en OXXO con la ficha de prueba y queda pagado hasta su fin', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    await crearCicloYGrupo(page);
    await page.goto('/plan');
    await expect(page.getByText('Todos tus grupos están en Gratis.')).toBeVisible();
    await expect(page.getByText(/Modo de prueba/)).toBeVisible();

    await page.getByRole('checkbox', { name: /4° B/ }).check();
    const panel = page.locator('.pago-panel').locator('visible=true');
    await expect(panel.locator('.total .monto')).toHaveText('79');
    await expect(panel.getByText('79 primer grupo')).toBeVisible();
    await panel.getByRole('radio', { name: /OXXO, 3 meses/ }).check();
    await sinFallasDeAccesibilidad(page);
    await page
      .getByRole('button', { name: 'Generar ficha OXXO de 237 MXN' })
      .locator('visible=true')
      .click();

    await expect(page.getByRole('heading', { level: 1, name: 'Pago de prueba' })).toBeVisible();
    await expect(page.getByText('Ficha OXXO')).toBeVisible();
    await expect(page.locator('.referencia')).toHaveText(/^\d{4} \d{4} \d{4} \d{2}$/);
    await sinFallasDeAccesibilidad(page);
    await page.getByRole('button', { name: 'Simular pago en tienda' }).click();

    await expect(page.getByText(/^Pro en 1 grupo, pagado con OXXO hasta el /)).toBeVisible();
    const pagos = page.getByRole('region', { name: 'Historial de pagos' });
    await expect(pagos.getByText('Prepago de 3 meses · 1 grupo')).toBeVisible();
    await expect(pagos.getByText('Pagado')).toBeVisible();
    await expect(pagos.getByText('237 MXN')).toBeVisible();

    await page.goto('/');
    await expect(page.locator('app-etiqueta-plan').getByText('Pro', { exact: true })).toBeVisible();
  });

  test('RN-084, RN-085, RN-086: con tarjeta un cobro fallido muestra la gracia; en Gratis el urgente no va por WhatsApp', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page);
    await agregarTutor(page, grupo, /Cruz Ortiz/, 'Rosa Vega');

    // RN-086: sin Pro el interruptor se apaga y dice por qué.
    await page.goto(grupo + '/avisos/nuevo');
    await expect(page.getByRole('switch', { name: /Urgente/ })).toBeDisabled();
    await expect(
      page.getByText('Con Pro, los urgentes también llegan por WhatsApp.'),
    ).toBeVisible();

    await page.getByRole('link', { name: 'Ver planes' }).click();
    await expect(page.getByRole('checkbox', { name: /4° B/ })).toBeChecked();
    await page.getByRole('button', { name: 'Pagar 79 MXN al mes' }).locator('visible=true').click();
    await expect(page.getByText('Pago con tarjeta')).toBeVisible();
    await page.getByRole('button', { name: 'Pagar 79 MXN' }).click();
    await expect(
      page.getByText(/^Pro en 1 grupo con tarjeta\. Próximo cobro: 79 MXN el /),
    ).toBeVisible();

    // RN-085: el cobro siguiente falla y corre la gracia, con aviso en el plan y en Inicio.
    await page.getByRole('button', { name: 'Simular tarjeta rechazada' }).click();
    await expect(page.getByText('No pudimos cobrar tu tarjeta.')).toBeVisible();
    await expect(page.getByText(/Tienes 7 días para actualizarla/)).toBeVisible();
    await sinFallasDeAccesibilidad(page);
    await page.goto('/');
    await expect(page.getByRole('link', { name: 'Actualizar mi tarjeta' })).toBeVisible();

    await page.goto(grupo + '/avisos/nuevo');
    await expect(page.getByRole('switch', { name: /Urgente/ })).toBeEnabled();
  });
});
