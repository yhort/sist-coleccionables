import { DatePipe } from '@angular/common';
import {
  AfterViewInit,
  Component,
  HostListener,
  computed,
  input,
  output,
  signal,
} from '@angular/core';

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

  /** Fecha de entrega editable; por defecto hoy (yyyy-MM-dd). */
  readonly fechaEntregaInput = signal(hoyLocalIso());

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
      return (
        pedido.tituloSubasta?.trim() ||
        pedido.codigoSubasta?.trim() ||
        pedido.codigo
      );
    }
    return pedido.codigo;
  });

  readonly fechaTicket = computed(() => {
    const valor = this.fechaEntregaInput().trim();
    if (/^\d{4}-\d{2}-\d{2}$/.test(valor)) {
      return `${valor}T12:00:00`;
    }
    return valor || new Date().toISOString();
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

  readonly contactoReferencia = computed(
    () =>
      this.fila().entrega.contactoReferencia?.trim() ||
      this.fila().pedido.entrega?.contactoReferencia?.trim() ||
      '',
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

  onFechaChange(valor: string): void {
    this.fechaEntregaInput.set(valor);
  }

  imprimir(): void {
    globalThis.print();
  }
}

function hoyLocalIso(): string {
  const ahora = new Date();
  const y = ahora.getFullYear();
  const m = String(ahora.getMonth() + 1).padStart(2, '0');
  const d = String(ahora.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
