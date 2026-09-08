import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { SedeInventario } from '../../../inventario/models/inventario.model';
import {
  ESTADOS_LOGISTICA,
  ETIQUETAS_ESTADO_LOGISTICA,
  ETIQUETAS_METODO_ENVIO,
  EntregasFiltros,
  METODOS_ENVIO,
} from '../../models/entrega.model';

@Component({
  selector: 'app-entregas-filters',
  imports: [FormsModule],
  templateUrl: './entregas-filters.component.html',
  styleUrl: './entregas-filters.component.scss',
})
export class EntregasFiltersComponent {
  readonly filtros = input.required<EntregasFiltros>();
  readonly sedes = input.required<readonly SedeInventario[]>();
  readonly changed = output<EntregasFiltros>();
  readonly cleared = output<void>();

  readonly metodos = METODOS_ENVIO;
  readonly estados = ESTADOS_LOGISTICA;
  readonly etiquetasMetodo = ETIQUETAS_METODO_ENVIO;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_LOGISTICA;

  actualizar<K extends keyof EntregasFiltros>(clave: K, valor: EntregasFiltros[K]): void {
    this.changed.emit({ ...this.filtros(), [clave]: valor });
  }
}
