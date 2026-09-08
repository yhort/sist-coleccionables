import { Component, HostListener, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { CajaApiService } from '../../data-access/caja.service';
import { TipoCajaMovimiento } from '../../models/caja.model';

@Component({
  selector: 'app-caja-movimiento-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './caja-movimiento-dialog.component.html',
  styleUrl: './caja-movimiento-dialog.component.scss',
})
export class CajaMovimientoDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly cajaApi = inject(CajaApiService);

  readonly sedeId = input.required<string>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly enviando = signal(false);

  readonly form = this.fb.nonNullable.group({
    tipo: this.fb.nonNullable.control<TipoCajaMovimiento>('INGRESO'),
    monto: [0, [Validators.required, Validators.min(0.01)]],
    concepto: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
  });

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid || this.enviando()) {
      return;
    }

    const raw = this.form.getRawValue();
    this.enviando.set(true);
    try {
      await this.cajaApi.registrarMovimiento({
        sedeId: this.sedeId(),
        tipo: raw.tipo,
        monto: Number(raw.monto),
        concepto: raw.concepto.trim(),
      });
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo registrar el movimiento.');
    } finally {
      this.enviando.set(false);
    }
  }
}
