import { CurrencyPipe } from '@angular/common';
import {
  Component,
  HostListener,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { EntregasApiService } from '../../data-access/entregas.service';
import { codigoPedido } from '../../../../core/ui/codigo-amigable';
import {
  ETIQUETAS_METODO_ENVIO,
  EntregaFila,
  METODOS_ENVIO,
  MetodoEnvio,
} from '../../models/entrega.model';

@Component({
  selector: 'app-programar-entrega-dialog',
  imports: [CurrencyPipe, ReactiveFormsModule],
  templateUrl: './programar-entrega-dialog.component.html',
  styleUrl: './programar-entrega-dialog.component.scss',
})
export class ProgramarEntregaDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly entregasApi = inject(EntregasApiService);

  readonly fila = input.required<EntregaFila>();
  readonly modo = input<'empaque' | 'despacho'>('empaque');
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly metodos = METODOS_ENVIO;
  readonly etiquetasMetodo = ETIQUETAS_METODO_ENVIO;

  readonly form = this.fb.nonNullable.group({
    notasEmpaque: [''],
    metodoEnvio: this.fb.nonNullable.control<MetodoEnvio>('OLVA_COURIER'),
    numeroTracking: [''],
    agencia: [''],
    costoEnvio: [0, [Validators.min(0)]],
    observacion: [''],
  });

  esRecojo(): boolean {
    return this.form.controls.metodoEnvio.value === 'RECOJO_TIENDA';
  }

  ngOnInit(): void {
    const { pedido, entrega } = this.fila();
    this.form.patchValue({
      notasEmpaque: entrega.notasEmpaque ?? '',
      metodoEnvio: entrega.metodoEnvio,
      numeroTracking: entrega.numeroTracking ?? '',
      agencia: entrega.agencia ?? '',
      costoEnvio: entrega.costoEnvio,
      observacion: entrega.observacion ?? '',
    });
    if (this.modo() === 'empaque' && !entrega.notasEmpaque) {
      this.form.controls.notasEmpaque.setValue(
        pedido.detalles.length === 1
          ? `Empaque de ${pedido.detalles[0].descripcion}.`
          : `Empaque de ${pedido.detalles.length} ítems.`,
      );
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  onMetodoChange(metodo: MetodoEnvio): void {
    this.form.controls.metodoEnvio.setValue(metodo);
    if (metodo === 'RECOJO_TIENDA') {
      this.form.patchValue({
        costoEnvio: 0,
        numeroTracking: this.form.controls.numeroTracking.value || `RECOJO-${codigoPedido(this.fila().pedido)}`,
        agencia: this.form.controls.agencia.value || this.fila().sedeNombre,
      });
    }
  }

  guardar(): void {
    this.error.set('');
    const raw = this.form.getRawValue();
    try {
      if (this.modo() === 'empaque') {
        this.entregasApi.empaquetar(this.fila().pedido.id, { notasEmpaque: raw.notasEmpaque });
      } else {
        this.entregasApi.despachar(this.fila().pedido.id, {
          metodoEnvio: raw.metodoEnvio,
          numeroTracking: raw.numeroTracking,
          agencia: raw.agencia,
          costoEnvio: Number(raw.costoEnvio),
          observacion: raw.observacion,
        });
      }
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar la entrega.');
    }
  }
}
