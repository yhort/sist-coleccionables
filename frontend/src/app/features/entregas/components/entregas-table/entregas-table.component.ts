import { NgClass } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ETIQUETAS_ESTADO_PEDIDO } from '../../../pedidos-digitales/models/pedido-digital.model';
import { etiquetaCanalContacto } from '../../../../shared/models/contacto-entrega.model';
import {
  ETIQUETAS_ESTADO_LOGISTICA,
  ETIQUETAS_METODO_ENVIO,
  EntregaFila,
  destinoDe,
} from '../../models/entrega.model';

@Component({
  selector: 'app-entregas-table',
  imports: [NgClass, RouterLink],
  templateUrl: './entregas-table.component.html',
  styleUrl: './entregas-table.component.scss',
})
export class EntregasTableComponent {
  readonly filas = input.required<EntregaFila[]>();
  readonly empaquetar = output<EntregaFila>();
  readonly despachar = output<EntregaFila>();
  readonly confirmar = output<EntregaFila>();
  readonly etiqueta = output<EntregaFila>();

  readonly etiquetasMetodo = ETIQUETAS_METODO_ENVIO;
  readonly etiquetasLogistica = ETIQUETAS_ESTADO_LOGISTICA;
  readonly etiquetasPedido = ETIQUETAS_ESTADO_PEDIDO;
  readonly destino = destinoDe;

  canalDe(fila: EntregaFila): string | null {
    const canal = fila.entrega.canalContacto ?? null;
    const etiqueta = etiquetaCanalContacto(canal);
    return etiqueta === '—' ? null : etiqueta;
  }

  puedeEmpaquetar(fila: EntregaFila): boolean {
    return fila.pedido.estado === 'Pagado';
  }

  puedeDespachar(fila: EntregaFila): boolean {
    return fila.pedido.estado === 'Empaquetado' || fila.pedido.estado === 'PendienteEntrega';
  }

  puedeConfirmar(fila: EntregaFila): boolean {
    return (
      fila.pedido.estado === 'PendienteEntrega' ||
      (fila.pedido.estado === 'Empaquetado' && fila.entrega.metodoEnvio === 'RECOJO_TIENDA')
    );
  }
}
