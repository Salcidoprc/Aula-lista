import { Routes } from '@angular/router';
import { requierePerfilCompleto, requiereSesion, soloSinSesion } from './core/sesion/guardias';

export const routes: Routes = [
  {
    path: '',
    canActivate: [requiereSesion, requierePerfilCompleto],
    title: 'titulos.inicio',
    loadComponent: () => import('./docente/inicio/inicio').then((m) => m.Inicio),
  },
  {
    path: 'ciclos',
    canActivate: [requiereSesion, requierePerfilCompleto],
    children: [
      {
        path: 'nuevo',
        title: 'titulos.cicloNuevo',
        loadComponent: () => import('./docente/ciclos/ciclo').then((m) => m.Ciclo),
      },
      {
        path: ':id',
        title: 'titulos.ciclo',
        loadComponent: () => import('./docente/ciclos/ciclo').then((m) => m.Ciclo),
      },
    ],
  },
  {
    path: 'grupos',
    canActivate: [requiereSesion, requierePerfilCompleto],
    children: [
      {
        path: 'nuevo',
        title: 'titulos.grupoNuevo',
        loadComponent: () => import('./docente/grupos/crear-grupo').then((m) => m.CrearGrupo),
      },
      {
        path: ':id',
        title: 'titulos.calificaciones',
        loadComponent: () =>
          import('./docente/evaluacion/calificaciones').then((m) => m.Calificaciones),
      },
      {
        path: ':id/alumnos',
        title: 'titulos.grupo',
        loadComponent: () => import('./docente/grupos/grupo').then((m) => m.Grupo),
      },
      {
        path: ':id/periodos/:periodoId/rubros',
        title: 'titulos.rubros',
        loadComponent: () => import('./docente/evaluacion/rubros').then((m) => m.Rubros),
      },
      {
        path: ':id/periodos/:periodoId/actividades/:actividadId',
        title: 'titulos.captura',
        loadComponent: () => import('./docente/evaluacion/captura').then((m) => m.Captura),
      },
      {
        path: ':id/periodos/:periodoId/tabla',
        title: 'titulos.tabla',
        loadComponent: () => import('./docente/evaluacion/tabla').then((m) => m.Tabla),
      },
      {
        path: ':id/ajustes',
        title: 'titulos.configuracionGrupo',
        loadComponent: () =>
          import('./docente/grupos/configuracion-grupo').then((m) => m.ConfiguracionGrupo),
      },
      {
        path: ':id/importar',
        title: 'titulos.importar',
        loadComponent: () => import('./docente/importar/importar').then((m) => m.Importar),
      },
    ],
  },
  {
    path: 'alumnos/:id',
    canActivate: [requiereSesion, requierePerfilCompleto],
    title: 'titulos.alumno',
    loadComponent: () => import('./docente/alumnos/ficha-alumno').then((m) => m.FichaAlumno),
  },
  {
    path: 'bienvenida',
    canActivate: [requiereSesion],
    title: 'titulos.bienvenida',
    loadComponent: () => import('./docente/cuenta/bienvenida').then((m) => m.Bienvenida),
  },
  {
    path: 'ajustes',
    canActivate: [requiereSesion, requierePerfilCompleto],
    children: [
      {
        path: '',
        title: 'titulos.ajustes',
        loadComponent: () => import('./docente/ajustes/ajustes').then((m) => m.Ajustes),
      },
      {
        path: 'exportaciones/:id',
        title: 'titulos.exportacion',
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
        title: 'titulos.entrar',
        loadComponent: () => import('./docente/cuenta/entrar').then((m) => m.Entrar),
      },
      {
        path: 'registro',
        canActivate: [soloSinSesion],
        title: 'titulos.registro',
        loadComponent: () => import('./docente/cuenta/registro').then((m) => m.Registro),
      },
      {
        path: 'verificar',
        title: 'titulos.verificar',
        loadComponent: () => import('./docente/cuenta/verificar').then((m) => m.Verificar),
      },
      {
        path: 'recuperar',
        title: 'titulos.recuperar',
        loadComponent: () => import('./docente/cuenta/recuperar').then((m) => m.Recuperar),
      },
      {
        path: 'restablecer',
        title: 'titulos.restablecer',
        loadComponent: () => import('./docente/cuenta/restablecer').then((m) => m.Restablecer),
      },
      {
        path: 'eliminada',
        title: 'titulos.eliminada',
        loadComponent: () => import('./docente/cuenta/eliminada').then((m) => m.CuentaEliminada),
      },
      { path: '', pathMatch: 'full', redirectTo: 'entrar' },
    ],
  },
  {
    path: 'legal/:documento',
    title: 'titulos.legal',
    loadComponent: () => import('./legal/legal').then((m) => m.Legal),
  },
  { path: '**', redirectTo: '' },
];
