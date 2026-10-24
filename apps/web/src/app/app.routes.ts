import { Routes } from '@angular/router';
import { confirmarSalida } from './shared/rutas';
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
        path: ':id/asistencia',
        title: 'titulos.asistencia',
        loadComponent: () => import('./docente/asistencia/pase-lista').then((m) => m.PaseLista),
      },
      {
        path: ':id/asistencia/historial',
        title: 'titulos.historialAsistencia',
        loadComponent: () =>
          import('./docente/asistencia/historial-asistencia').then((m) => m.HistorialAsistencia),
      },
      {
        path: ':id/avisos',
        title: 'titulos.avisos',
        loadComponent: () => import('./docente/avisos/avisos-grupo').then((m) => m.AvisosGrupo),
      },
      {
        path: ':id/avisos/nuevo',
        title: 'titulos.nuevoAviso',
        loadComponent: () => import('./docente/avisos/nuevo-aviso').then((m) => m.NuevoAviso),
      },
      {
        path: ':id/equipos',
        title: 'titulos.equipos',
        canDeactivate: [confirmarSalida],
        loadComponent: () => import('./docente/evaluacion/equipos').then((m) => m.Equipos),
      },
      {
        path: ':id/periodos/:periodoId/rubros',
        title: 'titulos.rubros',
        canDeactivate: [confirmarSalida],
        loadComponent: () => import('./docente/evaluacion/rubros').then((m) => m.Rubros),
      },
      {
        path: ':id/periodos/:periodoId/actividades/:actividadId',
        title: 'titulos.captura',
        loadComponent: () => import('./docente/evaluacion/captura').then((m) => m.Captura),
      },
      {
        path: ':id/periodos/:periodoId/cierre',
        title: 'titulos.cierre',
        loadComponent: () => import('./docente/cierre/cierre-periodo').then((m) => m.CierrePeriodo),
      },
      {
        path: ':id/periodos/:periodoId/cierre/:alumnoId',
        title: 'titulos.revisarObservacion',
        loadComponent: () =>
          import('./docente/cierre/revisar-observacion').then((m) => m.RevisarObservacion),
      },
      {
        path: ':id/periodos/:periodoId/boletas',
        title: 'titulos.boletas',
        loadComponent: () =>
          import('./docente/boletas/boletas-periodo').then((m) => m.BoletasPeriodo),
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
    path: 'avisos/:id',
    canActivate: [requiereSesion, requierePerfilCompleto],
    title: 'titulos.aviso',
    loadComponent: () => import('./docente/avisos/detalle-aviso').then((m) => m.DetalleAviso),
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
        path: 'estilo',
        title: 'titulos.estilo',
        loadComponent: () =>
          import('./docente/ajustes/estilo-observaciones').then((m) => m.EstiloObservaciones),
      },
      {
        path: 'exportaciones/:id',
        title: 'titulos.exportacion',
        loadComponent: () => import('./docente/ajustes/exportacion').then((m) => m.Exportacion),
      },
    ],
  },
  {
    path: 'plan',
    canActivate: [requiereSesion, requierePerfilCompleto],
    children: [
      {
        path: '',
        title: 'titulos.plan',
        loadComponent: () => import('./docente/planes/plan').then((m) => m.Plan),
      },
      {
        path: 'pago-simulado/:id',
        title: 'titulos.pagoSimulado',
        loadComponent: () => import('./docente/planes/pago-simulado').then((m) => m.PagoSimulado),
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
    path: 'admin',
    canActivate: [requiereSesion],
    loadChildren: () => import('./admin/admin.routes').then((m) => m.rutasAdmin),
  },
  {
    path: 'tutor',
    loadChildren: () => import('./tutor/portal.routes').then((m) => m.rutasPortal),
  },
  {
    path: 'legal/:documento',
    title: 'titulos.legal',
    loadComponent: () => import('./legal/legal').then((m) => m.Legal),
  },
  { path: '**', redirectTo: '' },
];
