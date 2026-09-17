import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';

import {
  ETIQUETAS_ORIGEN_PAGO,
  ORIGENES_PAGO,
  PagosKpis,
} from '../../models/pago.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-pagos-kpis',
  imports: [SolesPipe, DecimalPipe],
  templateUrl: './pagos-kpis.component.html',
  styleUrl: './pagos-kpis.component.scss',
})
export class PagosKpisComponent {
  readonly kpis = input.required<PagosKpis>();
  readonly origenes = ORIGENES_PAGO;
  readonly etiquetas = ETIQUETAS_ORIGEN_PAGO;
}
