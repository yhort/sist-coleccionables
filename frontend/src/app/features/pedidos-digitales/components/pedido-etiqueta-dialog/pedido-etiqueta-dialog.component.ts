import { DatePipe } from '@angular/common';
import { AfterViewInit, Component, HostListener, computed, input, output } from '@angular/core';

import { etiquetaCanalContacto } from '../../../../shared/models/contacto-entrega.model';
import {
  ETIQUETAS_CANAL_PEDIDO,
  PedidoDigital,
  etiquetaCantidadPedidos,
} from '../../models/pedido-digital.model';

export interface EtiquetaImpresion {
  id: string;
  consolidada: boolean;
  pedidos: PedidoDigital[];
  codigos: string;
  clienteNombre: string;
  sedeNombre: string;
  telefono: string;
  canal: string;
  destinatarioFinal: string;
  referencia: string | null;
  metodoEnvio: string;
  direccion: string;
  subasta: string | null;
  lineas: string[];
  fecha: string;
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

  readonly logoUrl = 'assets/img/logo-trunqi.jpg';
  readonly etiquetasCanal = ETIQUETAS_CANAL_PEDIDO;
  readonly etiquetaCantidad = etiquetaCantidadPedidos;
  readonly etiquetaCanalContacto = etiquetaCanalContacto;

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
    const consolidadas = hojas.filter((e) => e.consolidada).length;
    if (consolidadas === 0) {
      return `${this.etiquetaCantidad(this.pedidos().length)} · ${hojas.length} etiqueta${hojas.length === 1 ? '' : 's'} individual${hojas.length === 1 ? '' : 'es'}`;
    }
    return `${this.etiquetaCantidad(this.pedidos().length)} · ${hojas.length} etiqueta${hojas.length === 1 ? '' : 's'} (${consolidadas} consolidada${consolidadas === 1 ? '' : 's'})`;
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
    const clave = claveCliente(pedido);
    const lista = grupos.get(clave) ?? [];
    lista.push(pedido);
    grupos.set(clave, lista);
  }

  const etiquetas: EtiquetaImpresion[] = [];
  for (const grupo of grupos.values()) {
    if (grupo.length === 1) {
      etiquetas.push(construirEtiqueta(grupo, false));
    } else {
      etiquetas.push(construirEtiqueta(grupo, true));
    }
  }

  return etiquetas;
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

function construirEtiqueta(pedidos: PedidoDigital[], consolidada: boolean): EtiquetaImpresion {
  const base = pedidos[0];
  const codigos = pedidos.map((pedido) => pedido.codigo).join(' / ');
  const lineas = pedidos.flatMap((pedido) =>
    pedido.detalles.map((detalle) => {
      const item =
        detalle.cantidad > 1
          ? `${detalle.descripcion.trim()} ×${detalle.cantidad}`
          : detalle.descripcion.trim();
      return consolidada ? `${pedido.codigo} · ${item}` : item;
    }),
  );

  const subastas = [
    ...new Set(
      pedidos
        .map((pedido) => pedido.tituloSubasta?.trim() || pedido.codigoSubasta?.trim() || '')
        .filter((valor) => valor.length > 0),
    ),
  ];

  return {
    id: pedidos.map((pedido) => pedido.id).join('|'),
    consolidada,
    pedidos,
    codigos,
    clienteNombre: base.clienteNombre,
    sedeNombre: base.sedeNombre?.trim() || 'Sede',
    telefono: telefonoDe(base),
    canal: canalDe(pedidos),
    destinatarioFinal: destinatarioFinalDe(base),
    referencia: base.entrega.contactoReferencia?.trim() || null,
    metodoEnvio: metodoEnvioDe(base),
    direccion: direccionEnvioDe(base),
    subasta: subastas.length > 0 ? subastas.join(' · ') : null,
    lineas,
    fecha: base.fechaPedido,
  };
}

function telefonoDe(pedido: PedidoDigital): string {
  return (
    pedido.entrega.destinatarioTelefono?.trim() ||
    pedido.clienteTelefono?.trim() ||
    '—'
  );
}

function canalDe(pedidos: PedidoDigital[]): string {
  const base = pedidos[0];
  const canalPedido = ETIQUETAS_CANAL_PEDIDO[base.canalPedido] ?? base.canalPedido;
  const contacto = base.entrega.canalContacto
    ? etiquetaCanalContacto(base.entrega.canalContacto)
    : null;
  return contacto ? `${canalPedido} · ${contacto}` : canalPedido;
}

function destinatarioFinalDe(pedido: PedidoDigital): string {
  const dest = pedido.entrega.destinatarioNombre?.trim();
  if (dest && dest.toLowerCase() !== pedido.clienteNombre.trim().toLowerCase()) {
    return dest;
  }
  return dest || pedido.clienteNombre;
}

function metodoEnvioDe(pedido: PedidoDigital): string {
  const e = pedido.entrega;
  if (e.esRecojoTienda) {
    return 'Recojo en tienda';
  }
  return e.puntoEntrega?.trim() || e.agencia?.trim() || e.courier?.trim() || 'Envío';
}

function direccionEnvioDe(pedido: PedidoDigital): string {
  const e = pedido.entrega;
  if (e.esRecojoTienda) {
    return pedido.sedeNombre?.trim() || 'Mostrador';
  }
  const partes = [
    e.direccion,
    e.distrito,
    e.provincia,
    e.departamento,
    e.agencia ? `Agencia: ${e.agencia}` : null,
    e.numeroTracking ? `Tracking: ${e.numeroTracking}` : null,
  ]
    .map((p) => p?.trim())
    .filter((p): p is string => !!p && p.length > 0);
  return partes.length > 0 ? partes.join(' · ') : '—';
}
