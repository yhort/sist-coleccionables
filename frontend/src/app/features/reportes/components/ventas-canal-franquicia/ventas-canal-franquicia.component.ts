import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { SerieReporte } from '../../models/reporte.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-ventas-canal-franquicia',
  imports: [SolesPipe, DecimalPipe],
  templateUrl: './ventas-canal-franquicia.component.html',
  styleUrl: './ventas-canal-franquicia.component.scss',
})
export class VentasCanalFranquiciaComponent {
  readonly canales = input.required<SerieReporte[]>();
  readonly franquicias = input.required<SerieReporte[]>();
}
