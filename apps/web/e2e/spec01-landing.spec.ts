import { expect, test } from '@playwright/test';
import { sinFallasDeAccesibilidad } from './cuenta';
import { docenteVerificado } from './grupos';

test.describe('SPEC-01: landing pública (RF-010)', () => {
  test('RF-010: sin sesión, la raíz explica Aulísima, muestra los precios del catálogo y lleva a crear cuenta o entrar', async ({
    page,
    request,
  }) => {
    const planes = (await (await request.get('/api/v1/planes')).json()) as {
      aLaVenta: boolean;
      precioPrimerGrupo: number;
      precioGrupoAdicional: number;
    };

    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Califica en clase.');
    await expect(page).toHaveTitle(/Aulísima/);
    // La cabecera de la app no aparece: la landing trae su propia portada y navegación.
    await expect(page.locator('app-portada')).toHaveCount(0);
    await expect(page.getByRole('heading', { level: 3 })).toHaveCount(14);
    await sinFallasDeAccesibilidad(page);

    // Precios del catálogo (ADR-0017), no del código: el total sigue al número de grupos.
    const precios = page.locator('#precios');
    await precios.scrollIntoViewIfNeeded();
    if (planes.aLaVenta) {
      const total = (n: number) =>
        Number(planes.precioPrimerGrupo) + Number(planes.precioGrupoAdicional) * (n - 1);
      await expect(precios.getByText(`${total(1)} MXN al mes`)).toBeVisible();
      await precios.getByRole('button', { name: 'Un grupo más' }).click();
      await precios.getByRole('button', { name: 'Un grupo más' }).click();
      await expect(precios.getByText('3 grupos')).toBeVisible();
      await expect(precios.getByText(`${total(3)} MXN al mes`)).toBeVisible();
    }

    // Preguntas: una a la vez, con su estado para lectores de pantalla.
    const pregunta = page.getByRole('button', { name: '¿Las familias necesitan crear cuenta?' });
    await pregunta.click();
    await expect(pregunta).toHaveAttribute('aria-expanded', 'true');
    await expect(page.getByText('Cada tutor recibe un enlace personal')).toBeVisible();

    await page.getByRole('link', { name: 'Crear cuenta gratis' }).first().click();
    await expect(page).toHaveURL(/\/cuenta\/registro$/);
    await page.goto('/');
    await page.getByRole('link', { name: 'Entrar', exact: true }).first().click();
    await expect(page).toHaveURL(/\/cuenta\/entrar$/);
  });

  test('RF-010: "Pruébalo aquí" califica a seis alumnos de ejemplo y mide el tiempo', async ({
    page,
  }) => {
    await page.goto('/');
    const teclado = page.getByRole('group', { name: 'Teclado rápido' });
    await teclado.scrollIntoViewIfNeeded();
    for (const v of ['10', '9', '8', '9', '7', '10']) {
      await teclado.getByRole('button', { name: v, exact: true }).click();
    }
    await expect(page.getByText(/^Listo: 6 alumnos en [\d.]+ s\.$/)).toBeVisible();
    await expect(page.getByText(/un grupo de 35 te toma/)).toBeVisible();
    await page.getByRole('button', { name: 'Otra vez' }).click();
    await expect(
      page.getByRole('button', { name: /Valeria Aguilar Ramos: Sin calificar/ }),
    ).toBeVisible();
  });

  test('RF-010: con sesión, la raíz abre Mis grupos y no la landing', async ({ page, request }) => {
    await docenteVerificado(page, request);
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1, name: 'Mis grupos' })).toBeVisible();
    await expect(page.locator('app-landing')).toHaveCount(0);
  });
});
