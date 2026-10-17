import { Injectable, inject, signal } from '@angular/core';
import { Api } from '../../api/api';
import { listarCiclos } from '../../api/fn/ciclos/listar-ciclos';
import { listarGrupos } from '../../api/fn/grupos/listar-grupos';
import { obtenerGrupo } from '../../api/fn/grupos/obtener-grupo';
import { CicloDto } from '../../api/models/ciclo-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { DatosPortada } from '../../shared/portada/portada.service';
import { claveGrado, nombreGrupo } from '../../shared/grados';

/**
 * Grupos y ciclos del docente. Guarda lo último que leyó para pintar la portada del grupo sin esperar
 * a la red al entrar desde la lista; cada pantalla vuelve a pedir lo suyo.
 */
@Injectable({ providedIn: 'root' })
export class GruposService {
  private readonly api = inject(Api);
  private readonly porId = new Map<string, GrupoDto>();
  private readonly listaCiclos = signal<readonly CicloDto[] | null>(null);

  readonly ciclos = this.listaCiclos.asReadonly();

  async listar(archivados = false): Promise<GrupoDto[]> {
    const grupos = await this.api.invoke(listarGrupos, { archivados });
    for (const g of grupos) {
      this.porId.set(g.id, g);
    }
    return grupos;
  }

  async cargarCiclos(): Promise<readonly CicloDto[]> {
    const ciclos = await this.api.invoke(listarCiclos);
    this.listaCiclos.set(ciclos);
    return ciclos;
  }

  enCache(id: string): GrupoDto | undefined {
    return this.porId.get(id);
  }

  async obtener(id: string): Promise<GrupoDto> {
    const grupo = await this.api.invoke(obtenerGrupo, { id });
    this.recordar(grupo);
    return grupo;
  }

  recordar(grupo: GrupoDto): void {
    this.porId.set(grupo.id, grupo);
  }

  olvidar(id: string): void {
    this.porId.delete(id);
  }
}

/** Portada de un grupo: "4° B" con su color y su número; la materia y el ciclo abajo. */
export function portadaDeGrupo(grupo: GrupoDto): DatosPortada {
  return {
    titulo: nombreGrupo(grupo.grado, grupo.nombre),
    subtitulo: [grupo.materia, grupo.cicloNombre].filter(Boolean).join(' · '),
    numero: String(Number(grupo.grado)),
    grado: claveGrado(grupo.nivel, grupo.grado),
  };
}
