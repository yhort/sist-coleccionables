import { DatePipe } from '@angular/common';
import { AfterViewInit, Component, HostListener, computed, inject, input, output } from '@angular/core';

import { origenDeCanal } from '../../../pedidos-digitales/models/pedido-digital.model';
import { EntregasApiService } from '../../data-access/entregas.service';
import { EntregaFila } from '../../models/entrega.model';

export type TicketDespachoTipo = 'WEB' | 'SUBASTA';

@Component({
  selector: 'app-packing-slip-dialog',
  imports: [DatePipe],
  templateUrl: './packing-slip-dialog.component.html',
  styleUrl: './packing-slip-dialog.component.scss',
})
export class PackingSlipDialogComponent implements AfterViewInit {
  private readonly entregasApi = inject(EntregasApiService);

  readonly fila = input.required<EntregaFila>();
  readonly autoPrint = input(false);
  readonly closed = output<void>();

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

  readonly clienteNombre = computed(
    () =>
      this.fila().entrega.destinatarioNombre ||
      this.fila().pedido.clienteNombre ||
      'Cliente',
  );

  readonly contactoReferencia = computed(
    () => this.fila().entrega.contactoReferencia?.trim() || null,
  );

  readonly celular = computed(
    () =>
      this.fila().entrega.destinatarioTelefono ||
      this.fila().pedido.clienteTelefono ||
      '—',
  );

  readonly tienda = computed(() => this.fila().sedeNombre || 'Tienda');

  remitente() {
    return this.entregasApi.remitente(this.fila().entrega.sedeOrigenId);
  }

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
