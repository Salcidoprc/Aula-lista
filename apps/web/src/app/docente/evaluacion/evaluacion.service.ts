import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Api } from '../../api/api';
import { borrarActividad } from '../../api/fn/actividades/borrar-actividad';
import { crearActividad } from '../../api/fn/actividades/crear-actividad';
import { editarActividad } from '../../api/fn/actividades/editar-actividad';
import { obtenerMatrizCalificaciones } from '../../api/fn/calificaciones/obtener-matriz-calificaciones';
import { listarEquipos } from '../../api/fn/equipos/listar-equipos';
import { reemplazarEquipos } from '../../api/fn/equipos/reemplazar-equipos';
import { listarAlumnos } from '../../api/fn/grupos/listar-alumnos';
import { listarPeriodos } from '../../api/fn/grupos/listar-periodos';
import { listarNotas } from '../../api/fn/notas/listar-notas';
import { copiarRubros } from '../../api/fn/rubros/copiar-rubros';
import { listarTiposRubro } from '../../api/fn/rubros/listar-tipos-rubro';
import { obtenerRubros } from '../../api/fn/rubros/obtener-rubros';
import { reemplazarRubros } from '../../api/fn/rubros/reemplazar-rubros';
import { ActividadDto } from '../../api/models/actividad-dto';
import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { CambioCelda } from '../../api/models/cambio-celda';
import { CrearNota } from '../../api/models/crear-nota';
import { DatosActividad } from '../../api/models/datos-actividad';
import { DatosEquipo } from '../../api/models/datos-equipo';
import { EquipoDto } from '../../api/models/equipo-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { NotaDto } from '../../api/models/nota-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { RubroEntrada } from '../../api/models/rubro-entrada';
import { RubrosDelPeriodoDto } from '../../api/models/rubros-del-periodo-dto';
import { TipoRubroDto } from '../../api/models/tipo-rubro-dto';
import { ColaSincronizacion, VistaCaptura } from '../../core/sincronizacion/cola-sincronizacion';
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
  capturar(
    matriz: MatrizCalificacionesDto,
    cambios: readonly CambioCelda[],
    vista: VistaCaptura,
  ): Promise<void> {
    return this.cola.guardar(matriz, cambios, vista);
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

  /** Los alumnos del grupo como los muestra la matriz. */
  async alumnos(grupoId: string): Promise<AlumnoMatrizDto[]> {
    const lista = await this.api.invoke(listarAlumnos, { id: grupoId });
    return lista.map((a) => ({
      id: a.id,
      nombres: a.nombres,
      apellidos: a.apellidos,
      numeroLista: a.numeroLista,
      dadoDeBaja: a.bajaEn !== null,
    }));
  }

  equipos(grupoId: string): Promise<EquipoDto[]> {
    return this.api.invoke(listarEquipos, { id: grupoId });
  }

  /** Reemplaza todos los equipos del grupo (RN-114). Solo en línea. */
  reemplazarEquipos(grupoId: string, equipos: DatosEquipo[]): Promise<EquipoDto[]> {
    return this.api.invoke(reemplazarEquipos, { id: grupoId, body: { equipos } });
  }

  /** Las notas del servidor; sin conexión, ninguna (las pendientes las da la cola). */
  notas(alumnoId: string): Promise<NotaDto[]> {
    return this.api.invoke(listarNotas, { id: alumnoId });
  }

  /** La nota queda en el dispositivo y la cola la envía (RF-037). */
  crearNota(alumnoId: string, nota: CrearNota): Promise<void> {
    return this.cola.guardarNota(alumnoId, nota);
  }

  notasPendientes(alumnoId: string) {
    return this.cola.notasPendientes(alumnoId);
  }
}
