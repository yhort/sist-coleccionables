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
  readonly roles = ROLES_USUARIO;
  readonly etiquetas = ETIQUETAS_ROL;

  readonly form = this.fb.nonNullable.group({
    nombre: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    rol: this.fb.nonNullable.control<RolUsuario>('CAJERO'),
    activo: [true],
  });

  ngOnInit(): void {
    const actual = this.usuario();
    if (actual) {
      this.form.patchValue(actual);
    }
  }

  guardar(): void {
    this.error.set('');
    try {
      this.api.guardarUsuario(this.usuario()?.id ?? null, this.form.getRawValue());
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar el usuario.');
    }
  }
}
