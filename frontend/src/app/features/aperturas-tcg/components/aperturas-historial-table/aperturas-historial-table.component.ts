import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';

import {
  AperturaTcg,
  ETIQUETAS_ESTADO_APERTURA,
  EstadoAperturaTcg,
  RendimientoApertura,
  totalCartasObtenidas,
} from '../../models/apertura-tcg.model';

export interface AperturaHistorialFila {
  apertura: AperturaTcg;
  sedeNombre: string;
  productoSelladoNombre: string;
  rendimiento: RendimientoApertura;
}

const PAGE_SIZE = 40;

@Component({
  selector: 'app-aperturas-historial-table',
  imports: [DatePipe, DecimalPipe, NgClass],
  templateUrl: './aperturas-historial-table.component.html',
  styleUrl: './aperturas-historial-table.component.scss',
})
export class AperturasHistorialTableComponent {
  readonly filas = input.required<AperturaHistorialFila[]>();
  readonly continuar = output<AperturaTcg>();
  readonly anular = output<AperturaTcg>();

  readonly etiquetas = ETIQUETAS_ESTADO_APERTURA;
  readonly page = signal(1);

  totalCartas = totalCartasObtenidas;

  constructor() {
    effect(() => {
      this.filas();
      this.page.set(1);
    });
  }

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filas().length / PAGE_SIZE)),
  );

  readonly paginaActual = computed(() => Math.min(this.page(), this.totalPages()));

  readonly pageItems = computed(() => {
    const pagina = this.paginaActual();
    const inicio = (pagina - 1) * PAGE_SIZE;
    return this.filas().slice(inicio, inicio + PAGE_SIZE);
  });

  readonly rango = computed(() => {
    const total = this.filas().length;
    if (total === 0) {
      return { desde: 0, hasta: 0, total: 0 };
    }
    const pagina = this.paginaActual();
    const desde = (pagina - 1) * PAGE_SIZE + 1;
    const hasta = Math.min(pagina * PAGE_SIZE, total);
    return { desde, hasta, total };
  });

  claseEstado(estado: EstadoAperturaTcg): string {
    return `estado--${estado.toLowerCase()}`;
  }

  paginaAnterior(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  paginaSiguiente(): void {
    this.page.update((p) => Math.min(this.totalPages(), p + 1));
  }
}
