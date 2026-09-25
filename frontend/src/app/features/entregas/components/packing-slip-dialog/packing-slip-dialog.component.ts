import { DatePipe } from '@angular/common';
import { AfterViewInit, Component, HostListener, computed, input, output } from '@angular/core';

import { origenDeCanal } from '../../../pedidos-digitales/models/pedido-digital.model';
import { EntregaFila } from '../../models/entrega.model';

export type TicketDespachoTipo = 'WEB' | 'SUBASTA';

@Component({
  selector: 'app-packing-slip-dialog',
  imports: [DatePipe],
  templateUrl: './packing-slip-dialog.component.html',
  styleUrl: './packing-slip-dialog.component.scss',
})
export class PackingSlipDialogComponent implements AfterViewInit {
  readonly fila = input.required<EntregaFila>();
  readonly autoPrint = input(false);
  readonly closed = output<void>();

  readonly logoUrl = 'assets/img/logo-trunqi.png';

  readonly tipoTicket = computed<TicketDespachoTipo>(() => {
    const pedido = this.fila().pedido;
    if (pedido.subastaTcgId || pedido.canalPedido === 'FACEBOOK_SUBASTA') {
      return 'SUBASTA';
    }
    const origen = origenDeCanal(pedido.canalPedido);
    if (origen === 'FACEBOOK_SUBASTA') {
      return 'SUBASTA';
    }
    return 'WEB';
  });

  readonly esSubasta = computed(() => this.tipoTicket() === 'SUBASTA');

  readonly tituloTicket = computed(() =>
    this.esSubasta() ? 'SUBASTA' : 'PEDIDO WEB',
  );

  readonly etiquetaNumero = computed(() =>
    this.esSubasta() ? 'N° SUBASTA' : 'N° PEDIDO',
  );

  readonly numeroTicket = computed(() => {
    const pedido = this.fila().pedido;
    if (this.esSubasta()) {
      return pedido.codigoSubasta || pedido.codigo;
    }
    return pedido.codigo;
  });

  readonly fechaTicket = computed(() => {
    const pedido = this.fila().pedido;
    const historial = [...(pedido.historialEstados ?? [])].sort((a, b) =>
      a.fecha.localeCompare(b.fecha),
    );
    const despacho = [...historial]
      .reverse()
      .find((evento) => evento.estadoNuevo === 'PendienteEntrega');
    if (despacho) {
      return despacho.fecha;
    }
    const empaque = [...historial]
      .reverse()
      .find((evento) => evento.estadoNuevo === 'Empaquetado');
    if (empaque) {
      return empaque.fecha;
    }
    return pedido.fechaPedido;
  });

  readonly clienteNombre = computed(
    () => this.fila().pedido.clienteNombre?.trim() || 'Cliente',
  );

  readonly celular = computed(
    () =>
      this.fila().entrega.destinatarioTelefono ||
      this.fila().pedido.clienteTelefono ||
      '—',
  );

  readonly tienda = computed(() => this.fila().sedeNombre || 'Tienda');

  ngAfterViewInit(): void {
    if (this.autoPrint()) {
      queueMicrotask(() => this.imprimir());
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }

  imprimir(): void {
    globalThis.print();
  }
}
