import { Component, HostListener, input, output, signal } from '@angular/core';

import {
  ETIQUETAS_MOTIVO_DEVOLUCION_TIENDA,
  MOTIVOS_DEVOLUCION_TIENDA,
  MotivoDevolucionTienda,
  PedidoDigital,
} from '../../models/pedido-digital.model';

@Component({
  selector: 'app-liberar-stock-abandono-dialog',
  templateUrl: './liberar-stock-abandono-dialog.component.html',
  styleUrl: './liberar-stock-abandono-dialog.component.scss',
})
export class LiberarStockAbandonoDialogComponent {
  readonly pedido = input.required<PedidoDigital>();
  readonly guardando = input(false);
  readonly error = input('');
  readonly confirmed = output<MotivoDevolucionTienda>();
  readonly cancelled = output<void>();

  readonly motivos = MOTIVOS_DEVOLUCION_TIENDA;
  readonly etiquetasMotivo = ETIQUETAS_MOTIVO_DEVOLUCION_TIENDA;
  readonly motivo = signal<MotivoDevolucionTienda | ''>('');

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelar();
  }

  cancelar(): void {
    if (this.guardando()) {
      return;
    }
    this.cancelled.emit();
  }

  confirmar(): void {
    if (this.guardando()) {
      return;
    }
    const seleccionado = this.motivo();
    if (!seleccionado) {
      return;
    }
    this.confirmed.emit(seleccionado);
  }

  puedeConfirmar(): boolean {
    return !!this.motivo() && !this.guardando();
  }
}
