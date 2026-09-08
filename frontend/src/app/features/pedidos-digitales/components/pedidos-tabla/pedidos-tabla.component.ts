import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';

import {
  COLUMNAS_KANBAN,
  ETIQUETAS_ESTADO_PEDIDO,
  ETIQUETAS_ORIGEN_PEDIDO,
  EstadoPedidoDigital,
  PedidoDigital,
  origenDeCanal,
  transicionesPermitidas,
} from '../../models/pedido-digital.model';
import { PedidoKanbanItem } from '../pedido-kanban-card/pedido-kanban-card.component';

export type PedidosTablaSort = 'fecha' | 'total' | 'cliente' | 'codigo' | 'estado';
export type PedidosTablaSortDir = 'asc' | 'desc';

const PAGE_SIZE = 40;

@Component({
  selector: 'app-pedidos-tabla',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './pedidos-tabla.component.html',
  styleUrl: './pedidos-tabla.component.scss',
})
export class PedidosTablaComponent {
  readonly items = input.required<PedidoKanbanItem[]>();
  readonly abrir = output<PedidoDigital>();
  readonly transicionar = output<{ pedido: PedidoDigital; estado: EstadoPedidoDigital }>();

  readonly etiquetasEstado = ETIQUETAS_ESTADO_PEDIDO;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PEDIDO;
  readonly estadosTab: ReadonlyArray<EstadoPedidoDigital | 'TODOS'> = [
    'TODOS',
    ...COLUMNAS_KANBAN,
  ];

  readonly estadoTab = signal<EstadoPedidoDigital | 'TODOS'>('TODOS');
  readonly sortKey = signal<PedidosTablaSort>('fecha');
  readonly sortDir = signal<PedidosTablaSortDir>('desc');
  readonly page = signal(1);

  readonly origenDe = origenDeCanal;
  readonly transicionesDe = transicionesPermitidas;

  readonly conteoPorEstado = computed(() => {
    const items = this.items();
    const mapa: Record<string, number> = { TODOS: items.length };
    for (const estado of COLUMNAS_KANBAN) {
      mapa[estado] = items.filter((item) => item.pedido.estado === estado).length;
    }
    return mapa;
  });

  readonly filtrados = computed(() => {
    const tab = this.estadoTab();
    const key = this.sortKey();
    const dir = this.sortDir();
    const factor = dir === 'asc' ? 1 : -1;

    const base =
      tab === 'TODOS'
        ? [...this.items()]
        : this.items().filter((item) => item.pedido.estado === tab);

    base.sort((a, b) => {
      const pa = a.pedido;
      const pb = b.pedido;
      let cmp = 0;
      switch (key) {
        case 'codigo':
          cmp = pa.codigo.localeCompare(pb.codigo, 'es');
          break;
        case 'cliente':
          cmp = pa.clienteNombre.localeCompare(pb.clienteNombre, 'es');
          break;
        case 'total':
          cmp = pa.total - pb.total;
          break;
        case 'estado':
          cmp = pa.estado.localeCompare(pb.estado);
          break;
        case 'fecha':
        default:
          cmp = new Date(pa.fechaPedido).getTime() - new Date(pb.fechaPedido).getTime();
          break;
      }
      return cmp * factor;
    });

    return base;
  });

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filtrados().length / PAGE_SIZE)),
  );

  readonly paginaActual = computed(() => Math.min(this.page(), this.totalPages()));

  readonly pageItems = computed(() => {
    const pagina = this.paginaActual();
    const inicio = (pagina - 1) * PAGE_SIZE;
    return this.filtrados().slice(inicio, inicio + PAGE_SIZE);
  });

  readonly rango = computed(() => {
    const total = this.filtrados().length;
    if (total === 0) {
      return { desde: 0, hasta: 0, total: 0 };
    }
    const pagina = this.paginaActual();
    const desde = (pagina - 1) * PAGE_SIZE + 1;
    const hasta = Math.min(pagina * PAGE_SIZE, total);
    return { desde, hasta, total };
  });

  setEstadoTab(estado: EstadoPedidoDigital | 'TODOS'): void {
    this.estadoTab.set(estado);
    this.page.set(1);
  }

  ordenar(clave: PedidosTablaSort): void {
    if (this.sortKey() === clave) {
      this.sortDir.update((dir) => (dir === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortKey.set(clave);
      this.sortDir.set(clave === 'cliente' || clave === 'codigo' ? 'asc' : 'desc');
    }
    this.page.set(1);
  }

  sortMark(clave: PedidosTablaSort): string {
    if (this.sortKey() !== clave) {
      return '';
    }
    return this.sortDir() === 'asc' ? ' ↑' : ' ↓';
  }

  paginaAnterior(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  paginaSiguiente(): void {
    this.page.update((p) => Math.min(this.totalPages(), p + 1));
  }

  puntoEntrega(item: PedidoKanbanItem): string {
    const e = item.pedido.entrega;
    if (e.puntoEntrega?.trim()) {
      return e.puntoEntrega.trim();
    }
    if (e.esRecojoTienda) {
      return item.sedeNombre || 'Recojo en tienda';
    }
    return e.distrito?.trim() || e.agencia?.trim() || e.direccion?.trim() || '—';
  }

  puedeEntregarRapido(pedido: PedidoDigital): boolean {
    return pedido.entrega.esRecojoTienda && this.transicionesDe(pedido).includes('Entregado');
  }

  etiquetaEstadoTab(estado: EstadoPedidoDigital | 'TODOS'): string {
    return estado === 'TODOS' ? 'Todos' : this.etiquetasEstado[estado];
  }
}
