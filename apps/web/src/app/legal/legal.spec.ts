import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';

import ajustes from '../../../../api/src/Aulisima.Api/appsettings.json';
import { DOCUMENTOS_LEGALES, RESPONSABLE } from './documentos';
import { Legal } from './legal';

describe('Documentos legales', () => {
  it('RN-003: la versión de cada documento que se acepta es la vigente en la API', () => {
    const vigentes = ajustes.Cuentas.VersionesLegales;
    expect(DOCUMENTOS_LEGALES.terminos.version).toBe(vigentes.Terminos);
    expect(DOCUMENTOS_LEGALES.privacidad.version).toBe(vigentes.Privacidad);
    expect(DOCUMENTOS_LEGALES.declaracion_datos.version).toBe(vigentes.DeclaracionDatos);
  });

  it('RNF-006: el aviso de privacidad identifica al responsable, su domicilio y el correo para derechos ARCO', () => {
    const texto = JSON.stringify(DOCUMENTOS_LEGALES.privacidad);
    expect(texto).toContain(RESPONSABLE.nombre);
    expect(texto).toContain(RESPONSABLE.domicilio);
    expect(texto).toContain(RESPONSABLE.correoPrivacidad);
    expect(texto).toContain('20 días hábiles');
  });

  it('cada sección tiene id único dentro de su documento', () => {
    for (const documento of Object.values(DOCUMENTOS_LEGALES)) {
      const ids = documento.secciones.map((s) => s.id);
      expect(new Set(ids).size).toBe(ids.length);
    }
  });
});

describe('Legal', () => {
  function pintar(documento: string): HTMLElement {
    TestBed.configureTestingModule({
      imports: [
        Legal,
        TranslocoTestingModule.forRoot({
          langs: { 'es-MX': { legal: { actualizado: 'Última actualización: {{ fecha }}' } } },
          translocoConfig: { availableLangs: ['es-MX'], defaultLang: 'es-MX' },
        }),
      ],
    });
    const fixture = TestBed.createComponent(Legal);
    fixture.componentRef.setInput('documento', documento);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('pinta el aviso de privacidad con sus secciones, listas y tablas', () => {
    const pagina = pintar('privacidad');
    expect(pagina.querySelector('h1')?.textContent).toBe('Aviso de privacidad integral');
    expect(pagina.querySelectorAll('h2').length).toBe(
      DOCUMENTOS_LEGALES.privacidad.secciones.length,
    );
    expect(pagina.querySelector('table th[scope="row"]')?.textContent).toBe('Docentes');
    expect(pagina.textContent).toContain('Última actualización: 25 de noviembre de 2026');
  });

  it('un documento desconocido muestra los términos', () => {
    expect(pintar('otro').querySelector('h1')?.textContent).toBe(
      DOCUMENTOS_LEGALES.terminos.titulo,
    );
  });
});
