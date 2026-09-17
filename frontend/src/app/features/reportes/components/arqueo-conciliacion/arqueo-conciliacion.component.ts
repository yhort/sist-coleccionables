import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { ReporteArqueo } from '../../models/reporte.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-arqueo-conciliacion',
  imports: [SolesPipe, DecimalPipe],
  templateUrl: './arqueo-conciliacion.component.html',
  styleUrl: './arqueo-conciliacion.component.scss',
})
export class ArqueoConciliacionComponent {
  readonly arqueo = input.required<ReporteArqueo>();
}
