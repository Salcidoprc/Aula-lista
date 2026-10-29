import { execSync } from 'node:child_process';
import { Page, expect, test } from '@playwright/test';
import { grupoConAlumnos } from './avisos';
import { sinFallasDeAccesibilidad } from './cuenta';
import { docenteVerificado } from './grupos';

/**
 * El rol admin solo se da con `--admin <correo>` (SPEC-09). Por defecto en el contenedor de la API de Compose;
 * `E2E_HACER_ADMIN` lo cambia (por ejemplo, con la API corriendo con `dotnet run`), con `{correo}` en su lugar.
 */
function hacerAdmin(correo: string): void {
  const comando =
    process.env['E2E_HACER_ADMIN'] ??
    'docker compose -f ../../infra/docker-compose.yml exec -T api dotnet AulaLista.Api.dll --admin {correo}';
  execSync(comando.replace('{correo}', correo), { stdio: 'pipe' });
}

/** El catálogo es de toda la instalación y la base local se comparte entre corridas: un nombre por corrida. */
const nombreNuevo = () => `Laboratorio ${Math.floor(Math.random() * 1e4)}`;

async function abrirSeccion(page: Page, titulo: string): Promise<void> {
  await page
    .getByRole('navigation', { name: 'Secciones del panel' })
    .getByRole('link', { name: titulo })
    .click();
  await expect(page.getByRole('heading', { level: 1, name: titulo })).toBeVisible();
}

test.describe('SPEC-09: operación y métricas', () => {
  test('RN-090: quien no es admin no ve el panel y la API le responde 404', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    // El panel pregunta a la API antes de mostrarse; a quien no es admin le responde 404 y vuelve a sus grupos.
    const respuesta = page.waitForResponse((r) => r.url().includes('/api/v1/admin/'));
    await page.goto('/admin/metricas');
    expect((await respuesta).status()).toBe(404);
    await expect(page.getByRole('heading', { level: 1, name: 'Mis grupos' })).toBeVisible();
  });

  test('RN-092, RN-094, RN-095: el admin crea un tipo que el docente ve al instante, lo desactiva y queda en la auditoría', async ({
    page,
    request,
  }) => {
    test.setTimeout(120_000);
    const correo = await docenteVerificado(page, request);
    hacerAdmin(correo);
    const nombre = nombreNuevo();

    // RF-094: el panel abre en el Resumen, con sus cinco bloques y el embudo completo.
    await page.goto('/admin');
    await expect(page.getByRole('heading', { level: 1, name: 'Resumen' })).toBeVisible();
    for (const bloque of ['Uso', 'Dinero', 'Activación', 'Valor entregado', 'Retención']) {
      await expect(page.getByRole('heading', { level: 2, name: bloque })).toBeVisible();
    }
    await expect(page.getByText('Publicó boletas')).toBeVisible();
    await expect(page.getByRole('img', { name: /^Activos en 7 días por semana:/ })).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    await abrirSeccion(page, 'Métricas del piloto');
    await expect(page.getByRole('region', { name: 'Boleta del piloto por semana' })).toBeVisible();
    for (const metrica of [
      'Docentes activos',
      'Cierran un periodo',
      'Minutos para cerrar',
      'Observaciones aceptadas',
      'Tutores que abren',
      'Pagan después de la prueba',
    ]) {
      await expect(page.getByRole('rowheader', { name: new RegExp(`^${metrica}`) })).toBeVisible();
    }
    await sinFallasDeAccesibilidad(page);

    await abrirSeccion(page, 'Tipos de rubro');
    await page.getByRole('button', { name: 'Nuevo tipo' }).click();
    const hoja = page.getByRole('dialog');
    await hoja.getByLabel('Nombre', { exact: true }).fill(nombre);
    await hoja.getByLabel('Cómo se calcula').selectOption({ label: 'Promedio de actividades' });
    await hoja.getByLabel('Nombres sugeridos').fill('Práctica 1\nReporte');
    await sinFallasDeAccesibilidad(page);
    await hoja.getByRole('button', { name: 'Crear tipo' }).click();
    await expect(page.getByText(`Listo: los docentes ya ven ${nombre}.`)).toBeVisible();

    // RN-092: sin desplegar, el tipo nuevo ya está en el editor de rubros del docente.
    await page.goto(await grupoConAlumnos(page));
    await page.getByRole('link', { name: 'Crear rubros' }).click();
    await page.getByRole('button', { name: 'Agregar rubro' }).first().click();
    await expect(
      page
        .locator('section.bloque')
        .first()
        .getByLabel('Tipo')
        .locator('option', { hasText: nombre }),
    ).toHaveCount(1);

    // Desactivarlo: deja de ofrecerse, no se borra.
    await page.goto('/admin/tipos-rubro');
    await page.getByRole('button', { name: `Editar ${nombre}` }).click();
    await page
      .getByRole('dialog')
      .getByRole('switch', { name: /Se ofrece a los docentes/ })
      .click();
    await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();
    await expect(page.getByText(`Se guardó ${nombre}.`)).toBeVisible();
    await expect(page.getByRole('row', { name: new RegExp(nombre) })).toContainText('Desactivado');

    // RN-095: cada cambio queda con antes y después.
    await abrirSeccion(page, 'Auditoría');
    const registro = page.getByRole('article').filter({ hasText: 'Desactivó el tipo' }).first();
    await expect(registro).toContainText(correo);
    await expect(registro.getByRole('row', { name: /Se ofrece/ })).toContainText('Sí');
    await expect(registro.getByRole('row', { name: /Se ofrece/ })).toContainText('No');
    await expect(
      page.getByRole('article').filter({ hasText: 'Creó el tipo' }).filter({ hasText: nombre }),
    ).toHaveCount(1);
    await sinFallasDeAccesibilidad(page);

    for (const titulo of ['Costos del mes', 'Docentes', 'Errores de procesos']) {
      await abrirSeccion(page, titulo);
      await sinFallasDeAccesibilidad(page);
    }
    await expect(
      page.getByText(/nunca muestra alumnos, tutores, notas ni observaciones/),
    ).toBeAttached();
  });
});
