import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { TemaService } from './tema.service';

/** Consulta de medios falsa: el sistema empieza claro u oscuro y puede cambiar mientras la app está abierta. */
function sistema(oscuro: boolean) {
  const oyentes: ((e: { matches: boolean }) => void)[] = [];
  const consulta = {
    matches: oscuro,
    addEventListener: (_: string, oyente: (e: { matches: boolean }) => void) =>
      oyentes.push(oyente),
  };
  return {
    consulta,
    cambiar(matches: boolean) {
      consulta.matches = matches;
      oyentes.forEach((o) => o({ matches }));
    },
  };
}

describe('TemaService (RF-009)', () => {
  const html = document.documentElement;

  function crear(oscuro: boolean) {
    const s = sistema(oscuro);
    vi.stubGlobal('matchMedia', () => s.consulta);
    const servicio = TestBed.inject(TemaService);
    TestBed.tick();
    return { servicio, sistema: s };
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    vi.unstubAllGlobals();
    html.removeAttribute('data-tema');
  });

  it('sin elección sigue al sistema y marca data-tema en <html>', () => {
    const { servicio } = crear(true);
    expect(servicio.preferencia()).toBe('sistema');
    expect(servicio.tema()).toBe('oscuro');
    expect(TestBed.inject(DOCUMENT).documentElement.getAttribute('data-tema')).toBe('oscuro');
  });

  it('con "Automática" cambia cuando el sistema pasa a modo oscuro', () => {
    const { servicio, sistema } = crear(false);
    expect(servicio.tema()).toBe('claro');
    sistema.cambiar(true);
    TestBed.tick();
    expect(servicio.tema()).toBe('oscuro');
    expect(html.getAttribute('data-tema')).toBe('oscuro');
  });

  it('la elección manda sobre el sistema y se guarda en el dispositivo', () => {
    const { servicio, sistema } = crear(true);
    servicio.elegir('claro');
    TestBed.tick();
    expect(servicio.tema()).toBe('claro');
    expect(html.getAttribute('data-tema')).toBe('claro');
    sistema.cambiar(false);
    servicio.elegir('oscuro');
    TestBed.tick();
    expect(html.getAttribute('data-tema')).toBe('oscuro');
    expect(localStorage.getItem('aula-lista.tema')).toBe('oscuro');
  });

  it('al abrir de nuevo la app recupera la elección; "Automática" la borra', () => {
    localStorage.setItem('aula-lista.tema', 'oscuro');
    const { servicio } = crear(false);
    expect(servicio.preferencia()).toBe('oscuro');
    expect(servicio.tema()).toBe('oscuro');
    servicio.elegir('sistema');
    expect(localStorage.getItem('aula-lista.tema')).toBeNull();
    expect(servicio.tema()).toBe('claro');
  });

  it('un valor guardado que no se reconoce vuelve a "Automática"', () => {
    localStorage.setItem('aula-lista.tema', 'morado');
    expect(crear(false).servicio.preferencia()).toBe('sistema');
  });
});
