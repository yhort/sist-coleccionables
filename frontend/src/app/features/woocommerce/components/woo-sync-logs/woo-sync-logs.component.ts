import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';

import { WooCommerceApiService } from '../../data-access/woocommerce.service';
import { ETIQUETAS_TIPO_SYNC, ResultadoEventoSyncWoo } from '../../models/woocommerce.model';

@Component({
  selector: 'app-woo-sync-logs',
  imports: [DatePipe],
  templateUrl: './woo-sync-logs.component.html',
  styleUrl: './woo-sync-logs.component.scss',
})
export class WooSyncLogsComponent {
  readonly wooApi = inject(WooCommerceApiService);
  readonly etiquetasTipo = ETIQUETAS_TIPO_SYNC;

  etiquetaResultado(resultado: ResultadoEventoSyncWoo): string {
    if (resultado === 'OK') {
      return 'OK';
    }
    return resultado === 'REINTENTO' ? 'Reintento' : 'Error';
  }
}
