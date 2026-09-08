import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { ReporteArqueo } from '../../models/reporte.model';

@Component({
  selector: 'app-arqueo-conciliacion',
  imports: [CurrencyPipe, DecimalPipe],
  templateUrl: './arqueo-conciliacion.component.html',
  styleUrl: './arqueo-conciliacion.component.scss',
})
export class ArqueoConciliacionComponent {
  readonly arqueo = input.required<ReporteArqueo>();
}
