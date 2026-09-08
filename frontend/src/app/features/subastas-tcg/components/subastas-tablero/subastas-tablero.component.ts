import { Component, input, output } from '@angular/core';

import {
  ESTADOS_TABLERO_SUBASTA,
  ETIQUETAS_ESTADO_SUBASTA,
  EstadoSubastaTcg,
  SubastaTcg,
} from '../../models/subasta-tcg.model';
import { SubastaCardComponent, SubastaTableroItem } from '../subasta-card/subasta-card.component';

@Component({
  selector: 'app-subastas-tablero',
  imports: [SubastaCardComponent],
  templateUrl: './subastas-tablero.component.html',
  styleUrl: './subastas-tablero.component.scss',
})
export class SubastasTableroComponent {
  readonly items = input.required<SubastaTableroItem[]>();
  readonly ver = output<SubastaTcg>();
  readonly pujar = output<SubastaTcg>();

  readonly columnas = ESTADOS_TABLERO_SUBASTA;
  readonly etiquetas = ETIQUETAS_ESTADO_SUBASTA;

  itemsDe(estado: EstadoSubastaTcg): SubastaTableroItem[] {
    return this.items().filter((item) => item.subasta.estado === estado);
  }
}
