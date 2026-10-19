import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AdminService } from './admin.service';

/**
 * SPEC-09: el panel solo existe para un administrador. A cualquier otro la API le responde 404 y aquí se le lleva al
 * inicio, igual que con una dirección que no existe.
 */
export const requiereAdmin: CanActivateFn = async () => {
  const router = inject(Router);
  try {
    await inject(AdminService).comportamientos();
    return true;
  } catch {
    return router.createUrlTree(['/']);
  }
};

export const rutasAdmin: Routes = [
  {
    path: '',
    canActivate: [requiereAdmin],
    loadComponent: () => import('./admin').then((m) => m.Admin),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'metricas' },
      {
        path: 'metricas',
        title: 'titulos.adminMetricas',
        loadComponent: () => import('./metricas').then((m) => m.Metricas),
      },
      {
        path: 'costos',
        title: 'titulos.adminCostos',
        loadComponent: () => import('./costos').then((m) => m.Costos),
      },
      {
        path: 'docentes',
        title: 'titulos.adminDocentes',
        loadComponent: () => import('./docentes').then((m) => m.Docentes),
      },
      {
        path: 'errores',
        title: 'titulos.adminErrores',
        loadComponent: () => import('./errores').then((m) => m.Errores),
      },
      {
        path: 'tipos-rubro',
        title: 'titulos.adminTiposRubro',
        loadComponent: () => import('./tipos-rubro').then((m) => m.TiposRubro),
      },
      {
        path: 'auditoria',
        title: 'titulos.adminAuditoria',
        loadComponent: () => import('./auditoria').then((m) => m.Auditoria),
      },
    ],
  },
];
