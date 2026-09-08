import { Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  TAMANOS_PAGINA_TABLA,
  paginasVisibles,
  rangoPaginaDe,
  totalPaginasDe,
} from './paginacion';

@Component({
  selector: 'app-tabla-paginacion',
  imports: [FormsModule],
  templateUrl: './tabla-paginacion.component.html',
  styleUrl: './tabla-paginacion.component.scss',
})
export class TablaPaginacionComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly tamanos = TAMANOS_PAGINA_TABLA;

  readonly totalPaginas = computed(() => totalPaginasDe(this.total(), this.pageSize()));
  readonly paginaActual = computed(() =>
    Math.min(Math.max(1, this.page()), this.totalPaginas()),
  );
  readonly rango = computed(() =>
    rangoPaginaDe(this.paginaActual(), this.pageSize(), this.total()),
  );
  readonly paginas = computed(() => paginasVisibles(this.paginaActual(), this.totalPaginas()));

  irA(pagina: number): void {
    const destino = Math.min(Math.max(1, pagina), this.totalPaginas());
    if (destino !== this.paginaActual()) {
      this.pageChange.emit(destino);
    }
  }

  cambiarTamano(valor: string | number): void {
    const tamano = Number(valor);
    if (Number.isFinite(tamano) && tamano > 0) {
      this.pageSizeChange.emit(tamano);
    }
  }
}
