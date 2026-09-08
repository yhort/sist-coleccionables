import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { ReportesKpis } from '../../models/reporte.model';

@Component({
  selector: 'app-reportes-kpis',
  imports: [CurrencyPipe, DecimalPipe],
  templateUrl: './reportes-kpis.component.html',
  styleUrl: './reportes-kpis.component.scss',
})
export class ReportesKpisComponent {
  readonly kpis = input.required<ReportesKpis>();
}
