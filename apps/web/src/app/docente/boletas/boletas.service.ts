import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Api } from '../../api/api';
import { corregirBoleta } from '../../api/fn/boletas/corregir-boleta';
import { generarBoletas } from '../../api/fn/boletas/generar-boletas';
import { listarBoletas } from '../../api/fn/boletas/listar-boletas';
import { publicarBoletas } from '../../api/fn/boletas/publicar-boletas';
import { cerrarPeriodo } from '../../api/fn/cierre/cerrar-periodo';
import { reabrirPeriodo } from '../../api/fn/cierre/reabrir-periodo';
import { BoletasDto } from '../../api/models/boletas-dto';
import { PublicacionDto } from '../../api/models/publicacion-dto';
import { ColaSincronizacion } from '../../core/sincronizacion/cola-sincronizacion';
import { descargarArchivo } from '../../shared/descarga';

/** SPEC-05: boletas, versiones, PDF y Excel. Todo es en línea, como el cierre. */
@Injectable({ providedIn: 'root' })
export class BoletasService {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly cola = inject(ColaSincronizacion);
  readonly enLinea = computed(() => this.cola.estado() !== 'sin_conexion');

  boletas(periodoId: string): Promise<BoletasDto> {
    return this.api.invoke(listarBoletas, { id: periodoId });
  }

  /** Sin alumnos, todas las que faltan o están desactualizadas (RF-050). */
  generar(periodoId: string, alumnoIds?: string[]): Promise<BoletasDto> {
    return this.api.invoke(generarBoletas, {
      id: periodoId,
      body: { alumnoIds: alumnoIds ?? null },
    });
  }

  /** Sin alumnos, todas las listas (RF-052); avisa a los tutores y dice a quién compartírsela a mano (RN-054). */
  publicar(periodoId: string, alumnoIds?: string[]): Promise<PublicacionDto> {
    return this.api.invoke(publicarBoletas, {
      id: periodoId,
      body: { alumnoIds: alumnoIds ?? null },
    });
  }

  corregir(boletaId: string): Promise<BoletasDto> {
    return this.api.invoke(corregirBoleta, { id: boletaId });
  }

  async cerrar(periodoId: string): Promise<void> {
    await this.api.invoke(cerrarPeriodo, { id: periodoId });
  }

  async reabrir(periodoId: string): Promise<void> {
    await this.api.invoke(reabrirPeriodo, { id: periodoId });
  }

  /** El PDF de un alumno, como objeto local para abrirlo en el visor del navegador. */
  async pdf(boletaId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`/api/v1/boletas/${boletaId}/pdf`, { responseType: 'blob' }),
    );
  }

  descargarGrupo(periodoId: string): Promise<void> {
    return descargarArchivo(this.http, `/api/v1/periodos/${periodoId}/boletas/pdf`, 'boletas.pdf');
  }

  exportar(periodoId: string): Promise<void> {
    return descargarArchivo(
      this.http,
      `/api/v1/periodos/${periodoId}/exportacion.xlsx`,
      'calificaciones.xlsx',
    );
  }
}
