import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { readApiError } from '../../../../core/http/api-error';
import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { StockApiService } from '../../../inventario/data-access/stock.service';
import { PedidosDigitalesApiService } from '../../../pedidos-digitales/data-access/pedidos-digitales.service';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import { SubastaDetallePanelComponent } from '../../components/subasta-detalle-panel/subasta-detalle-panel.component';
import { SubastasTcgApiService } from '../../data-access/subastas-tcg.service';

@Component({
  selector: 'app-subasta-detalle-page',
  imports: [RouterLink, SubastaDetallePanelComponent],
  templateUrl: './subasta-detalle-page.component.html',
  styleUrl: './subasta-detalle-page.component.scss',
})
export class SubastaDetallePageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly subastasApi = inject(SubastasTcgApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly stockApi = inject(StockApiService);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);

  readonly error = signal('');

  readonly subastaId = toSignal(
    this.route.paramMap.pipe(map((params) => params.get('id') ?? '')),
    { initialValue: this.route.snapshot.paramMap.get('id') ?? '' },
  );

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    try {
      await Promise.all([
        this.productosApi.refrescar(),
        this.sedesApi.refrescar(),
        this.subastasApi.refrescar(),
        this.pedidosApi.refrescar().catch(() => undefined),
      ]);
      const sedeId = this.sedesApi.sedes()[0]?.id;
      if (sedeId) {
        await this.stockApi.refrescarSede(sedeId);
      }
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }
}
