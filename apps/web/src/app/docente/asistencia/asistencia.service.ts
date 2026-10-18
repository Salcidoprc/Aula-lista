import { Injectable, inject } from '@angular/core';
import { Api } from '../../api/api';
import { borrarAsistencia } from '../../api/fn/asistencia/borrar-asistencia';
import { listarAsistencias } from '../../api/fn/asistencia/listar-asistencias';
import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { EstadoAlumno } from '../../api/models/estado-alumno';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { TomaAsistenciaDto } from '../../api/models/toma-asistencia-dto';
import { AsistenciaEnCola } from '../../core/sincronizacion/almacen-local';
import {
  AsistenciaEnviada,
  ColaSincronizacion,
} from '../../core/sincronizacion/cola-sincronizacion';
import { EvaluacionService } from '../evaluacion/evaluacion.service';

/**
 * Pasar lista (SPEC-10). Leer guarda una copia en el dispositivo y marcar pasa por la cola sin conexión (RF-103,
 * reglas/02): no hay botón "Guardar". Borrar la lista de un día es solo en línea.
 */
@Injectable({ providedIn: 'root' })
export class AsistenciaService {
  private readonly api = inject(Api);
  private readonly cola = inject(ColaSincronizacion);
  private readonly evaluacion = inject(EvaluacionService);

  grupo(id: string) {
    return this.evaluacion.grupo(id);
  }

  periodos(grupoId: string) {
    return this.evaluacion.periodos(grupoId);
  }

  /** Número, nombre y si sigue activo; sin tutores, para que la copia no guarde sus datos de contacto. */
  alumnos(grupoId: string): Promise<AlumnoMatrizDto[]> {
    return this.cola.conCopia(`alumnos:${grupoId}`, () => this.evaluacion.alumnos(grupoId));
  }

  /** Las tomas de la unidad (RF-101). */
  tomas(grupoId: string, unidad: PeriodoDto): Promise<TomaAsistenciaDto[]> {
    return this.cola.conCopia(`tomas:${grupoId}:${unidad.id}`, () =>
      this.api.invoke(listarAsistencias, { id: grupoId, desde: unidad.inicio, hasta: unidad.fin }),
    );
  }

  pendientes(grupoId: string): Promise<readonly AsistenciaEnCola[]> {
    return this.cola.asistenciasPendientes(grupoId);
  }

  marcar(
    grupoId: string,
    fecha: string,
    alumnos: readonly EstadoAlumno[],
    id?: string,
  ): Promise<AsistenciaEnCola> {
    return this.cola.guardarAsistencia(grupoId, fecha, alumnos, id);
  }

  alEnviar(oyente: (enviada: AsistenciaEnviada) => void): () => void {
    return this.cola.alEnviarAsistencia(oyente);
  }

  borrar(grupoId: string, fecha: string): Promise<void> {
    return this.api.invoke(borrarAsistencia, { id: grupoId, fecha });
  }
}
