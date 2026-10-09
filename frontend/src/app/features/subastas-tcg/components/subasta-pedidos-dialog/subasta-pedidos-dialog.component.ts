import { Component, HostListener, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PedidoVinculoSubasta, SubastaTcg } from '../../models/subasta-tcg.model';

@Component({
  selector: 'app-subasta-pedidos-dialog',
  imports: [RouterLink],
  templateUrl: './subasta-pedidos-dialog.component.html',
  styleUrl: './subasta-pedidos-dialog.component.scss',
})
export class SubastaPedidosDialogComponent {
  readonly subasta = input.required<SubastaTcg>();
  readonly pedidos = input.required<PedidoVinculoSubasta[]>();
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
