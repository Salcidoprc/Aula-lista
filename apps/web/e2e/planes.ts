import { Page, expect } from '@playwright/test';

/**
 * Activa Pro con tarjeta en el grupo (SPEC-08) con la página de pago de prueba: sin Stripe, pagar no cobra nada.
 * Termina en la pantalla Plan con el grupo en Pro.
 */
export async function activarPro(page: Page, grupo: string): Promise<void> {
  const id = grupo.split('/').pop()!;
  await page.goto(`/plan?grupo=${id}`);
  await page
    .getByRole('button', { name: /^Pagar \d+ MXN al mes$/ })
    .locator('visible=true')
    .click();
  await expect(page.getByRole('heading', { level: 1, name: 'Pago de prueba' })).toBeVisible();
  await page.getByRole('button', { name: /^Pagar \d+ MXN$/ }).click();
  await expect(page.getByText(/^Pro en 1 grupo con tarjeta/)).toBeVisible();
}
