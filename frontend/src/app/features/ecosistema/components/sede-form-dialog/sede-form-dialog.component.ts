import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { EcosistemaApiService } from '../../data-access/ecosistema.service';
import { SedeEmpresa, TipoSedeEmpresa } from '../../models/ecosistema.model';

@Component({
  selector: 'app-sede-form-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './sede-form-dialog.component.html',
  styleUrl: './sede-form-dialog.component.scss',
})
export class SedeFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(EcosistemaApiService);

  readonly sede = input<SedeEmpresa | null>(null);
  readonly saved = output<void>();
  readonly cancelled = output<void>();
  readonly error = signal('');
  readonly guardando = signal(false);

  readonly form = this.fb.nonNullable.group({
    nombre: ['', Validators.required],
    tipo: this.fb.nonNullable.control<TipoSedeEmpresa>('TIENDA'),
    direccion: ['', Validators.required],
    distrito: [''],
    provincia: ['Lima'],
    departamento: ['Lima'],
    ubigeo: ['', Validators.pattern(/^$|^\d{6}$/)],
    esPuntoPartidaGre: [true],
    esPuntoLlegadaGre: [false],
    esAlmacenPrincipal: [false],
    activa: [true],
  });

  ngOnInit(): void {
    const actual = this.sede();
    if (actual) {
      this.form.patchValue(actual);
    }
  }

  async guardar(): Promise<void> {
    this.error.set('');
    if (this.form.invalid || this.guardando()) {
      this.form.markAllAsTouched();
      return;
    }
    this.guardando.set(true);
    try {
      await this.api.guardarSede(this.sede()?.id ?? null, this.form.getRawValue());
      this.saved.emit();
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.guardando.set(false);
    }
  }
}
