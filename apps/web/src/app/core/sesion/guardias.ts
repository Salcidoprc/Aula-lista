import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { PerfilService } from './perfil.service';
import { SesionService } from './sesion.service';

/** Pantallas del docente: sin sesión, manda a entrar y regresa después a donde iba. */
export const requiereSesion: CanActivateFn = async (_ruta, estado) => {
  const router = inject(Router);
  if (await inject(SesionService).asegurar()) {
    return true;
  }
  return router.createUrlTree(['/cuenta/entrar'], { queryParams: { volver: estado.url } });
};

/** Entrar y crear cuenta: con sesión activa no tiene caso mostrarlas. */
export const soloSinSesion: CanActivateFn = async () => {
  const router = inject(Router);
  return (await inject(SesionService).asegurar()) ? router.createUrlTree(['/']) : true;
};

/**
 * Mientras falte el perfil o una aceptación legal vigente (RN-003), se pasa por la bienvenida.
 * Angular corre las guardias de una ruta a la vez: sin sesión, deja que `requiereSesion` redirija.
 */
export const requierePerfilCompleto: CanActivateFn = async () => {
  const router = inject(Router);
  const servicio = inject(PerfilService);
  if (!(await inject(SesionService).asegurar())) {
    return true;
  }
  const perfil = await servicio.cargar();
  if (perfil === null || (perfil.perfilCompleto && perfil.aceptacionesPendientes.length === 0)) {
    return true;
  }
  return router.createUrlTree(['/bienvenida']);
};

/**
 * SPEC-09: el administrador entra directo al panel. Al iniciar sesión o abrir la raíz, si el perfil dice que la cuenta
 * es admin, se le lleva a `/admin`; un docente sigue a Mis grupos. Va antes de la bienvenida: el panel no la necesita.
 */
export const adminAlPanel: CanActivateFn = async () => {
  const router = inject(Router);
  if (!(await inject(SesionService).asegurar())) {
    return true;
  }
  const perfil = await inject(PerfilService).cargar();
  return perfil?.esAdmin ? router.createUrlTree(['/admin']) : true;
};

/** La landing (RF-010) solo existe sin sesión: con sesión, la raíz es Mis grupos. */
export const sinSesion: CanMatchFn = async () => !(await inject(SesionService).asegurar());
