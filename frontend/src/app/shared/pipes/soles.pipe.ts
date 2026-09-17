import { formatCurrency } from '@angular/common';
import { Pipe, PipeTransform, inject, LOCALE_ID } from '@angular/core';

import { MONEDA_CODIGO, MONEDA_DIGITOS, SIMBOLO_SOL } from '../utils/moneda';

/**
 * Formatea montos en soles peruanos con prefijo "S/ " (no el código ISO "PEN").
 * Uso: `{{ monto | soles }}`
 */
@Pipe({
  name: 'soles',
  standalone: true,
})
export class SolesPipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(
    value: number | string | null | undefined,
    digitsInfo: string = MONEDA_DIGITOS,
  ): string | null {
    if (value == null || value === '') {
      return null;
    }
    const n = typeof value === 'string' ? Number(value) : value;
    if (Number.isNaN(n)) {
      return null;
    }
    return formatCurrency(n, this.locale, SIMBOLO_SOL, MONEDA_CODIGO, digitsInfo);
  }
}
