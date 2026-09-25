import { DatePipe } from '@angular/common';
import { AfterViewInit, Component, HostListener, computed, input, output } from '@angular/core';

import {
  origenDeCanal,
  PedidoDigital,
  etiquetaCantidadPedidos,
} from '../../models/pedido-digital.model';

export type TicketEtiquetaTipo = 'WEB' | 'SUBASTA';

export interface EtiquetaImpresion {
  id: string;
  consolidada: boolean;
  tipo: TicketEtiquetaTipo;
  tituloTipo: string;
  etiquetaNumero: string;
  numero: string;
  fecha: string;
  clienteNombre: string;
  celular: string;
  tienda: string;
}

@Component({
  selector: 'app-pedido-etiqueta-dialog',
  imports: [DatePipe],
  templateUrl: './pedido-etiqueta-dialog.component.html',
  styleUrl: './pedido-etiqueta-dialog.component.scss',
})
export class PedidoEtiquetaDialogComponent implements AfterViewInit {
  readonly pedidos = input.required<PedidoDigital[]>();
  readonly autoPrint = input(false);
  readonly closed = output<void>();

  readonly logoUrl = 'assets/img/logo-trunqi.png';
  readonly etiquetaCantidad = etiquetaCantidadPedidos;

  readonly etiquetas = computed(() => agruparEtiquetas(this.pedidos()));

  readonly titulo = computed(() => {
    const hojas = this.etiquetas().length;
    const pedidos = this.pedidos().length;
    if (hojas <= 1 && pedidos <= 1) {
      return 'Etiqueta de despacho';
    }
    if (hojas === 1 && pedidos > 1) {
      return `Etiqueta consolidada (${this.etiquetaCantidad(pedidos)})`;
    }
    return `Etiquetas de despacho (${hojas} · ${this.etiquetaCantidad(pedidos)})`;
  });

  readonly resumenHint = computed(() => {
    const hojas = this.etiquetas();
    const subastas = hojas.filter((e) => e.tipo === 'SUBASTA').length;
    const webs = hojas.length - subastas;
    const partes: string[] = [];
    if (webs > 0) {
      partes.push(`${webs} PEDIDO WEB`);
    }
    if (subastas > 0) {
      partes.push(`${subastas} SUBASTA`);
    }
    return `${this.etiquetaCantidad(this.pedidos().length)} · ${partes.join(' · ') || 'sin hojas'}`;
  });

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

function agruparEtiquetas(pedidos: PedidoDigital[]): EtiquetaImpresion[] {
  if (pedidos.length === 0) {
    return [];
  }

  const grupos = new Map<string, PedidoDigital[]>();
  for (const pedido of pedidos) {
    const clave = `${tipoDePedido(pedido)}|${claveCliente(pedido)}`;
    const lista = grupos.get(clave) ?? [];
    lista.push(pedido);
    grupos.set(clave, lista);
  }

  return [...grupos.values()].map((grupo) =>
    construirEtiqueta(grupo, grupo.length > 1),
  );
}

function claveCliente(pedido: PedidoDigital): string {
  const nombre = pedido.clienteNombre.trim().toLowerCase();
  if (nombre.length > 0) {
    return `n:${nombre}`;
  }
  if (pedido.clienteId?.trim()) {
    return `i:${pedido.clienteId.trim()}`;
  }
  return `p:${pedido.id}`;
}

function tipoDePedido(pedido: PedidoDigital): TicketEtiquetaTipo {
  if (pedido.subastaTcgId || pedido.canalPedido === 'FACEBOOK_SUBASTA') {
    return 'SUBASTA';
  }
  if (origenDeCanal(pedido.canalPedido) === 'FACEBOOK_SUBASTA') {
    return 'SUBASTA';
  }
  return 'WEB';
}

function construirEtiqueta(pedidos: PedidoDigital[], consolidada: boolean): EtiquetaImpresion {
  const base = pedidos[0];
  const tipo = tipoDePedido(base);
  const esSubasta = tipo === 'SUBASTA';

  const numeros = pedidos.map((pedido) =>
    esSubasta ? pedido.codigoSubasta?.trim() || pedido.codigo : pedido.codigo,
  );

  return {
    id: pedidos.map((pedido) => pedido.id).join('|'),
    consolidada,
    tipo,
    tituloTipo: esSubasta ? 'SUBASTA' : 'PEDIDO WEB',
    etiquetaNumero: esSubasta
      ? consolidada
        ? 'N° SUBASTAS'
        : 'N° SUBASTA'
      : consolidada
        ? 'N° PEDIDOS'
        : 'N° PEDIDO',
    numero: [...new Set(numeros)].join(' / '),
    fecha: fechaDespachoDe(base),
    clienteNombre: base.clienteNombre?.trim() || 'Cliente',
    celular:
      base.entrega.destinatarioTelefono?.trim() ||
      base.clienteTelefono?.trim() ||
      '—',
    tienda: base.sedeNombre?.trim() || 'Tienda',
  };
}

/** Preferir fecha de despacho (→ PendienteEntrega); si no, empaque; si no, pedido. */
function fechaDespachoDe(pedido: PedidoDigital): string {
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
}
