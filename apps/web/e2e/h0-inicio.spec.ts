import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test.describe('H0: la app arranca y habla con la API', () => {
  test('sin sesión, el inicio lleva a entrar, en español', async ({ page }) => {
    await page.goto('/');

    await expect(page).toHaveURL(/\/cuenta\/entrar/);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Entra a tu aula');
    await expect(page.locator('html')).toHaveAttribute('lang', 'es-MX');
  });

  test('la API responde a través del mismo origen', async ({ request }) => {
    const salud = await request.get('/salud');

    expect(salud.ok()).toBe(true);
  });

  test('no tiene fallas de accesibilidad WCAG 2.1 AA', async ({ page }) => {
    await page.goto('/cuenta/entrar');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    const resultado = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();

    expect(resultado.violations).toEqual([]);
  });
});
