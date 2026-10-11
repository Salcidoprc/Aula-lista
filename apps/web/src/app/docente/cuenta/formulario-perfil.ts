import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { ActualizarPerfil } from '../../api/models/actualizar-perfil';
import { PerfilDocente } from '../../api/models/perfil-docente';

/** Claves que acepta la API (EntidadesFederativas del dominio); el nombre visible viene de i18n. */
export const ENTIDADES = [
  'aguascalientes',
  'baja_california',
  'baja_california_sur',
  'campeche',
  'chiapas',
  'chihuahua',
  'ciudad_de_mexico',
  'coahuila',
  'colima',
  'durango',
  'estado_de_mexico',
  'guanajuato',
  'guerrero',
  'hidalgo',
  'jalisco',
  'michoacan',
  'morelos',
  'nayarit',
  'nuevo_leon',
  'oaxaca',
  'puebla',
  'queretaro',
  'quintana_roo',
  'san_luis_potosi',
  'sinaloa',
  'sonora',
  'tabasco',
  'tamaulipas',
  'tlaxcala',
  'veracruz',
  'yucatan',
  'zacatecas',
] as const;

/** CCT: 2 dígitos de entidad, 3 letras, 4 dígitos y una letra (ej. 25DPR0123X). */
const PATRON_CCT = /^\d{2}[A-Za-z]{3}\d{4}[A-Za-z]$/;

type Campo = 'nombre' | 'nivel' | 'escuelaNombre' | 'escuelaCct' | 'entidadFederativa';

/** Datos del perfil (RF-004), compartido por la bienvenida y los ajustes. */
@Component({
  selector: 'app-formulario-perfil',
  imports: [ReactiveFormsModule, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form
      class="formulario"
      [formGroup]="formulario"
      (ngSubmit)="enviar()"
      novalidate
      *transloco="let t"
    >
      <div class="campo">
        <label for="nombre">{{ t('cuenta.perfil.nombre') }}</label>
        <input
          id="nombre"
          formControlName="nombre"
          autocomplete="name"
          maxlength="120"
          [attr.aria-invalid]="invalido('nombre')"
          [attr.aria-describedby]="invalido('nombre') ? 'nombre-error' : null"
        />
        @if (invalido('nombre')) {
          <p id="nombre-error" class="error">{{ t('cuenta.validacion.requerida') }}</p>
        }
      </div>

      <fieldset
        class="eleccion"
        [attr.aria-invalid]="invalido('nivel')"
        [attr.aria-describedby]="invalido('nivel') ? 'nivel-error' : null"
      >
        <legend>{{ t('cuenta.perfil.nivel') }}</legend>
        <label
          ><input type="radio" formControlName="nivel" value="primaria" />{{
            t('cuenta.perfil.primaria')
          }}</label
        >
        <label
          ><input type="radio" formControlName="nivel" value="secundaria" />{{
            t('cuenta.perfil.secundaria')
          }}</label
        >
      </fieldset>
      @if (invalido('nivel')) {
        <p id="nivel-error" class="error">{{ t('cuenta.validacion.elige') }}</p>
      }

      <div class="campo">
        <label for="escuela">{{ t('cuenta.perfil.escuela') }}</label>
        <input
          id="escuela"
          formControlName="escuelaNombre"
          autocomplete="organization"
          maxlength="160"
          [attr.aria-invalid]="invalido('escuelaNombre')"
          [attr.aria-describedby]="invalido('escuelaNombre') ? 'escuela-error' : null"
        />
        @if (invalido('escuelaNombre')) {
          <p id="escuela-error" class="error">{{ t('cuenta.validacion.requerida') }}</p>
        }
      </div>

      <div class="campo">
        <label for="cct">{{ t('cuenta.perfil.cct') }}</label>
        <input
          id="cct"
          formControlName="escuelaCct"
          autocapitalize="characters"
          autocomplete="off"
          maxlength="10"
          spellcheck="false"
          [attr.aria-invalid]="invalido('escuelaCct')"
          [attr.aria-describedby]="invalido('escuelaCct') ? 'cct-error cct-ayuda' : 'cct-ayuda'"
        />
        <p id="cct-ayuda" class="ayuda">{{ t('cuenta.perfil.cctAyuda') }}</p>
        @if (invalido('escuelaCct')) {
          <p id="cct-error" class="error">{{ t('errores.cct_invalida') }}</p>
        }
      </div>

      <div class="campo">
        <label for="entidad">{{ t('cuenta.perfil.entidad') }}</label>
        <span class="selector">
          <select
            id="entidad"
            formControlName="entidadFederativa"
            [attr.aria-invalid]="invalido('entidadFederativa')"
            [attr.aria-describedby]="invalido('entidadFederativa') ? 'entidad-error' : null"
          >
            <option value="" disabled>{{ t('cuenta.perfil.eligeEntidad') }}</option>
            @for (clave of entidades; track clave) {
              <option [value]="clave">{{ t('entidades.' + clave) }}</option>
            }
          </select>
        </span>
        @if (invalido('entidadFederativa')) {
          <p id="entidad-error" class="error">{{ t('errores.entidad_federativa_invalida') }}</p>
        }
      </div>

      <ng-content />

      <button class="boton boton--ancho" type="submit" [disabled]="enviando()">
        {{ accion() }}
      </button>
    </form>
  `,
})
export class FormularioPerfil {
  readonly perfil = input<PerfilDocente | null>(null);
  readonly accion = input.required<string>();
  readonly enviando = input(false);
  /** Validación adicional del contenedor (por ejemplo, la casilla legal de la bienvenida). */
  readonly valido = input(true);
  readonly guardar = output<ActualizarPerfil>();

  protected readonly entidades = ENTIDADES;
  protected readonly formulario = inject(FormBuilder).nonNullable.group({
    nombre: ['', [Validators.required, Validators.maxLength(120)]],
    nivel: ['', Validators.required],
    escuelaNombre: ['', [Validators.required, Validators.maxLength(160)]],
    escuelaCct: ['', Validators.pattern(PATRON_CCT)],
    entidadFederativa: ['', Validators.required],
  });
  protected readonly intentado = signal(false);

  constructor() {
    effect(() => {
      const p = this.perfil();
      if (p) {
        this.formulario.reset({
          nombre: p.nombre ?? '',
          nivel: p.nivel ?? '',
          escuelaNombre: p.escuelaNombre ?? '',
          escuelaCct: p.escuelaCct ?? '',
          entidadFederativa: p.entidadFederativa ?? '',
        });
      }
    });
  }

  protected invalido(campo: Campo): boolean {
    return this.intentado() && this.formulario.controls[campo].invalid;
  }

  protected enviar(): void {
    this.intentado.set(true);
    if (this.formulario.invalid || !this.valido()) {
      return;
    }
    const v = this.formulario.getRawValue();
    const cct = v.escuelaCct.trim().toUpperCase();
    this.guardar.emit({
      nombre: v.nombre.trim(),
      nivel: v.nivel,
      escuelaNombre: v.escuelaNombre.trim(),
      escuelaCct: cct === '' ? null : cct,
      entidadFederativa: v.entidadFederativa,
    });
  }
}
