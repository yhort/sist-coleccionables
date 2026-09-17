import { Component, HostListener, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import { PagosApiService } from '../../../pagos/data-access/pagos.service';
import {
  ETIQUETAS_ORIGEN_PAGO,
  ORIGENES_PAGO,
  OrigenPago,
  esOrigenDigital,
} from '../../../pagos/models/pago.model';
import { PedidoDigital, round2 } from '../../models/pedido-digital.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-registrar-pago-lote-dialog',
  imports: [SolesPipe, ReactiveFormsModule],
  templateUrl: './registrar-pago-lote-dialog.component.html',
  styleUrl: './registrar-pago-lote-dialog.component.scss',
})
export class RegistrarPagoLoteDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly pagosApi = inject(PagosApiService);

  readonly pedidos = input.required<PedidoDigital[]>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly enviando = signal(false);
  readonly origenes = ORIGENES_PAGO;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PAGO;

  readonly form = this.fb.nonNullable.group({
    origen: this.fb.nonNullable.control<OrigenPago>('YAPE'),
    codigoOperacion: [''],
    referenciaExterna: [''],
    observacion: [''],
  });

  readonly total = computed(() => round2(this.pedidos().reduce((sum, pedido) => sum + pedido.total, 0)));

  readonly clienteNombre = computed(() => this.pedidos()[0]?.clienteNombre ?? 'Cliente');

  readonly lineas = computed(() =>
    this.pedidos().flatMap((pedido) =>
      pedido.detalles.map((detalle) => ({
        pedidoCodigo: pedido.codigo,
        descripcion:
          detalle.cantidad > 1
            ? `${detalle.descripcion.trim()} x${detalle.cantidad}`
            : detalle.descripcion.trim(),
        total: detalle.total,
      })),
    ),
  );

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.enviando()) {
      this.cancelled.emit();
    }
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    const pedidos = this.pedidos();
    if (pedidos.length === 0) {
      this.error.set('Selecciona al menos un pedido pendiente de pago.');
      return;
    }

    const raw = this.form.getRawValue();
    if (esOrigenDigital(raw.origen) && !raw.codigoOperacion.trim()) {
      this.error.set('Indica el código de operación Yape, Plin, Izipay o tarjeta.');
      return;
    }

    this.enviando.set(true);
    try {
      await this.pagosApi.registrarLote({
        origen: raw.origen,
        monto: this.total(),
        codigoOperacion: raw.codigoOperacion,
        referenciaExterna: raw.referenciaExterna,
        observacion: raw.observacion,
        confirmar: true,
        pedidoDigitalIds: pedidos.map((pedido) => pedido.id),
      });
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo registrar el pago.');
    } finally {
      this.enviando.set(false);
    }
  }
}
