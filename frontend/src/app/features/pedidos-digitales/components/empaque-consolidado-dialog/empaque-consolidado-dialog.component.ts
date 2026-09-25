import { Component, HostListener, computed, inject, input, output } from '@angular/core';

import { codigoItem, pareceUuid } from '../../../../core/ui/codigo-amigable';
import {
  ETIQUETAS_RAREZA,
  ProductoTcg,
  RarezaTcg,
} from '../../../productos-tcg/models/producto-tcg.model';
import { PedidosDigitalesApiService } from '../../data-access/pedidos-digitales.service';
import {
  PedidoDigital,
  etiquetaCantidadPedidos,
  round2,
} from '../../models/pedido-digital.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

export interface LineaEmpaqueConsolidado {
  clave: string;
  sku: string;
  nombre: string;
  rareza: string;
  cantidad: number;
  total: number;
}

@Component({
  selector: 'app-empaque-consolidado-dialog',
  imports: [SolesPipe],
  templateUrl: './empaque-consolidado-dialog.component.html',
  styleUrl: './empaque-consolidado-dialog.component.scss',
})
export class EmpaqueConsolidadoDialogComponent {
  private readonly pedidosApi = inject(PedidosDigitalesApiService);

  readonly pedidos = input.required<PedidoDigital[]>();
  readonly closed = output<void>();

  readonly etiquetaCantidad = etiquetaCantidadPedidos;

  readonly clienteNombre = computed(() => this.pedidos()[0]?.clienteNombre ?? 'Cliente');

  readonly codigos = computed(() => this.pedidos().map((pedido) => pedido.codigo).join(' · '));

  readonly lineas = computed(() => consolidarProductos(this.pedidos(), this.pedidosApi.productos()));

  readonly totalUnidades = computed(() =>
    this.lineas().reduce((sum, linea) => sum + linea.cantidad, 0),
  );

  readonly totalMonto = computed(() =>
    round2(this.lineas().reduce((sum, linea) => sum + linea.total, 0)),
  );

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}

function consolidarProductos(
  pedidos: PedidoDigital[],
  catalogo: readonly ProductoTcg[],
): LineaEmpaqueConsolidado[] {
  const mapa = new Map<string, LineaEmpaqueConsolidado>();

  for (const pedido of pedidos) {
    for (const detalle of pedido.detalles) {
      const producto = catalogo.find((item) => item.id === detalle.productoId);
      const skuRaw = producto?.codigoSku?.trim() ?? detalle.codigo?.trim() ?? '';
      const sku = skuRaw && !pareceUuid(skuRaw) ? skuRaw : codigoItem(detalle.id);
      const rarezaRaw = producto?.atributosTcg?.rareza ?? '';
      const rareza =
        rarezaRaw && rarezaRaw in ETIQUETAS_RAREZA
          ? ETIQUETAS_RAREZA[rarezaRaw as RarezaTcg]
          : rarezaRaw || '—';
      const nombre = detalle.descripcion.trim() || producto?.nombre?.trim() || 'Producto';
      const clave = `${detalle.productoId}|${nombre.toLowerCase()}|${sku.toLowerCase()}`;
      const actual = mapa.get(clave);
      if (actual) {
        actual.cantidad = round2(actual.cantidad + Number(detalle.cantidad));
        actual.total = round2(actual.total + Number(detalle.total));
      } else {
        mapa.set(clave, {
          clave,
          sku,
          nombre,
          rareza,
          cantidad: Number(detalle.cantidad),
          total: Number(detalle.total),
        });
      }
    }
  }

  return [...mapa.values()].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
}
