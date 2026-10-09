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
  clienteNombre: string;
  celular: string;
  contactoReferencia: string;
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

  /** Fecha de entrega editable compartida por todas las hojas. */
  readonly fechaEntregaInput = signal(hoyLocalIso());

  readonly etiquetas = computed(() => agruparEtiquetas(this.pedidos()));

  readonly fechaTicket = computed(() => {
    const valor = this.fechaEntregaInput().trim();
    if (/^\d{4}-\d{2}-\d{2}$/.test(valor)) {
      return `${valor}T12:00:00`;
    }
    return valor || new Date().toISOString();
  });

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

  onFechaChange(valor: string): void {
    this.fechaEntregaInput.set(valor);
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
    esSubasta
      ? pedido.tituloSubasta?.trim() || pedido.codigoSubasta?.trim() || pedido.codigo
      : pedido.codigo,
  );

  const contacto =
    base.entrega.contactoReferencia?.trim() ||
    pedidos.map((p) => p.entrega.contactoReferencia?.trim()).find(Boolean) ||
    '';

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
    clienteNombre: base.clienteNombre?.trim() || 'Cliente',
    celular:
      base.entrega.destinatarioTelefono?.trim() ||
      base.clienteTelefono?.trim() ||
      '—',
    contactoReferencia: contacto,
    tienda: base.sedeNombre?.trim() || 'Tienda',
  };
}

function hoyLocalIso(): string {
  const ahora = new Date();
  const y = ahora.getFullYear();
  const m = String(ahora.getMonth() + 1).padStart(2, '0');
  const d = String(ahora.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
