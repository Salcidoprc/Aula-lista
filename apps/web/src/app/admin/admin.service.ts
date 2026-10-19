import { Injectable, inject } from '@angular/core';
import { Api } from '../api/api';
import { auditoriaAdmin } from '../api/fn/administracion/auditoria-admin';
import { comportamientosAdmin } from '../api/fn/administracion/comportamientos-admin';
import { costosAdmin } from '../api/fn/administracion/costos-admin';
import { crearTipoRubro } from '../api/fn/administracion/crear-tipo-rubro';
import { docentesAdmin } from '../api/fn/administracion/docentes-admin';
import { editarTipoRubro } from '../api/fn/administracion/editar-tipo-rubro';
import { erroresAdmin } from '../api/fn/administracion/errores-admin';
import { metricasAdmin } from '../api/fn/administracion/metricas-admin';
import { tiposRubroAdmin } from '../api/fn/administracion/tipos-rubro-admin';
import { CostosDto } from '../api/models/costos-dto';
import { DocentesAdminDto } from '../api/models/docentes-admin-dto';
import { ErrorTrabajoDto } from '../api/models/error-trabajo-dto';
import { MetricasDto } from '../api/models/metricas-dto';
import { PaginaAuditoriaDto } from '../api/models/pagina-auditoria-dto';
import { TipoRubroAdminDto } from '../api/models/tipo-rubro-admin-dto';
import { TipoRubroEntrada } from '../api/models/tipo-rubro-entrada';

/**
 * SPEC-09: lecturas del panel y el catálogo de tipos de rubro. Para quien no es administrador todas responden 404,
 * como si el panel no existiera.
 */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly api = inject(Api);

  metricas(): Promise<MetricasDto> {
    return this.api.invoke(metricasAdmin);
  }

  docentes(): Promise<DocentesAdminDto> {
    return this.api.invoke(docentesAdmin);
  }

  costos(): Promise<CostosDto> {
    return this.api.invoke(costosAdmin);
  }

  errores(): Promise<ErrorTrabajoDto[]> {
    return this.api.invoke(erroresAdmin);
  }

  auditoria(antesDe: string | null): Promise<PaginaAuditoriaDto> {
    return this.api.invoke(auditoriaAdmin, antesDe ? { antesDe } : undefined);
  }

  comportamientos(): Promise<string[]> {
    return this.api.invoke(comportamientosAdmin);
  }

  tipos(): Promise<TipoRubroAdminDto[]> {
    return this.api.invoke(tiposRubroAdmin);
  }

  crearTipo(tipo: TipoRubroEntrada): Promise<TipoRubroAdminDto> {
    return this.api.invoke(crearTipoRubro, { body: tipo });
  }

  editarTipo(id: string, tipo: TipoRubroEntrada): Promise<TipoRubroAdminDto> {
    return this.api.invoke(editarTipoRubro, { id, body: tipo });
  }
}
