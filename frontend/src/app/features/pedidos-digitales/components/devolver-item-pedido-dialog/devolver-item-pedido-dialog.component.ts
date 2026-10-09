import { Component, input, output } from '@angular/core';

import { PedidoDigital, PedidoDigitalDetalle } from '../../models/pedido-digital.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-devolver-item-pedido-dialog',
  imports: [SolesPipe],
  templateUrl: './devolver-item-pedido-dialog.component.html',
  styleUrl: './devolver-item-pedido-dialog.component.scss',
})
export class DevolverItemPedidoDialogComponent {
  readonly pedido = input.required<PedidoDigital>();
  readonly detalle = input.required<PedidoDigitalDetalle>();
  readonly guardando = input(false);
  readonly error = input('');
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

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
