import { NgClass } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ETIQUETAS_ESTADO_PEDIDO } from '../../../pedidos-digitales/models/pedido-digital.model';
import { etiquetaCanalContacto } from '../../../../shared/models/contacto-entrega.model';
import {
  ETIQUETAS_ESTADO_LOGISTICA,
  ETIQUETAS_METODO_ENVIO,
  EntregaFila,
  destinoDe,
} from '../../models/entrega.model';

const PAGE_SIZE = 40;

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

  paginaAnterior(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  paginaSiguiente(): void {
    this.page.update((p) => Math.min(this.totalPages(), p + 1));
  }
}
