import { DecimalPipe, NgClass } from '@angular/common';
import { Component, input } from '@angular/core';

import { RendimientoApertura } from '../../models/apertura-tcg.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-apertura-yield-card',
  imports: [SolesPipe, DecimalPipe, NgClass],
  templateUrl: './apertura-yield-card.component.html',
  styleUrl: './apertura-yield-card.component.scss',
})
export class AperturaYieldCardComponent {
  readonly rendimiento = input.required<RendimientoApertura>();
  readonly compacto = input(false);

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
