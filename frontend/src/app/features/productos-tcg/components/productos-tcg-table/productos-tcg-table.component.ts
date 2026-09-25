import { NgClass } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { TablaPaginacionComponent } from '../../../../shared/ui/tabla-paginacion/tabla-paginacion.component';
import { primeraUrlImagen } from '../../data-access/producto-tcg.mapper';
import {
  ETIQUETAS_STOCK,
  ETIQUETAS_SYNC,
  ETIQUETAS_TIPO,
  ProductoTcg,
  estadoStockDe,
  etiquetaJuego,
} from '../../models/producto-tcg.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

/** Silueta local para no romper el thumb si la URL falla o no es un archivo de imagen. */
export const IMAGEN_PRODUCTO_PLACEHOLDER =
  'data:image/svg+xml,' +
  encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-label="Sin imagen">
      <rect width="64" height="64" rx="10" fill="#e8eef7"/>
      <circle cx="24" cy="22" r="5" fill="#93c5fd"/>
      <path d="M12 50l12-16 8 10 6-8 14 14H12z" fill="#60a5fa"/>
    </svg>`,
  );

export type VistaCatalogo = 'tabla' | 'grid';

@Component({
  selector: 'app-productos-tcg-table',
  imports: [SolesPipe, NgClass, TablaPaginacionComponent],
  templateUrl: './productos-tcg-table.component.html',
  styleUrl: './productos-tcg-table.component.scss',
})
export class ProductosTcgTableComponent {
  readonly productos = input.required<ProductoTcg[]>();
  readonly vista = input<VistaCatalogo>('tabla');
  readonly page = input(1);
  readonly pageSize = input(15);
  readonly total = input(0);
  readonly editar = output<ProductoTcg>();
  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly etiquetasTipo = ETIQUETAS_TIPO;
  readonly etiquetasStock = ETIQUETAS_STOCK;
  readonly etiquetasSync = ETIQUETAS_SYNC;
  readonly etiquetaJuego = etiquetaJuego;

  stockDe = estadoStockDe;
  readonly placeholderImagen = IMAGEN_PRODUCTO_PLACEHOLDER;

  private readonly imagenesFallidas = new Set<string>();

  imagenPrincipal(producto: ProductoTcg): string | null {
    return primeraUrlImagen(producto.woo.imagenes);
  }

  srcImagen(producto: ProductoTcg): string {
    const url = this.imagenPrincipal(producto);
    if (!url || this.imagenesFallidas.has(url)) {
      return this.placeholderImagen;
    }
    return url;
  }

  onImagenError(event: Event, producto: ProductoTcg): void {
    const url = this.imagenPrincipal(producto);
    if (url && url !== this.placeholderImagen) {
      this.imagenesFallidas.add(url);
    }
    const img = event.target as HTMLImageElement;
    if (img.src !== this.placeholderImagen) {
      img.src = this.placeholderImagen;
    }
  }

  /** Precio web efectivo (rebaja Woo si aplica). */
  precioWeb(producto: ProductoTcg): number {
    const rebajado = producto.woo.precioRebajado;
    if (rebajado != null && rebajado > 0 && rebajado < producto.woo.precioNormal) {
      return rebajado;
    }
    return producto.woo.precioNormal || 0;
  }

  /** Solo si está mapeado y el precio web difiere del de caja. */
  mostrarPrecioWoo(producto: ProductoTcg): boolean {
    if (producto.woo.wooCommerceId == null) {
      return false;
    }
    return this.precioWeb(producto) !== producto.precioVenta;
  }
}
