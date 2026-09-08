import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';

import { WooCommerceApiService } from '../../data-access/woocommerce.service';
import {
  ETIQUETAS_MAPEO_WOO,
  EstadoMapeoWoo,
} from '../../models/woocommerce.model';

@Component({
  selector: 'app-woo-mapeos-table',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './woo-mapeos-table.component.html',
  styleUrl: './woo-mapeos-table.component.scss',
})
export class WooMapeosTableComponent {
  readonly wooApi = inject(WooCommerceApiService);
  readonly etiquetas = ETIQUETAS_MAPEO_WOO;
  readonly filtro = signal<EstadoMapeoWoo | 'TODOS'>('TODOS');
  readonly estados: readonly (EstadoMapeoWoo | 'TODOS')[] = [
    'TODOS',
    'SINCRONIZADO',
    'DESFASADO',
    'PENDIENTE_SUBIDA',
    'NO_MAPEADO',
  ];

  readonly filas = computed(() => {
    const filtro = this.filtro();
    const mapeos = this.wooApi.mapeos();
    return filtro === 'TODOS' ? mapeos : mapeos.filter((fila) => fila.estadoMapeo === filtro);
  });

  etiquetaFiltro(estado: EstadoMapeoWoo | 'TODOS'): string {
    return estado === 'TODOS' ? 'Todos' : this.etiquetas[estado];
  }
}
