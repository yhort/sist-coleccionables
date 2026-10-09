import { Component, HostListener, input, output } from '@angular/core';

import { PedidoDigital } from '../../models/pedido-digital.model';

@Component({
  selector: 'app-liberar-stock-abandono-dialog',
  templateUrl: './liberar-stock-abandono-dialog.component.html',
  styleUrl: './liberar-stock-abandono-dialog.component.scss',
})
export class LiberarStockAbandonoDialogComponent {
  readonly pedido = input.required<PedidoDigital>();
  readonly guardando = input(false);
  readonly error = input('');
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

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
    this.confirmed.emit();
  }
}
