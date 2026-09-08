import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, input, output } from '@angular/core';

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

  totalCartas = totalCartasObtenidas;

  claseEstado(estado: EstadoAperturaTcg): string {
    return `estado--${estado.toLowerCase()}`;
  }
}
