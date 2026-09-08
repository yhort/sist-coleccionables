import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  ESTADOS_PAGO,
  ETIQUETAS_ESTADO_PAGO,
  ETIQUETAS_ORIGEN_PAGO,
  ORIGENES_PAGO,
  PagosFiltros,
} from '../../models/pago.model';

@Component({
  selector: 'app-pagos-filters',
  imports: [FormsModule],
  templateUrl: './pagos-filters.component.html',
  styleUrl: './pagos-filters.component.scss',
})
export class PagosFiltersComponent {
  readonly filtros = input.required<PagosFiltros>();
  readonly changed = output<PagosFiltros>();
  readonly cleared = output<void>();

  readonly origenes = ORIGENES_PAGO;
  readonly estados = ESTADOS_PAGO;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PAGO;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_PAGO;

  actualizar<K extends keyof PagosFiltros>(clave: K, valor: PagosFiltros[K]): void {
    this.changed.emit({ ...this.filtros(), [clave]: valor });
  }

  actualizarMonto(clave: 'montoMin' | 'montoMax', valor: string | number | null): void {
    const numero = valor === '' || valor === null ? null : Number(valor);
    this.actualizar(clave, numero !== null && Number.isFinite(numero) ? numero : null);
  }
}
