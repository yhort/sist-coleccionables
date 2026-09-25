import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { EcosistemaApiService } from '../../data-access/ecosistema.service';
import { ETIQUETAS_ROL, ROLES_USUARIO, RolUsuario, UsuarioEmpresa } from '../../models/ecosistema.model';

@Component({
  selector: 'app-usuario-form-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './usuario-form-dialog.component.html',
  styleUrl: './usuario-form-dialog.component.scss',
})
export class UsuarioFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(EcosistemaApiService);

  readonly usuario = input<UsuarioEmpresa | null>(null);
  readonly saved = output<void>();
  readonly cancelled = output<void>();
  readonly error = signal('');
  readonly enviando = signal(false);
  readonly roles = ROLES_USUARIO;
  readonly etiquetas = ETIQUETAS_ROL;

  readonly form = this.fb.nonNullable.group({
    dni: ['', [Validators.required, Validators.pattern(/^\d{8}$/)]],
    nombres: ['', [Validators.required, Validators.minLength(2)]],
    apellidos: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: [''],
    rol: this.fb.nonNullable.control<RolUsuario>('CAJERO'),
    activo: this.fb.nonNullable.control<'Activo' | 'Inactivo'>('Activo'),
  });

  get esEdicion(): boolean {
    return !!this.usuario();
  }

  ngOnInit(): void {
    const actual = this.usuario();
    if (actual) {
      this.form.patchValue({
        dni: actual.dni,
        nombres: actual.nombres,
        apellidos: actual.apellidos,
        email: actual.email,
        password: '',
        rol: actual.rol,
        activo: actual.activo ? 'Activo' : 'Inactivo',
      });
      this.form.controls.password.clearValidators();
    } else {
      this.form.controls.password.setValidators([
        Validators.required,
        Validators.minLength(8),
      ]);
    }
    this.form.controls.password.updateValueAndValidity();
  }

  async guardar(): Promise<void> {
    this.error.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Completa los campos obligatorios de identidad y acceso.');
      return;
    }
    this.enviando.set(true);
    try {
      const raw = this.form.getRawValue();
      await this.api.guardarUsuario(this.usuario()?.id ?? null, {
        dni: raw.dni,
        nombres: raw.nombres,
        apellidos: raw.apellidos,
        email: raw.email,
        password: raw.password,
        rol: raw.rol,
        activo: raw.activo === 'Activo',
      });
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar el usuario.');
    } finally {
      this.enviando.set(false);
    }
  }
}
