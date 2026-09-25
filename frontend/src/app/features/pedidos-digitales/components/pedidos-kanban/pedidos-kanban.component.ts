import { Component, input, output, signal } from '@angular/core';

import {
  COLUMNAS_KANBAN,
  ETIQUETAS_ESTADO_PEDIDO,
  EstadoPedidoDigital,
  PedidoDigital,
} from '../../models/pedido-digital.model';
import {
  PedidoKanbanCardComponent,
  PedidoKanbanItem,
} from '../pedido-kanban-card/pedido-kanban-card.component';

@Component({
  selector: 'app-pedidos-kanban',
  imports: [PedidoKanbanCardComponent],
  templateUrl: './pedidos-kanban.component.html',
  styleUrl: './pedidos-kanban.component.scss',
})
export class PedidosKanbanComponent {
  readonly items = input.required<PedidoKanbanItem[]>();
  readonly abrir = output<PedidoDigital>();
  readonly transicionar = output<{ pedido: PedidoDigital; estado: EstadoPedidoDigital }>();

  readonly columnas = COLUMNAS_KANBAN;
  readonly etiquetas = ETIQUETAS_ESTADO_PEDIDO;
  readonly arrastrandoId = signal<string | null>(null);
  readonly destinoHover = signal<EstadoPedidoDigital | null>(null);

  itemsDe(estado: EstadoPedidoDigital): PedidoKanbanItem[] {
    return this.items()
      .filter((item) => item.pedido.estado === estado)
      .slice()
      .sort(
        (a, b) =>
          new Date(b.pedido.fechaPedido).getTime() - new Date(a.pedido.fechaPedido).getTime(),
      );
  }

  puedeArrastrar(pedido: PedidoDigital): boolean {
    return pedido.estado !== 'Entregado' && pedido.estado !== 'Cancelado' && pedido.estado !== 'Anulado' && pedido.estado !== 'Devuelto';
  }

  onDragStart(event: DragEvent, pedido: PedidoDigital): void {
    if (!this.puedeArrastrar(pedido)) {
      event.preventDefault();
      return;
    }
    this.arrastrandoId.set(pedido.id);
    event.dataTransfer?.setData('text/plain', pedido.id);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
  }

  onDragEnd(): void {
    this.arrastrandoId.set(null);
    this.destinoHover.set(null);
  }

  onDragOver(event: DragEvent, estado: EstadoPedidoDigital): void {
    event.preventDefault();
    this.destinoHover.set(estado);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  }

  onDrop(event: DragEvent, estado: EstadoPedidoDigital): void {
    event.preventDefault();
    const id = this.arrastrandoId() ?? event.dataTransfer?.getData('text/plain');
    this.arrastrandoId.set(null);
    this.destinoHover.set(null);
    if (!id) {
      return;
    }
    const item = this.items().find((fila) => fila.pedido.id === id);
    if (!item || item.pedido.estado === estado) {
      return;
    }
    this.transicionar.emit({ pedido: item.pedido, estado });
  }
}
