import { Routes } from '@angular/router';
import { requierePerfilCompleto, requiereSesion, soloSinSesion } from './core/sesion/guardias';

export const routes: Routes = [
  {
    path: '',
    canActivate: [requiereSesion, requierePerfilCompleto],
    loadComponent: () => import('./docente/inicio/inicio').then((m) => m.Inicio),
  },
  {
    path: 'bienvenida',
    canActivate: [requiereSesion],
    loadComponent: () => import('./docente/cuenta/bienvenida').then((m) => m.Bienvenida),
  },
  {
    path: 'ajustes',
    canActivate: [requiereSesion, requierePerfilCompleto],
    children: [
      { path: '', loadComponent: () => import('./docente/ajustes/ajustes').then((m) => m.Ajustes) },
      {
        path: 'exportaciones/:id',
        loadComponent: () => import('./docente/ajustes/exportacion').then((m) => m.Exportacion),
      },
    ],
  },
  {
    path: 'cuenta',
    children: [
      {
        path: 'entrar',
        canActivate: [soloSinSesion],
        loadComponent: () => import('./docente/cuenta/entrar').then((m) => m.Entrar),
      },
      {
        path: 'registro',
        canActivate: [soloSinSesion],
        loadComponent: () => import('./docente/cuenta/registro').then((m) => m.Registro),
      },
      {
        path: 'verificar',
        loadComponent: () => import('./docente/cuenta/verificar').then((m) => m.Verificar),
      },
      {
        path: 'recuperar',
        loadComponent: () => import('./docente/cuenta/recuperar').then((m) => m.Recuperar),
      },
      {
        path: 'restablecer',
        loadComponent: () => import('./docente/cuenta/restablecer').then((m) => m.Restablecer),
      },
      {
        path: 'eliminada',
        loadComponent: () => import('./docente/cuenta/eliminada').then((m) => m.CuentaEliminada),
      },
      { path: '', pathMatch: 'full', redirectTo: 'entrar' },
    ],
  },
  { path: 'legal/:documento', loadComponent: () => import('./legal/legal').then((m) => m.Legal) },
  { path: '**', redirectTo: '' },
];
