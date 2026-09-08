import { Component, HostListener, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { SedeInventario } from '../../../inventario/models/inventario.model';
import { CajaApiService } from '../../data-access/caja.service';

@Component({
  selector: 'app-caja-apertura-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './caja-apertura-dialog.component.html',
  styleUrl: './caja-apertura-dialog.component.scss',
})
export class CajaAperturaDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly cajaApi = inject(CajaApiService);

  readonly sedes = input.required<readonly SedeInventario[]>();
  readonly sedeIdInicial = input<string>('');
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly enviando = signal(false);

  readonly form = this.fb.nonNullable.group({
    sedeId: ['', Validators.required],
    montoApertura: [0, [Validators.required, Validators.min(0)]],
    observacion: [''],
  });

  ngOnInit(): void {
    const sedeId = this.sedeIdInicial() || this.sedes()[0]?.id || '';
    if (sedeId) {
      this.form.controls.sedeId.setValue(sedeId);
    }
  }

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
      await this.cajaApi.abrir({
        sedeId: raw.sedeId,
        montoApertura: Number(raw.montoApertura),
        observacion: raw.observacion.trim() || null,
      });
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo abrir la caja.');
    } finally {
      this.enviando.set(false);
    }
  }
}
