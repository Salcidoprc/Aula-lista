import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test.describe('H0: la app arranca y habla con la API', () => {
  test('muestra el inicio en español y el servicio en línea', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Tu aula, en orden');
    await expect(page.getByRole('status')).toHaveText(/Servicio en línea/);
    await expect(page.locator('html')).toHaveAttribute('lang', 'es-MX');
  });

  test('no tiene fallas de accesibilidad WCAG 2.1 AA', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('status')).toHaveText(/Servicio en línea/);

    const resultado = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();

    expect(resultado.violations).toEqual([]);
  });
});
