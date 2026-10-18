import { Routes } from '@angular/router';

/** SPEC-06: el portal del tutor, aparte de la app del docente y sin sus guardias ni su sincronización. */
export const rutasPortal: Routes = [
  {
    path: '',
    title: 'titulos.misHijos',
    loadComponent: () => import('./mis-hijos').then((m) => m.MisHijos),
  },
  {
    path: 'alumnos/:id',
    title: 'titulos.hijo',
    loadComponent: () => import('./detalle-hijo').then((m) => m.DetalleHijo),
  },
  {
    path: 'alumnos/:id/boletas/:boletaId',
    title: 'titulos.boletaTutor',
    loadComponent: () => import('./boleta-tutor').then((m) => m.BoletaTutor),
  },
  {
    path: 'enlace-invalido',
    title: 'titulos.enlaceInvalido',
    loadComponent: () => import('./enlace-invalido').then((m) => m.EnlaceInvalido),
  },
];
