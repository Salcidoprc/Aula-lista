import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Api } from '../../api/api';
import { borrarActividad } from '../../api/fn/actividades/borrar-actividad';
import { crearActividad } from '../../api/fn/actividades/crear-actividad';
import { editarActividad } from '../../api/fn/actividades/editar-actividad';
import { obtenerMatrizCalificaciones } from '../../api/fn/calificaciones/obtener-matriz-calificaciones';
import { listarPeriodos } from '../../api/fn/grupos/listar-periodos';
import { copiarRubros } from '../../api/fn/rubros/copiar-rubros';
import { listarTiposRubro } from '../../api/fn/rubros/listar-tipos-rubro';
import { obtenerRubros } from '../../api/fn/rubros/obtener-rubros';
import { reemplazarRubros } from '../../api/fn/rubros/reemplazar-rubros';
import { ActividadDto } from '../../api/models/actividad-dto';
import { CambioCelda } from '../../api/models/cambio-celda';
import { DatosActividad } from '../../api/models/datos-actividad';
import { GrupoDto } from '../../api/models/grupo-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { RubroEntrada } from '../../api/models/rubro-entrada';
import { RubrosDelPeriodoDto } from '../../api/models/rubros-del-periodo-dto';
import { TipoRubroDto } from '../../api/models/tipo-rubro-dto';
import { ColaSincronizacion } from '../../core/sincronizacion/cola-sincronizacion';
import { GruposService } from '../grupos/grupos.service';

/**
 * Rubros, actividades y la matriz del periodo (SPEC-03). La captura no llama a la API: pasa por la cola sin
 * conexión (reglas/02). El grupo, sus periodos y la matriz se guardan en el dispositivo al leerlos.
 */
@Injectable({ providedIn: 'root' })
export class EvaluacionService {
  private readonly api = inject(Api);
  private readonly cola = inject(ColaSincronizacion);
  private readonly grupos = inject(GruposService);
  private tipos: Promise<TipoRubroDto[]> | null = null;

  grupo(id: string): Promise<GrupoDto> {
    return this.cola.conCopia(`grupo:${id}`, () => this.grupos.obtener(id));
  }

  periodos(grupoId: string): Promise<PeriodoDto[]> {
    return this.cola.conCopia(`periodos:${grupoId}`, () =>
      this.api.invoke(listarPeriodos, { id: grupoId }),
    );
  }

  tiposRubro(): Promise<TipoRubroDto[]> {
    this.tipos ??= this.cola
      .conCopia('tipos-rubro', () => this.api.invoke(listarTiposRubro))
      .catch((e: unknown) => {
        this.tipos = null;
        throw e;
      });
    return this.tipos;
  }

  /** La matriz del servidor con lo que este dispositivo aún no envía; sin conexión, la última copia. */
  async matriz(periodoId: string): Promise<MatrizCalificacionesDto> {
    try {
      const matriz = await this.api.invoke(obtenerMatrizCalificaciones, { id: periodoId });
      return await this.cola.conPendientes(matriz);
    } catch (error) {
      const copia =
        error instanceof HttpErrorResponse && error.status === 0
          ? await this.cola.matrizGuardada(periodoId).catch(() => null)
          : null;
      if (!copia) {
        throw error;
      }
      return copia;
    }
  }

  /** Captura: primero en el dispositivo, luego la cola la envía (SPEC-03: no hay botón "Guardar"). */
  capturar(matriz: MatrizCalificacionesDto, cambios: readonly CambioCelda[]): Promise<void> {
    return this.cola.guardar(matriz, cambios);
  }

  rubros(periodoId: string): Promise<RubrosDelPeriodoDto> {
    return this.api.invoke(obtenerRubros, { id: periodoId });
  }

  reemplazarRubros(
    periodoId: string,
    rubros: RubroEntrada[],
    confirmarBorrado: boolean,
  ): Promise<RubrosDelPeriodoDto> {
    return this.api.invoke(reemplazarRubros, { id: periodoId, body: { rubros, confirmarBorrado } });
  }

  copiarRubros(periodoId: string, desdePeriodoId: string): Promise<RubrosDelPeriodoDto> {
    return this.api.invoke(copiarRubros, { id: periodoId, body: { desdePeriodoId } });
  }

  crearActividad(rubroId: string, datos: DatosActividad): Promise<ActividadDto> {
    return this.api.invoke(crearActividad, { id: rubroId, body: datos });
  }

  editarActividad(id: string, datos: DatosActividad): Promise<ActividadDto> {
    return this.api.invoke(editarActividad, { id, body: datos });
  }

  borrarActividad(id: string, confirmar: boolean): Promise<void> {
    return this.api.invoke(borrarActividad, { id, confirmar });
  }
}
