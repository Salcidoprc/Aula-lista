import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';

import { PerfilDocente } from '../../api/models/perfil-docente';
import { adminAlPanel } from './guardias';
import { PerfilService } from './perfil.service';
import { SesionService } from './sesion.service';

describe('adminAlPanel (SPEC-09)', () => {
  function correr(conSesion: boolean, perfil: Partial<PerfilDocente> | null) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SesionService, useValue: { asegurar: async () => conSesion } },
        { provide: PerfilService, useValue: { cargar: async () => perfil } },
      ],
    });
    return TestBed.runInInjectionContext(() =>
      adminAlPanel({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Promise<boolean | UrlTree>;
  }

  it('la cuenta admin abre directo en el panel', async () => {
    const resultado = await correr(true, { esAdmin: true });
    expect(TestBed.inject(Router).serializeUrl(resultado as UrlTree)).toBe('/admin');
  });

  it('un docente sigue a sus grupos', async () => {
    expect(await correr(true, { esAdmin: false })).toBe(true);
  });

  it('sin sesión o sin perfil deja que decidan las demás guardias', async () => {
    expect(await correr(false, null)).toBe(true);
    TestBed.resetTestingModule();
    expect(await correr(true, null)).toBe(true);
  });
});
