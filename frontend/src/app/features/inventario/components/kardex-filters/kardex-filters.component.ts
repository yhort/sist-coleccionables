import { Component, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ProductoTcg } from '../../../productos-tcg/models/producto-tcg.model';
import {
  ETIQUETAS_MOVIMIENTO,
  FILTROS_KARDEX_VACIOS,
  GRUPOS_MOVIMIENTO,
  KardexFiltros,
  SedeInventario,
} from '../../models/inventario.model';

@Component({
  selector: 'app-kardex-filters',
  imports: [FormsModule],
  templateUrl: './kardex-filters.component.html',
  styleUrl: './kardex-filters.component.scss',
})
export class KardexFiltersComponent {
  readonly filtros = model.required<KardexFiltros>();
  readonly sedes = input<readonly SedeInventario[]>([]);
  readonly productos = input<readonly ProductoTcg[]>([]);

  readonly grupos = GRUPOS_MOVIMIENTO;
  readonly etiquetas = ETIQUETAS_MOVIMIENTO;

  limpiar(): void {
    this.filtros.set({ ...FILTROS_KARDEX_VACIOS });
  }
}
