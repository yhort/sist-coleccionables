import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { ReportesKpis } from '../../models/reporte.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-reportes-kpis',
  imports: [SolesPipe, DecimalPipe],
  templateUrl: './reportes-kpis.component.html',
  styleUrl: './reportes-kpis.component.scss',
})
export class ReportesKpisComponent {
  readonly kpis = input.required<ReportesKpis>();
}
