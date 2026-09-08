import { Component, HostListener, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ProveedoresApiService } from '../../data-access/proveedores.service';
import { Proveedor, validarRuc } from '../../models/proveedor.model';

@Component({
  selector: 'app-proveedor-form-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './proveedor-form-dialog.component.html',
  styleUrl: './proveedor-form-dialog.component.scss',
})
export class ProveedorFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ProveedoresApiService);

  readonly proveedor = input<Proveedor | null>(null);
  readonly saved = output<Proveedor>();
  readonly cancelled = output<void>();
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    ruc: ['', [Validators.required, Validators.minLength(11), Validators.maxLength(11)]],
    razonSocial: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
    nombreComercial: [''],
    telefono: [''],
    activo: [true],
  });

  ngOnInit(): void {
    const actual = this.proveedor();
    if (actual) {
      this.form.patchValue({
        ruc: actual.ruc,
        razonSocial: actual.razonSocial,
        nombreComercial: actual.nombreComercial ?? '',
        telefono: actual.telefono ?? '',
        activo: actual.activo,
      });
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    const raw = this.form.getRawValue();
    const rucError = validarRuc(raw.ruc);
    if (this.form.invalid || rucError) {
      this.error.set(rucError ?? 'Completa RUC y razón social.');
      return;
    }

    try {
      const proveedor = await this.api.guardar(
        {
          ruc: raw.ruc.replace(/\D/g, ''),
          razonSocial: raw.razonSocial,
          nombreComercial: raw.nombreComercial || null,
          telefono: raw.telefono || null,
          activo: raw.activo,
        },
        this.proveedor()?.id,
      );
      this.saved.emit(proveedor);
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }
}
