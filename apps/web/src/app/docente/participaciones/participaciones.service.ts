import { Injectable, computed, inject } from '@angular/core';
import { Api } from '../../api/api';
import { editarGrupo } from '../../api/fn/grupos/editar-grupo';
import { listarParticipaciones } from '../../api/fn/participaciones/listar-participaciones';
import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { ParticipacionDto } from '../../api/models/participacion-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { ParticipacionEnCola } from '../../core/sincronizacion/almacen-local';
import {
  ColaSincronizacion,
  ParticipacionEnviada,
} from '../../core/sincronizacion/cola-sincronizacion';
import { EvaluacionService } from '../evaluacion/evaluacion.service';
import { GruposService } from '../grupos/grupos.service';

/**
 * Participaciones extra (SPEC-03, RF-110, RF-111). Leer guarda una copia en el dispositivo; dar y quitar pasan por la
 * cola sin conexión, sin botón "Guardar". Cambiar lo que vale una es solo en línea.
 */
@Injectable({ providedIn: 'root' })
export class ParticipacionesService {
  private readonly api = inject(Api);
  private readonly cola = inject(ColaSincronizacion);
  private readonly evaluacion = inject(EvaluacionService);
  private readonly grupos = inject(GruposService);
  readonly enLinea = computed(() => this.cola.estado() !== 'sin_conexion');

  grupo(id: string): Promise<GrupoDto> {
    return this.evaluacion.grupo(id);
  }

  periodos(grupoId: string): Promise<PeriodoDto[]> {
    return this.evaluacion.periodos(grupoId);
  }

  /** Número, nombre y si sigue activo; la misma copia que usa pasar lista. */
  alumnos(grupoId: string): Promise<AlumnoMatrizDto[]> {
    return this.cola.conCopia(`alumnos:${grupoId}`, () => this.evaluacion.alumnos(grupoId));
  }

  /** Las participaciones de la unidad. */
  participaciones(grupoId: string, unidad: PeriodoDto): Promise<ParticipacionDto[]> {
    return this.cola.conCopia(`participaciones:${grupoId}:${unidad.id}`, () =>
      this.api.invoke(listarParticipaciones, {
        id: grupoId,
        desde: unidad.inicio,
        hasta: unidad.fin,
      }),
    );
  }

  pendientes(grupoId: string): Promise<readonly ParticipacionEnCola[]> {
    return this.cola.participacionesPendientes(grupoId);
  }

  dar(grupoId: string, alumnoId: string, fecha: string, id?: string): Promise<ParticipacionEnCola> {
    return this.cola.darParticipacion(grupoId, alumnoId, fecha, id);
  }

  quitar(participacion: {
    id: string;
    grupoId: string;
    alumnoId: string;
    fecha: string;
  }): Promise<void> {
    return this.cola.quitarParticipacion(participacion);
  }

  alEnviar(oyente: (enviada: ParticipacionEnviada) => void): () => void {
    return this.cola.alEnviarParticipacion(oyente);
  }

  /** RN-121: cambia solo el valor; lo demás del grupo viaja como está. */
  async cambiarValor(grupo: GrupoDto, valor: string): Promise<GrupoDto> {
    const nuevo = await this.api.invoke(editarGrupo, {
      id: grupo.id,
      body: {
        nombre: grupo.nombre,
        grado: Number(grupo.grado),
        materia: grupo.materia ?? null,
        califMinima: grupo.califMinima,
        califAprobatoria: grupo.califAprobatoria,
        decimalesFinal: Number(grupo.decimalesFinal),
        redondeo: grupo.redondeo,
        valorRetardo: grupo.valorRetardo,
        valorParticipacion: valor,
      },
    });
    this.grupos.recordar(nuevo);
    return nuevo;
  }
}
