import { Component, computed, inject, model } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ProductosTcgApiService } from '../../data-access/productos-tcg.service';
import {
  ETIQUETAS_JUEGO,
  ETIQUETAS_STOCK,
  ETIQUETAS_SYNC,
  ETIQUETAS_TIPO,
  FILTROS_PRODUCTOS_VACIOS,
  JuegoTcg,
  ProductosTcgFiltros,
  TipoProductoTcg,
  etiquetaJuego,
  normalizarJuego,
} from '../../models/producto-tcg.model';

@Component({
  selector: 'app-productos-tcg-filters',
  imports: [FormsModule],
  templateUrl: './productos-tcg-filters.component.html',
  styleUrl: './productos-tcg-filters.component.scss',
})
export class ProductosTcgFiltersComponent {
  private readonly productosApi = inject(ProductosTcgApiService);

  readonly filtros = model.required<ProductosTcgFiltros>();

  readonly tipos = Object.entries(ETIQUETAS_TIPO) as [TipoProductoTcg, string][];
  readonly estadosStock = Object.entries(ETIQUETAS_STOCK);
  readonly estadosSync = Object.entries(ETIQUETAS_SYNC);

  /** Conocidos + juegos/categorías ya usados en el catálogo. */
  readonly juegos = computed(() => {
    const porValor = new Map<string, string>();
    for (const [codigo, etiqueta] of Object.entries(ETIQUETAS_JUEGO) as [JuegoTcg, string][]) {
      porValor.set(codigo, etiqueta);
    }
    for (const producto of this.productosApi.productos()) {
      const valor = normalizarJuego(producto.juego);
      if (valor && !porValor.has(valor)) {
        porValor.set(valor, etiquetaJuego(valor) || valor);
      }
    }
    return [...porValor.entries()].sort((a, b) => a[1].localeCompare(b[1], 'es'));
  });

  limpiar(): void {
    this.filtros.set({ ...FILTROS_PRODUCTOS_VACIOS });
  }
}
