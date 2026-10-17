import { expect, test } from '@playwright/test';
import { registrarYCompletarPerfil, sinFallasDeAccesibilidad } from './cuenta';
import { crearCicloYGrupo, docenteVerificado } from './grupos';

test.describe('SPEC-02: grupos, alumnos y tutores', () => {
  test('ciclo de agosto a julio por trimestre: propone 3 unidades editables y crea varios grupos', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    await expect(page.getByText('Tu grupo listo en tres pasos')).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    await page.getByRole('link', { name: 'Definir mi ciclo' }).click();
    await expect(page).toHaveTitle('Tu ciclo escolar · Aula Lista');
    await page.getByLabel('Empieza el').fill('2026-08-25');
    await page.getByLabel('Termina el').fill('2027-07-16');
    await expect(page.getByLabel('Divido el ciclo en')).toHaveValue('trimestre');
    await expect(page.getByLabel('Nombre', { exact: true })).toHaveCount(3);
    await expect(page.locator('#unidad-inicio-0')).toHaveValue('2026-08-25');
    await expect(page.locator('#unidad-fin-2')).toHaveValue('2027-07-16');
    await sinFallasDeAccesibilidad(page);

    // RN-023: dos unidades que se traslapan no se guardan.
    await page.locator('#unidad-inicio-1').fill('2026-11-01');
    await page.getByRole('button', { name: 'Guardar ciclo' }).click();
    await expect(page.getByText('Dos unidades no pueden compartir fechas.')).toBeVisible();
    await page.locator('#unidad-fin-0').fill('2026-10-31');
    await page.locator('#unidad-nombre-0').fill('Primer trimestre');
    await page.getByRole('button', { name: 'Guardar ciclo' }).click();

    await expect(page).toHaveURL(/\/$/);
    await page.getByRole('link', { name: 'Crear mi primer grupo' }).click();
    await page.getByText('Crear varios grupos a la vez').click();
    await page.getByText('4°', { exact: true }).click();
    await page.getByLabel('Grupos', { exact: true }).fill('A, B, C');
    await expect(page.getByText('Así quedan tus 3 grupos')).toBeVisible();
    await sinFallasDeAccesibilidad(page);
    await page.getByRole('button', { name: 'Crear 3 grupos' }).click();

    await expect(page.getByText('Se crearon 3 grupos.')).toBeVisible();
    for (const nombre of ['4° A', '4° B', '4° C']) {
      await expect(page.getByRole('link', { name: new RegExp(nombre) })).toBeVisible();
    }
    await expect(page.getByRole('link', { name: '3 unidades' })).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    // Las unidades editadas quedaron en el ciclo que heredan los grupos (RN-025).
    await page.getByRole('link', { name: '3 unidades' }).click();
    await expect(page.locator('#unidad-nombre-0')).toHaveValue('Primer trimestre');
    await expect(page.locator('#unidad-fin-0')).toHaveValue('2026-10-31');
  });

  test('RF-014: pega una lista numerada y quedan con número por orden alfabético', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    await crearCicloYGrupo(page);
    await expect(page.locator('app-portada')).toHaveAttribute('data-grado', 'p4');
    await expect(page.getByText('Agrega a tus alumnos')).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    await page.getByRole('link', { name: /Pegar lista/ }).click();
    await expect(page.getByRole('tab', { name: 'Pegar lista' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    await page
      .getByLabel('Lista de alumnos')
      .fill('1. Ruiz Vega Ana Sofía\n2. Álvarez Soto José Luis\n3) De la Cruz Pérez María');
    await page.getByRole('button', { name: 'Ver cómo queda' }).click();

    await expect(page.getByText('3 listos')).toBeVisible();
    await expect(page.getByText('De la Cruz Pérez')).toBeVisible();
    await sinFallasDeAccesibilidad(page);
    await page.getByRole('button', { name: 'Importar 3 alumnos' }).click();

    await expect(page.getByText('Se importaron 3 alumnos.')).toBeVisible();
    const filas = page.getByRole('list', { name: 'Lista del grupo' }).getByRole('listitem');
    await expect(filas).toHaveCount(3);
    await expect(filas.nth(0)).toContainText('Álvarez Soto José Luis');
    await expect(filas.nth(2)).toContainText('Ruiz Vega Ana Sofía');
    await expect(filas.nth(0)).toContainText('Sin tutor');
    await sinFallasDeAccesibilidad(page);
  });

  test('RF-013: CSV con tutores; la fila con WhatsApp inválido se corrige en la vista previa', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    await crearCicloYGrupo(page);
    await page.goto(page.url().replace(/\/alumnos$/, '') + '/importar?origen=excel');
    const csv = [
      'No.;Apellidos;Nombre(s);Nombre del tutor;Celular;Parentesco',
      '1;García López;Ana;María López;667 123 4567;Mamá',
      '2;Hernández;Luis;Pedro Hernández;12345;Papá',
      '3;Soto;Eva;;;',
    ].join('\n');
    await page.locator('input[type=file]').setInputFiles({
      name: 'lista.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csv, 'utf-8'),
    });

    await expect(page.getByText('2 listos')).toBeVisible();
    await expect(page.getByText('1 por corregir')).toBeVisible();
    await expect(page.getByText('WhatsApp inválido')).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    await page.getByRole('button', { name: 'Corregir renglón 2' }).click();
    await page.getByLabel('WhatsApp del tutor 1').fill('667 765 4321');
    await expect(page.getByText('3 listos')).toBeVisible();
    await page.getByRole('button', { name: 'Importar 3 alumnos' }).click();

    await expect(page.getByText('Se importaron 3 alumnos.')).toBeVisible();
    await page.getByRole('link', { name: /García López Ana/ }).click();
    await expect(page.getByText('María López').first()).toBeVisible();
    await expect(page.getByText('667 123 4567')).toBeVisible();
  });

  test('ficha: agrega tutor, cambia el trato y da de baja (RN-014, RN-020, RN-017)', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    await crearCicloYGrupo(page);
    await page.getByRole('button', { name: /Uno por uno/ }).click();
    await page.getByLabel('Apellidos').fill('Ruiz Vega');
    await page.getByLabel('Nombres').fill('Ana Sofía');
    await page.getByRole('button', { name: 'Agregar', exact: true }).click();
    await expect(page.getByLabel('Apellidos')).toBeFocused();
    await page.getByRole('button', { name: 'Listo' }).click();

    await page.getByRole('link', { name: /Ruiz Vega/ }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Ana Sofía Ruiz Vega');
    await sinFallasDeAccesibilidad(page);

    await page.getByLabel('Nombre del tutor').fill('Rosa Vega');
    await page.getByLabel('WhatsApp').fill('667-555-0101');
    await page.getByLabel('Parentesco').fill('Mamá');
    await page.getByRole('button', { name: 'Agregar tutor' }).click();
    await expect(page.getByText('667 555 0101')).toBeVisible();

    await page.getByLabel('Ella').check();
    await page.getByRole('button', { name: 'Guardar datos' }).click();
    await expect(page.getByText('Datos guardados.')).toBeVisible();

    await page.getByRole('button', { name: 'Dar de baja' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Dar de baja' }).click();
    await expect(page.getByText('Se dio de baja a Ana Sofía.')).toBeVisible();
    await expect(page.getByText('Bajas (1)')).toBeVisible();
  });

  test('RN-001: sin correo confirmado crea grupos pero no da de alta alumnos', async ({ page }) => {
    await registrarYCompletarPerfil(page);
    await crearCicloYGrupo(page);
    await expect(page.getByText('Lo necesitas antes de dar de alta a tus alumnos.')).toBeVisible();
    await expect(page.getByRole('link', { name: /Pegar lista/ })).toHaveCount(0);
  });
});
