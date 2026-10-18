import { HijoDto } from '../api/models/hijo-dto';
import { urlWhatsApp } from '../docente/alumnos/ficha-alumno';
import {
  calificacion,
  esCorregida,
  etiquetaHijo,
  fechaCorta,
  grupoDelHijo,
  peso,
  primerNombre,
  tokenDelFragmento,
} from './portal';

const hijo = (cambios: Partial<HijoDto>): HijoDto => ({
  id: 'a',
  nombres: 'Sofía',
  apellidos: 'Ramírez',
  grado: 4,
  grupo: 'B',
  materia: null,
  nivel: 'primaria',
  docente: 'Ana López',
  dadoDeBaja: false,
  ultimaBoleta: null,
  ...cambios,
});

describe('tokenDelFragmento (RF-060)', () => {
  it('toma el token del fragmento, sin el signo', () => {
    expect(tokenDelFragmento('#abcDEF0123456789_-xyzQ')).toBe('abcDEF0123456789_-xyzQ');
  });

  it.each(['', '#', '#corto', '#con espacio y más texto', null, undefined])(
    'ignora "%s"',
    (fragmento) => {
      expect(tokenDelFragmento(fragmento)).toBeNull();
    },
  );
});

describe('portal del tutor', () => {
  it('saluda por el primer nombre', () => {
    expect(primerNombre('  Laura  Treviño ')).toBe('Laura');
    expect(primerNombre('')).toBeNull();
  });

  it('muestra la final con los decimales del grupo y el peso sin ceros', () => {
    expect(calificacion('8.7', 1)).toBe('8.7');
    expect(calificacion(9, 2)).toBe('9.00');
    expect(calificacion(null, 1)).toBe('—');
    expect(peso('12.50')).toBe('12.5%');
  });

  it('marca la versión corregida y su fecha dd/mm/aaaa (RN-052)', () => {
    expect(esCorregida(1)).toBe(false);
    expect(esCorregida('2')).toBe(true);
    expect(fechaCorta('2026-10-12T18:00:00Z')).toMatch(/^12\/10\/2026$/);
  });

  it('nombra el grupo con grado, letra y materia', () => {
    expect(grupoDelHijo(hijo({ grado: '2', grupo: 'A', materia: 'Español' }))).toContain('2');
  });

  it('distingue chips con el mismo nombre de pila', () => {
    const uno = hijo({ id: '1', materia: 'Español' });
    const otro = hijo({ id: '2', materia: 'Historia' });
    const hermano = hijo({ id: '3', nombres: 'Mateo' });
    expect(etiquetaHijo(uno, [uno, otro, hermano])).toBe('Sofía · Español');
    expect(etiquetaHijo(hermano, [uno, otro, hermano])).toBe('Mateo');
    const homonimo = hijo({ id: '4', materia: 'Español', apellidos: 'Soto' });
    expect(etiquetaHijo(homonimo, [uno, homonimo])).toBe('Sofía Soto');
  });
});

describe('urlWhatsApp (RN-063)', () => {
  it('quita el + del número y codifica el mensaje con el enlace', () => {
    const url = urlWhatsApp('+526671234567', 'Hola: https://aulalista.mx/tutor#abc');
    expect(url).toBe(
      'https://wa.me/526671234567?text=Hola%3A%20https%3A%2F%2Faulalista.mx%2Ftutor%23abc',
    );
  });
});
