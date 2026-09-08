import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, input } from '@angular/core';

import {
  ETIQUETAS_MOVIMIENTO,
  KardexFila,
  esTipoMovimientoTcg,
} from '../../models/inventario.model';

@Component({
  selector: 'app-kardex-historial',
  imports: [DatePipe, DecimalPipe, NgClass],
  templateUrl: './kardex-historial.component.html',
  styleUrl: './kardex-historial.component.scss',
})
export class KardexHistorialComponent {
  readonly movimientos = input.required<KardexFila[]>();
  readonly etiquetas = ETIQUETAS_MOVIMIENTO;

  esTcg = esTipoMovimientoTcg;

  deltaDisponible(fila: KardexFila): number {
    return fila.stockPosterior - fila.stockAnterior;
  }

  deltaReservado(fila: KardexFila): number {
    return fila.reservadoPosterior - fila.reservadoAnterior;
  }
}
