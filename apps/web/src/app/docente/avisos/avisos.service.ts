import { Injectable, computed, inject } from '@angular/core';
import { Api } from '../../api/api';
import { borrarAviso } from '../../api/fn/avisos/borrar-aviso';
import { compartirAviso } from '../../api/fn/avisos/compartir-aviso';
import { crearAviso } from '../../api/fn/avisos/crear-aviso';
import { listarAvisos } from '../../api/fn/avisos/listar-avisos';
import { listarDestinatariosAviso } from '../../api/fn/avisos/listar-destinatarios-aviso';
import { obtenerAviso } from '../../api/fn/avisos/obtener-aviso';
import { compartirBoleta } from '../../api/fn/boletas/compartir-boleta';
import { AvisoDto } from '../../api/models/aviso-dto';
import { AvisosDto } from '../../api/models/avisos-dto';
import { CrearAviso } from '../../api/models/crear-aviso';
import { DestinatariosDto } from '../../api/models/destinatarios-dto';
import { EnlaceDto } from '../../api/models/enlace-dto';
import { ColaSincronizacion } from '../../core/sincronizacion/cola-sincronizacion';
import { EvaluacionService } from '../evaluacion/evaluacion.service';

/** SPEC-07: avisos del docente a los tutores. Todo es en línea: un aviso no se encola (sale a muchos). */
@Injectable({ providedIn: 'root' })
export class AvisosGrupoService {
  private readonly api = inject(Api);
  private readonly cola = inject(ColaSincronizacion);
  private readonly evaluacion = inject(EvaluacionService);
  readonly enLinea = computed(() => this.cola.estado() !== 'sin_conexion');

  grupo(id: string) {
    return this.evaluacion.grupo(id);
  }

  avisos(grupoId: string): Promise<AvisosDto> {
    return this.api.invoke(listarAvisos, { id: grupoId });
  }

  destinatarios(grupoId: string): Promise<DestinatariosDto> {
    return this.api.invoke(listarDestinatariosAviso, { id: grupoId });
  }

  crear(grupoId: string, aviso: CrearAviso): Promise<AvisoDto> {
    return this.api.invoke(crearAviso, { id: grupoId, body: aviso });
  }

  aviso(id: string): Promise<AvisoDto> {
    return this.api.invoke(obtenerAviso, { id });
  }

  borrar(id: string): Promise<void> {
    return this.api.invoke(borrarAviso, { id });
  }

  /** Un enlace personal nuevo del tutor que abre el aviso; la entrega queda "Compartido" (RN-073). */
  compartirAviso(avisoId: string, tutorId: string): Promise<EnlaceDto> {
    return this.api.invoke(compartirAviso, { id: avisoId, tutorId });
  }

  /** Lo mismo para una boleta publicada a un tutor sin ningún canal (RN-054). */
  compartirBoleta(boletaId: string, tutorId: string): Promise<EnlaceDto> {
    return this.api.invoke(compartirBoleta, { id: boletaId, tutorId });
  }
}
