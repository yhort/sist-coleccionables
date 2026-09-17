import { DatePipe } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';

import {
  COLUMNAS_KANBAN,
  ETIQUETAS_ESTADO_PEDIDO,
  ETIQUETAS_ORIGEN_PEDIDO,
  EstadoPedidoDigital,
  PedidoDigital,
  etiquetaCantidadPedidos,
  mismoClienteCobro,
  origenDeCanal,
  puedeAnularPedido,
  puedeDespachar,
  puedeEmpaquetar,
  puedeEntregarRapido,
  puedeImprimirEtiqueta,
  puedeSeleccionarEnLote,
  round2,
} from '../../models/pedido-digital.model';
import {
  construirResumenWhatsAppLive,
  copiarAlPortapapeles,
} from '../../utils/resumen-whatsapp-live';
import { PedidoKanbanItem } from '../pedido-kanban-card/pedido-kanban-card.component';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

export type PedidosTablaSort = 'fecha' | 'total' | 'cliente' | 'codigo' | 'estado';
export type PedidosTablaSortDir = 'asc' | 'desc';

export type PedidosLoteAccion =
  | { tipo: 'cobro'; pedidos: PedidoDigital[]; clienteNombre: string; total: number }
  | { tipo: 'empaquetar'; pedidos: PedidoDigital[]; count: number }
  | { tipo: 'despachar'; pedidos: PedidoDigital[]; count: number }
  | { tipo: 'entregar'; pedidos: PedidoDigital[]; count: number }
  | { tipo: 'cpe'; pedidos: PedidoDigital[]; clienteNombre: string; total: number }
  | { tipo: 'invalido'; mensaje: string };

const PAGE_SIZE = 40;

@Component({
  selector: 'app-pedidos-tabla',
  imports: [SolesPipe, DatePipe],
  templateUrl: './pedidos-tabla.component.html',
  styleUrl: './pedidos-tabla.component.scss',
})
export class PedidosTablaComponent {
  readonly items = input.required<PedidoKanbanItem[]>();
  readonly abrir = output<PedidoDigital>();
  readonly transicionar = output<{ pedido: PedidoDigital; estado: EstadoPedidoDigital }>();
  readonly transicionarLote = output<{ pedidos: PedidoDigital[]; estado: EstadoPedidoDigital }>();
  readonly registrarPago = output<PedidoDigital[]>();
  readonly emitirCpe = output<PedidoDigital[]>();
  readonly imprimirEtiquetas = output<PedidoDigital[]>();
  readonly anular = output<PedidoDigital>();
  readonly marcarNotificados = output<PedidoDigital[]>();
  readonly toggleNotificacion = output<PedidoDigital>();
  readonly aviso = output<string>();
  readonly errorAccion = output<string>();

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
  readonly seleccionados = signal<ReadonlySet<string>>(new Set());

  readonly origenDe = origenDeCanal;
  readonly puedeEmpaquetar = puedeEmpaquetar;
  readonly puedeDespachar = puedeDespachar;
  readonly puedeEntregarRapido = puedeEntregarRapido;
  readonly puedeAnular = puedeAnularPedido;
  readonly puedeEtiqueta = puedeImprimirEtiqueta;
  readonly etiquetaCantidad = etiquetaCantidadPedidos;

  constructor() {
    effect(() => {
      const tab = this.estadoTab();
      const vigentes = new Set(
        this.items()
          .filter((item) => puedeSeleccionarEnLote(item.pedido, tab))
          .map((item) => item.pedido.id),
      );
      this.seleccionados.update((ids) => {
        const next = new Set([...ids].filter((id) => vigentes.has(id)));
        return next.size === ids.size && [...ids].every((id) => next.has(id)) ? ids : next;
      });
    });
  }

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

  readonly seleccionablesPagina = computed(() => {
    const tab = this.estadoTab();
    return this.pageItems().filter((item) => puedeSeleccionarEnLote(item.pedido, tab));
  });

  readonly todosPaginaSeleccionados = computed(() => {
    const seleccionables = this.seleccionablesPagina();
    const ids = this.seleccionados();
    return seleccionables.length > 0 && seleccionables.every((item) => ids.has(item.pedido.id));
  });

  readonly paginaParcial = computed(() => {
    const seleccionables = this.seleccionablesPagina();
    const ids = this.seleccionados();
    const cuantos = seleccionables.filter((item) => ids.has(item.pedido.id)).length;
    return cuantos > 0 && cuantos < seleccionables.length;
  });

  readonly pedidosSeleccionados = computed(() => {
    const ids = this.seleccionados();
    return this.items()
      .map((item) => item.pedido)
      .filter((pedido) => ids.has(pedido.id));
  });

  readonly lote = computed<PedidosLoteAccion | null>(() => {
    const pedidos = this.pedidosSeleccionados();
    if (pedidos.length === 0) {
      return null;
    }
    const estados = new Set(pedidos.map((pedido) => pedido.estado));
    if (estados.size !== 1) {
      return {
        tipo: 'invalido',
        mensaje: 'Selecciona pedidos del mismo estado para actuar en lote.',
      };
    }
    const estado = pedidos[0].estado;
    switch (estado) {
      case 'PendientePago':
        if (!mismoClienteCobro(pedidos)) {
          return {
            tipo: 'invalido',
            mensaje: 'Selecciona pedidos del mismo cliente en Pendiente de pago.',
          };
        }
        return {
          tipo: 'cobro',
          pedidos,
          clienteNombre: pedidos[0].clienteNombre,
          total: round2(pedidos.reduce((sum, pedido) => sum + pedido.total, 0)),
        };
      case 'Pagado':
        return { tipo: 'empaquetar', pedidos, count: pedidos.length };
      case 'Empaquetado':
        return { tipo: 'despachar', pedidos, count: pedidos.length };
      case 'PendienteEntrega':
        return { tipo: 'entregar', pedidos, count: pedidos.length };
      case 'Entregado':
        if (!mismoClienteCobro(pedidos)) {
          return {
            tipo: 'invalido',
            mensaje: 'Selecciona pedidos del mismo cliente para emitir un comprobante consolidado.',
          };
        }
        return {
          tipo: 'cpe',
          pedidos,
          clienteNombre: pedidos[0].clienteNombre,
          total: round2(pedidos.reduce((sum, pedido) => sum + pedido.total, 0)),
        };
      default:
        return { tipo: 'invalido', mensaje: 'Este estado no tiene acciones en lote.' };
    }
  });

  puedeSeleccionar(pedido: PedidoDigital): boolean {
    return puedeSeleccionarEnLote(pedido, this.estadoTab());
  }

  setEstadoTab(estado: EstadoPedidoDigital | 'TODOS'): void {
    this.estadoTab.set(estado);
    this.seleccionados.set(new Set());
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

  estaSeleccionado(id: string): boolean {
    return this.seleccionados().has(id);
  }

  toggleFila(pedido: PedidoDigital, checked: boolean): void {
    if (!this.puedeSeleccionar(pedido)) {
      return;
    }
    this.seleccionados.update((ids) => {
      const next = new Set(ids);
      if (checked) {
        next.add(pedido.id);
      } else {
        next.delete(pedido.id);
      }
      return next;
    });
  }

  toggleTodosPagina(checked: boolean): void {
    const idsPagina = this.seleccionablesPagina().map((item) => item.pedido.id);
    this.seleccionados.update((ids) => {
      const next = new Set(ids);
      for (const id of idsPagina) {
        if (checked) {
          next.add(id);
        } else {
          next.delete(id);
        }
      }
      return next;
    });
  }

  limpiarSeleccion(): void {
    this.seleccionados.set(new Set());
  }

  async copiarResumen(): Promise<void> {
    const lote = this.lote();
    if (lote?.tipo !== 'cobro') {
      this.errorAccion.emit('Selecciona pedidos del mismo cliente en Pendiente de pago.');
      return;
    }
    const texto = construirResumenWhatsAppLive(lote.pedidos);
    const ok = await copiarAlPortapapeles(texto);
    if (!ok) {
      this.errorAccion.emit('No se pudo copiar el resumen. Cópialo manualmente.');
      return;
    }
    // Persistir notificado=true; el padre muestra toast tras éxito del API.
    this.marcarNotificados.emit(lote.pedidos);
  }

  abrirPago(): void {
    const lote = this.lote();
    if (lote?.tipo !== 'cobro') {
      this.errorAccion.emit('Selecciona pedidos del mismo cliente en Pendiente de pago.');
      return;
    }
    this.registrarPago.emit(lote.pedidos);
  }

  ejecutarLoteLogistico(estado: EstadoPedidoDigital): void {
    const lote = this.lote();
    if (
      (estado === 'Empaquetado' && lote?.tipo !== 'empaquetar') ||
      (estado === 'PendienteEntrega' && lote?.tipo !== 'despachar') ||
      (estado === 'Entregado' && lote?.tipo !== 'entregar')
    ) {
      this.errorAccion.emit('Selecciona pedidos del mismo estado operativo.');
      return;
    }
    if (!lote || lote.tipo === 'invalido' || lote.tipo === 'cobro' || lote.tipo === 'cpe') {
      return;
    }
    this.transicionarLote.emit({ pedidos: lote.pedidos, estado });
    this.limpiarSeleccion();
  }

  abrirCpe(): void {
    const lote = this.lote();
    if (lote?.tipo !== 'cpe') {
      this.errorAccion.emit(
        'Selecciona pedidos del mismo cliente para emitir un comprobante consolidado.',
      );
      return;
    }
    this.emitirCpe.emit(lote.pedidos);
  }

  imprimirEtiquetasLote(): void {
    const lote = this.lote();
    if (lote?.tipo !== 'despachar') {
      this.errorAccion.emit('Selecciona pedidos en Empaquetado para imprimir etiquetas en lote.');
      return;
    }
    this.imprimirEtiquetas.emit(lote.pedidos);
  }

  abrirEtiqueta(pedido: PedidoDigital): void {
    if (!puedeImprimirEtiqueta(pedido)) {
      return;
    }
    this.imprimirEtiquetas.emit([pedido]);
  }

  etiquetaSubasta(pedido: PedidoDigital): string | null {
    if (pedido.canalPedido !== 'FACEBOOK_SUBASTA' && !pedido.subastaTcgId) {
      return null;
    }
    return pedido.tituloSubasta?.trim() || pedido.codigoSubasta?.trim() || null;
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

  etiquetaEstadoTab(estado: EstadoPedidoDigital | 'TODOS'): string {
    return estado === 'TODOS' ? 'Todos' : this.etiquetasEstado[estado];
  }
}
