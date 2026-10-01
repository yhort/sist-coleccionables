import { DatePipe, NgClass } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { EstadoPedidoDigital } from '../../../pedidos-digitales/models/pedido-digital.model';
import { codigoPedido } from '../../../../core/ui/codigo-amigable';
import {
  ETIQUETAS_ESTADO_PAGO,
  ETIQUETAS_ORIGEN_PAGO,
  Pago,
  esOrigenDigital,
} from '../../models/pago.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

export interface PagoFila {
  pago: Pago;
  clienteNombre: string;
  pedidoEstado: EstadoPedidoDigital | null;
}

const PAGE_SIZE = 40;

@Component({
  selector: 'app-pagos-table',
  imports: [SolesPipe, DatePipe, NgClass, RouterLink],
  templateUrl: './pagos-table.component.html',
  styleUrl: './pagos-table.component.scss',
})
export class PagosTableComponent {
  readonly filas = input.required<PagoFila[]>();
  readonly accionId = input<string | null>(null);
  readonly asociar = output<Pago>();
  readonly confirmar = output<Pago>();
  readonly rechazar = output<Pago>();
  readonly anular = output<Pago>();

  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PAGO;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_PAGO;
  readonly page = signal(1);

  constructor() {
    effect(() => {
      this.filas();
      this.page.set(1);
    });
  }

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filas().length / PAGE_SIZE)),
  );

  readonly paginaActual = computed(() => Math.min(this.page(), this.totalPages()));

  readonly pageItems = computed(() => {
    const pagina = this.paginaActual();
    const inicio = (pagina - 1) * PAGE_SIZE;
    return this.filas().slice(inicio, inicio + PAGE_SIZE);
  });

  readonly rango = computed(() => {
    const total = this.filas().length;
    if (total === 0) {
      return { desde: 0, hasta: 0, total: 0 };
    }
    const pagina = this.paginaActual();
    const desde = (pagina - 1) * PAGE_SIZE + 1;
    const hasta = Math.min(pagina * PAGE_SIZE, total);
    return { desde, hasta, total };
  });

  puedeConfirmar(pago: Pago): boolean {
    if (pago.estado === 'ASOCIADO') {
      return true;
    }
    return pago.estado === 'NOTIFICADO' && !pago.pedidoDigitalId && !esOrigenDigital(pago.origen);
  }

  puedeAnular(fila: PagoFila): boolean {
    const pago = fila.pago;
    if (pago.estado !== 'CONFIRMADO' || pago.ventaId) {
      return false;
    }
    const estado = fila.pedidoEstado;
    return (
      estado == null ||
      estado === 'PendientePago' ||
      estado === 'Pagado'
    );
  }

  codigoPedidoDe(fila: PagoFila): string {
    return fila.pago.pedidoCodigo || codigoPedido(fila.pago.pedidoDigitalId);
  }

  paginaAnterior(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  paginaSiguiente(): void {
    this.page.update((p) => Math.min(this.totalPages(), p + 1));
  }
}
