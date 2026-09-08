import { CurrencyPipe } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { ETIQUETAS_TIPO, TipoProductoTcg } from '../../../productos-tcg/models/producto-tcg.model';
import {
  ETIQUETAS_ESTADO_PEDIDO,
  ETIQUETAS_ORIGEN_PEDIDO,
  EstadoPedidoDigital,
  PedidoDigital,
  indicadorReservaDe,
  origenDeCanal,
  transicionesPermitidas,
} from '../../models/pedido-digital.model';

export interface PedidoKanbanItem {
  pedido: PedidoDigital;
  sedeNombre: string;
  tiposProducto: TipoProductoTcg[];
}

@Component({
  selector: 'app-pedido-kanban-card',
  imports: [CurrencyPipe],
  templateUrl: './pedido-kanban-card.component.html',
  styleUrl: './pedido-kanban-card.component.scss',
})
export class PedidoKanbanCardComponent {
  readonly item = input.required<PedidoKanbanItem>();
  readonly abrir = output<PedidoDigital>();
  readonly transicionar = output<{ pedido: PedidoDigital; estado: EstadoPedidoDigital }>();

  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PEDIDO;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_PEDIDO;
  readonly etiquetasTipo = ETIQUETAS_TIPO;

  origenDe = origenDeCanal;
  transicionesDe = transicionesPermitidas;
  reservaDe = indicadorReservaDe;

  tiposEtiqueta(tipos: TipoProductoTcg[]): string {
    const unicos = [...new Set(tipos)];
    return unicos.map((tipo) => this.etiquetasTipo[tipo]).join(' · ');
  }

  onMenu(pedido: PedidoDigital, valor: string): void {
    if (!valor) {
      return;
    }
    this.transicionar.emit({ pedido, estado: valor as EstadoPedidoDigital });
  }

  /** Recojo en mostrador: 1 clic a Entregado cuando el flujo lo permite. */
  puedeEntregarRapido(pedido: PedidoDigital): boolean {
    return (
      pedido.entrega.esRecojoTienda &&
      this.transicionesDe(pedido).includes('Entregado')
    );
  }

  entregar(pedido: PedidoDigital, event: Event): void {
    event.stopPropagation();
    this.transicionar.emit({ pedido, estado: 'Entregado' });
  }
}
