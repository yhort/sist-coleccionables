import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, input } from '@angular/core';

import { ResumenYieldAperturas } from '../../models/reporte.model';

@Component({
  selector: 'app-aperturas-yield-reporte',
  imports: [CurrencyPipe, DecimalPipe, NgClass],
  templateUrl: './aperturas-yield-reporte.component.html',
  styleUrl: './aperturas-yield-reporte.component.scss',
})
export class AperturasYieldReporteComponent {
  readonly resumen = input.required<ResumenYieldAperturas>();

  signo(valor: number): 'pos' | 'neg' | 'neu' {
    if (valor > 0) {
      return 'pos';
    }
    if (valor < 0) {
      return 'neg';
    }
    return 'neu';
  }
}
