import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { ETIQUETAS_TIPO, TipoProductoTcg } from '../../../productos-tcg/models/producto-tcg.model';
import {
  ETIQUETAS_CANAL_SUBASTA,
  SubastaTcg,
  subastaVencida,
  ultimaPuja,
} from '../../models/subasta-tcg.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

export interface SubastaTableroItem {
  subasta: SubastaTcg;
  productoNombre: string;
  tipoProducto: TipoProductoTcg;
  sedeNombre: string;
  stockLibre: number;
}

@Component({
  selector: 'app-subasta-card',
  imports: [SolesPipe, DatePipe],
  templateUrl: './subasta-card.component.html',
  styleUrl: './subasta-card.component.scss',
})
export class SubastaCardComponent {
  readonly item = input.required<SubastaTableroItem>();
  readonly ver = output<SubastaTcg>();
  readonly pujar = output<SubastaTcg>();

  readonly etiquetasCanal = ETIQUETAS_CANAL_SUBASTA;
  readonly etiquetasTipo = ETIQUETAS_TIPO;

  ultima = ultimaPuja;
  vencida = () => subastaVencida(this.item().subasta);
}
