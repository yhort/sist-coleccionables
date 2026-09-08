import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, input } from '@angular/core';

import { MargenSubasta } from '../../models/subasta-tcg.model';

@Component({
  selector: 'app-subasta-margen-card',
  imports: [CurrencyPipe, DecimalPipe, NgClass],
  templateUrl: './subasta-margen-card.component.html',
  styleUrl: './subasta-margen-card.component.scss',
})
export class SubastaMargenCardComponent {
  readonly margen = input.required<MargenSubasta>();
  readonly etiquetaOferta = input('Oferta de referencia');
  readonly compacto = input(false);

  signo(valor: number | null): 'pos' | 'neg' | 'neu' {
    if (valor === null || valor === 0) {
      return 'neu';
    }
    return valor > 0 ? 'pos' : 'neg';
  }
}
