import { DecimalPipe, NgClass } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { TablaPaginacionComponent } from '../../../../shared/ui/tabla-paginacion/tabla-paginacion.component';
import { ETIQUETAS_TIPO } from '../../../productos-tcg/models/producto-tcg.model';
import { StockFila } from '../../models/inventario.model';

@Component({
  selector: 'app-stock-sede-table',
  imports: [DecimalPipe, NgClass, TablaPaginacionComponent],
  templateUrl: './stock-sede-table.component.html',
  styleUrl: './stock-sede-table.component.scss',
})
export class StockSedeTableComponent {
  readonly filas = input.required<StockFila[]>();
  readonly page = input(1);
  readonly pageSize = input(15);
  readonly total = input(0);
  readonly ajustar = output<StockFila>();
  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly etiquetasTipo = ETIQUETAS_TIPO;

  estadoLibre(fila: StockFila): 'ok' | 'bajo' | 'sin' | 'reservado' {
    if (fila.cantidadDisponible <= 0) {
      return 'sin';
    }
    if (fila.cantidadLibre <= 0) {
      return 'reservado';
    }
    if (fila.cantidadLibre <= 3) {
      return 'bajo';
    }
    return 'ok';
  }
}
