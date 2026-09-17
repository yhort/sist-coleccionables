import { Component, computed, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { startWith } from 'rxjs';

import { EcosistemaApiService } from '../../data-access/ecosistema.service';
import {
  ETIQUETAS_COMPROBANTE,
  ETIQUETAS_ESTADO_EMISION,
  EmisionSimulada,
  TipoComprobanteSunat,
  numeroComprobante,
  puedeDescargarCdr,
  puedeDescargarXml,
  puedeAbrirPdf,
} from '../../models/ecosistema.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-emitir-cpe-dialog',
  imports: [SolesPipe, ReactiveFormsModule],
  templateUrl: './emitir-cpe-dialog.component.html',
  styleUrl: './emitir-cpe-dialog.component.scss',
})
export class EmitirCpeDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(EcosistemaApiService);

  readonly cancelled = output<void>();
  readonly saved = output<void>();
  readonly error = signal('');
  readonly enviando = signal(false);
  readonly emision = signal<EmisionSimulada | null>(null);
  readonly tipos = ['BOLETA', 'FACTURA', 'NOTA_VENTA'] as const;
  readonly etiquetas = ETIQUETAS_COMPROBANTE;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_EMISION;

  readonly form = this.fb.nonNullable.group({
    tipo: this.fb.nonNullable.control<TipoComprobanteSunat>('BOLETA'),
    documentoId: ['', Validators.required],
  });

  private readonly tipoValue = toSignal(
    this.form.controls.tipo.valueChanges.pipe(startWith(this.form.controls.tipo.value)),
    { initialValue: this.form.controls.tipo.value },
  );

  constructor() {
    this.form.controls.tipo.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.form.controls.documentoId.setValue('');
      this.error.set('');
      this.emision.set(null);
    });
  }

  readonly documentos = computed(() => {
    this.api.series();
    return this.api.documentosEmitibles(this.tipoValue());
  });

  readonly seriePreview = computed(() => {
    const serie = this.api.series().find((item) => item.tipo === this.tipoValue());
    if (!serie) {
      return '—';
    }
    return `${serie.serie}-${String(serie.correlativo).padStart(8, '0')}`;
  });

  numeroDe = numeroComprobante;
  puedeXml = puedeDescargarXml;
  puedeCdr = puedeDescargarCdr;
  puedePdf = puedeAbrirPdf;

  async emitir(): Promise<void> {
    this.error.set('');
    this.emision.set(null);
    this.enviando.set(true);
    try {
      const emision = await this.api.emitirPrueba(this.form.getRawValue());
      this.emision.set(emision);
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo emitir el comprobante.');
    } finally {
      this.enviando.set(false);
    }
  }

  async descargarXml(): Promise<void> {
    const emision = this.emision();
    if (!emision) {
      return;
    }
    try {
      await this.api.descargarXml(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el XML.');
    }
  }

  async descargarCdr(): Promise<void> {
    const emision = this.emision();
    if (!emision) {
      return;
    }
    try {
      await this.api.descargarCdr(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el CDR.');
    }
  }

  async abrirPdf(formato: 'a4' | 'ticket' = 'a4'): Promise<void> {
    const emision = this.emision();
    if (!emision) {
      return;
    }
    try {
      await this.api.abrirPdf(emision, formato);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo abrir el PDF.');
    }
  }
}
