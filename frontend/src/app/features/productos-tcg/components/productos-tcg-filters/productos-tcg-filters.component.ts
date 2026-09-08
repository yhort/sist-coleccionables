import { Component, model } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  ETIQUETAS_JUEGO,
  ETIQUETAS_STOCK,
  ETIQUETAS_SYNC,
  ETIQUETAS_TIPO,
  FILTROS_PRODUCTOS_VACIOS,
  JuegoTcg,
  ProductosTcgFiltros,
  TipoProductoTcg,
} from '../../models/producto-tcg.model';

@Component({
  selector: 'app-productos-tcg-filters',
  imports: [FormsModule],
  templateUrl: './productos-tcg-filters.component.html',
  styleUrl: './productos-tcg-filters.component.scss',
})
export class ProductosTcgFiltersComponent {
  readonly filtros = model.required<ProductosTcgFiltros>();

  readonly tipos = Object.entries(ETIQUETAS_TIPO) as [TipoProductoTcg, string][];
  readonly juegos = Object.entries(ETIQUETAS_JUEGO) as [JuegoTcg, string][];
  readonly estadosStock = Object.entries(ETIQUETAS_STOCK);
  readonly estadosSync = Object.entries(ETIQUETAS_SYNC);

  limpiar(): void {
    this.filtros.set({ ...FILTROS_PRODUCTOS_VACIOS });
  }
}
