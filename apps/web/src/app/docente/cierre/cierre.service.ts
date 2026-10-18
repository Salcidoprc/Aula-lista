import { Injectable, computed, inject } from '@angular/core';
import { Api } from '../../api/api';
import { aprobarObservacion } from '../../api/fn/cierre/aprobar-observacion';
import { editarObservacion } from '../../api/fn/cierre/editar-observacion';
import { generarObservaciones } from '../../api/fn/cierre/generar-observaciones';
import { guardarEstiloObservaciones } from '../../api/fn/cierre/guardar-estilo-observaciones';
import { iniciarCierre } from '../../api/fn/cierre/iniciar-cierre';
import { listarObservaciones } from '../../api/fn/cierre/listar-observaciones';
import { obtenerCierre } from '../../api/fn/cierre/obtener-cierre';
import { obtenerEstiloObservaciones } from '../../api/fn/cierre/obtener-estilo-observaciones';
import { reabrirObservacion } from '../../api/fn/cierre/reabrir-observacion';
import { regenerarObservacion } from '../../api/fn/cierre/regenerar-observacion';
import { CierreDto } from '../../api/models/cierre-dto';
import { EstiloDto } from '../../api/models/estilo-dto';
import { GuardarEstilo } from '../../api/models/guardar-estilo';
import { ObservacionDto } from '../../api/models/observacion-dto';
import { ColaSincronizacion } from '../../core/sincronizacion/cola-sincronizacion';

/**
 * SPEC-04: cierre del periodo y observaciones. Todo es en línea: generar y aprobar no se guardan en el
 * dispositivo (02-arquitectura, "Lo que no funciona sin conexión").
 */
@Injectable({ providedIn: 'root' })
export class CierreService {
  private readonly api = inject(Api);
  private readonly cola = inject(ColaSincronizacion);
  readonly enLinea = computed(() => this.cola.estado() !== 'sin_conexion');

  cierre(periodoId: string): Promise<CierreDto> {
    return this.api.invoke(obtenerCierre, { id: periodoId });
  }

  /** En un grupo Gratis, `usarPrueba` activa la prueba Pro del docente en este grupo (RN-081). */
  iniciar(periodoId: string, usarPrueba = false): Promise<CierreDto> {
    return this.api.invoke(iniciarCierre, { id: periodoId, body: { usarPrueba } });
  }

  /** Sin alumnos, todos los que no tienen texto (RF-041). */
  generar(periodoId: string, alumnoIds?: string[]): Promise<CierreDto> {
    return this.api.invoke(generarObservaciones, {
      id: periodoId,
      body: { alumnoIds: alumnoIds ?? null },
    });
  }

  observaciones(periodoId: string): Promise<ObservacionDto[]> {
    return this.api.invoke(listarObservaciones, { id: periodoId });
  }

  editar(id: string, texto: string): Promise<ObservacionDto> {
    return this.api.invoke(editarObservacion, { id, body: { texto } });
  }

  regenerar(id: string, instruccion: string | null): Promise<ObservacionDto> {
    return this.api.invoke(regenerarObservacion, { id, body: { instruccion } });
  }

  aprobar(id: string, texto: string | null): Promise<ObservacionDto> {
    return this.api.invoke(aprobarObservacion, { id, body: { texto } });
  }

  reabrir(id: string): Promise<ObservacionDto> {
    return this.api.invoke(reabrirObservacion, { id });
  }

  estilo(): Promise<EstiloDto> {
    return this.api.invoke(obtenerEstiloObservaciones);
  }

  guardarEstilo(estilo: GuardarEstilo): Promise<EstiloDto> {
    return this.api.invoke(guardarEstiloObservaciones, { body: estilo });
  }
}
