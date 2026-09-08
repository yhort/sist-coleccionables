import { CurrencyPipe, DatePipe, NgClass } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { EstadoPedidoDigital } from '../../../pedidos-digitales/models/pedido-digital.model';
import { codigoPedido } from '../../../../core/ui/codigo-amigable';
import {
  ETIQUETAS_ESTADO_PAGO,
  ETIQUETAS_ORIGEN_PAGO,
  Pago,
  esOrigenDigital,
} from '../../models/pago.model';

export interface PagoFila {
  pago: Pago;
  clienteNombre: string;
  pedidoEstado: EstadoPedidoDigital | null;
}

@Component({
  selector: 'app-pagos-table',
  imports: [CurrencyPipe, DatePipe, NgClass, RouterLink],
  templateUrl: './pagos-table.component.html',
  styleUrl: './pagos-table.component.scss',
})
export class PagosTableComponent {
  readonly filas = input.required<PagoFila[]>();
  readonly asociar = output<Pago>();
  readonly confirmar = output<Pago>();
  readonly rechazar = output<Pago>();

  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PAGO;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_PAGO;

  puedeConfirmar(pago: Pago): boolean {
    if (pago.estado === 'ASOCIADO') {
      return true;
    }
    return pago.estado === 'NOTIFICADO' && !pago.pedidoDigitalId && !esOrigenDigital(pago.origen);
  }

  codigoPedidoDe(fila: PagoFila): string {
    return fila.pago.pedidoCodigo || codigoPedido(fila.pago.pedidoDigitalId);
  }
}
