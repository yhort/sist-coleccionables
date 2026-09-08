import { CurrencyPipe } from '@angular/common';
import {
  Component,
  HostListener,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { startWith } from 'rxjs';

import { PedidosDigitalesApiService } from '../../../pedidos-digitales/data-access/pedidos-digitales.service';
import { PedidoDigital } from '../../../pedidos-digitales/models/pedido-digital.model';
import { PagosApiService } from '../../data-access/pagos.service';
import {
  ETIQUETAS_ORIGEN_PAGO,
  ORIGENES_PAGO,
  OrigenPago,
  Pago,
  round2,
} from '../../models/pago.model';

@Component({
  selector: 'app-asociar-pago-dialog',
  imports: [CurrencyPipe, ReactiveFormsModule],
  templateUrl: './asociar-pago-dialog.component.html',
  styleUrl: './asociar-pago-dialog.component.scss',
})
export class AsociarPagoDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly pagosApi = inject(PagosApiService);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);

  readonly pago = input<Pago | null>(null);
  readonly pedidoIdInicial = input<string | null>(null);
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly busquedaPedido = signal('');
  readonly origenes = ORIGENES_PAGO;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PAGO;

  readonly form = this.fb.nonNullable.group({
    origen: this.fb.nonNullable.control<OrigenPago>('YAPE'),
    codigoOperacion: [''],
    monto: [0, [Validators.required, Validators.min(0.01)]],
    referenciaExterna: [''],
    clienteNombre: [''],
    pedidoDigitalId: [''],
    observacion: [''],
    confirmar: [true],
  });

  private readonly codigoValue = toSignal(
    this.form.controls.codigoOperacion.valueChanges.pipe(
      startWith(this.form.controls.codigoOperacion.value),
    ),
    { initialValue: '' },
  );

  private readonly pedidoIdValue = toSignal(
    this.form.controls.pedidoDigitalId.valueChanges.pipe(
      startWith(this.form.controls.pedidoDigitalId.value),
    ),
    { initialValue: '' },
  );

  readonly codigoDuplicado = computed(() =>
    this.pagosApi.codigoOperacionDuplicado(this.codigoValue() ?? '', this.pago()?.id),
  );

  readonly pedidos = computed(() => {
    this.pagosApi.pagos();
    this.pedidosApi.pedidos();
    return this.pagosApi.pedidosPendientes(this.busquedaPedido());
  });

  readonly pedidoSeleccionado = computed(() => {
    const id = this.pedidoIdValue();
    return id ? this.pedidosApi.obtener(id) : undefined;
  });

  get esAsociacion(): boolean {
    return this.pago() !== null;
  }

  ngOnInit(): void {
    const existente = this.pago();
    if (existente) {
      this.form.patchValue({
        origen: existente.origen,
        codigoOperacion: existente.codigoOperacion ?? '',
        monto: existente.monto,
        referenciaExterna: existente.referenciaExterna ?? '',
        clienteNombre: existente.clienteNombre ?? '',
        pedidoDigitalId: existente.pedidoDigitalId ?? this.pedidoIdInicial() ?? '',
        observacion: existente.observacion ?? '',
        confirmar: true,
      });
      this.form.controls.origen.disable();
      this.form.controls.monto.disable();
      this.form.controls.codigoOperacion.disable();
      this.form.controls.referenciaExterna.disable();
    } else if (this.pedidoIdInicial()) {
      this.form.patchValue({ pedidoDigitalId: this.pedidoIdInicial() ?? '' });
      this.aplicarSaldoPedido(this.pedidoIdInicial());
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  onPedidoChange(pedidoId: string): void {
    this.form.controls.pedidoDigitalId.setValue(pedidoId);
    if (!this.esAsociacion) {
      this.aplicarSaldoPedido(pedidoId);
    }
  }

  saldoDe(pedido: PedidoDigital): number {
    return this.pagosApi.saldoPendiente(pedido);
  }

  etiquetaPedido(pedido: PedidoDigital): string {
    return `${pedido.clienteNombre} - ${pedido.codigo} - saldo PEN${this.saldoDe(pedido).toFixed(2)}`;
  }

  guardar(): void {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.codigoDuplicado() && !this.esAsociacion) {
      this.error.set('Ya existe un pago con ese código de operación.');
      return;
    }

    try {
      const existente = this.pago();
      const pedidoId = this.form.controls.pedidoDigitalId.value.trim() || null;
      const confirmar = this.form.controls.confirmar.value;

      if (existente) {
        if (!pedidoId) {
          throw new Error('Selecciona un pedido digital pendiente.');
        }
        this.pagosApi.asociar(existente.id, pedidoId);
        if (confirmar) {
          this.pagosApi.confirmar(existente.id);
        }
      } else {
        if (this.form.invalid) {
          return;
        }
        const raw = this.form.getRawValue();
        this.pagosApi.registrar({
          origen: raw.origen,
          monto: Number(raw.monto),
          codigoOperacion: raw.codigoOperacion,
          referenciaExterna: raw.referenciaExterna,
          pedidoDigitalId: pedidoId,
          clienteNombre: raw.clienteNombre,
          observacion: raw.observacion,
          confirmar: confirmar && !!pedidoId,
        });
      }
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar el pago.');
    }
  }

  private aplicarSaldoPedido(pedidoId: string | null | undefined): void {
    if (!pedidoId) {
      return;
    }
    const pedido = this.pedidosApi.obtener(pedidoId);
    if (!pedido || pedido.estado !== 'PendientePago') {
      return;
    }
    const saldo = this.pagosApi.saldoPendiente(pedido);
    if (saldo > 0) {
      this.form.controls.monto.setValue(round2(saldo));
    }
    this.form.controls.clienteNombre.setValue(pedido.clienteNombre);
  }
}
