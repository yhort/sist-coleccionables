import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';

import { FILTROS_PRODUCTOS_VACIOS, ProductoTcg, ProductosTcgFiltros, estadoStockDe } from '../../models/producto-tcg.model';
import { ProductosTcgApiService } from '../../data-access/productos-tcg.service';
import { ProductosTcgFiltersComponent } from '../../components/productos-tcg-filters/productos-tcg-filters.component';
import {
  ProductosTcgTableComponent,
  VistaCatalogo,
} from '../../components/productos-tcg-table/productos-tcg-table.component';
import { ProductoTcgFormDialogComponent } from '../../components/producto-tcg-form-dialog/producto-tcg-form-dialog.component';
import { CatalogoTcgPanelComponent } from '../../../catalogo-tcg/components/catalogo-tcg-panel/catalogo-tcg-panel.component';
import { StockApiService } from '../../../inventario/data-access/stock.service';
import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { readApiError } from '../../../../core/http/api-error';
import {
  TAMANO_PAGINA_TABLA_DEFECTO,
  TamanoPaginaTabla,
  esTamanoPaginaTabla,
} from '../../../../shared/ui/tabla-paginacion/paginacion';

type TabProductos = 'skus' | 'catalogo';

@Component({
  selector: 'app-productos-tcg-page',
  imports: [
    ProductosTcgFiltersComponent,
    ProductosTcgTableComponent,
    ProductoTcgFormDialogComponent,
    CatalogoTcgPanelComponent,
  ],
  templateUrl: './productos-tcg-page.component.html',
  styleUrl: './productos-tcg-page.component.scss',
})
export class ProductosTcgPageComponent {
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly stockApi = inject(StockApiService);
  private readonly sedesApi = inject(SedesApiService);

  readonly filtros = signal<ProductosTcgFiltros>({ ...FILTROS_PRODUCTOS_VACIOS });
  readonly filtrosConsulta = signal<ProductosTcgFiltros>({ ...FILTROS_PRODUCTOS_VACIOS });
  readonly pageSize = signal<TamanoPaginaTabla>(TAMANO_PAGINA_TABLA_DEFECTO);
  readonly pagina = linkedSignal({
    source: () => ({ filtros: this.filtrosConsulta(), pageSize: this.pageSize() }),
    computation: () => 1,
  });
  readonly total = signal(0);
  readonly items = signal<ProductoTcg[]>([]);
  readonly vista = signal<VistaCatalogo>('tabla');
  readonly tab = signal<TabProductos>('skus');
  readonly formularioAbierto = signal(false);
  readonly productoEditando = signal<ProductoTcg | null>(null);
  readonly error = signal('');

  readonly productosFiltrados = computed(() => {
    const filtros = this.filtrosConsulta();
    const stockPorProducto = new Map(
      this.stockApi.filas().map((fila) => [fila.productoId, fila.cantidadLibre]),
    );
    return this.items()
      .map((producto) => {
        const desdeApi = stockPorProducto.get(producto.id);
        // Si la fila de stock aún no llegó, conservar el valor del ítem (p.ej. stockLibre del alta).
        const stockLocal = desdeApi === undefined ? producto.stockLocal : desdeApi;
        return { ...producto, stockLocal };
      })
      .filter((producto) => {
        if (
          filtros.estadoStock !== 'TODOS' &&
          estadoStockDe(producto.stockLocal) !== filtros.estadoStock
        ) {
          return false;
        }
        if (
          filtros.sincronizacionWoo !== 'TODOS' &&
          producto.woo.estadoSincronizacion !== filtros.sincronizacionWoo
        ) {
          return false;
        }
        return true;
      });
  });
  readonly catalogo = this.productosApi.productos;
  readonly totalFiltrado = this.total;

  constructor() {
    effect((onCleanup) => {
      const filtros = this.filtros();
      const handle = setTimeout(() => {
        const actual = this.filtrosConsulta();
        if (
          actual.busqueda !== filtros.busqueda ||
          actual.tipoProducto !== filtros.tipoProducto ||
          actual.juego !== filtros.juego ||
          actual.estadoStock !== filtros.estadoStock ||
          actual.sincronizacionWoo !== filtros.sincronizacionWoo
        ) {
          this.filtrosConsulta.set({ ...filtros });
        }
      }, 300);
      onCleanup(() => clearTimeout(handle));
    });
    effect(() => {
      const filtros = this.filtrosConsulta();
      const page = this.pagina();
      const pageSize = this.pageSize();
      void this.cargarPagina(filtros, page, pageSize);
    });
    void this.cargarStockSede();
  }

  private cargaSeq = 0;

  private async cargarPagina(
    filtros: ProductosTcgFiltros,
    page: number,
    pageSize: number,
  ): Promise<void> {
    const seq = ++this.cargaSeq;
    this.error.set('');
    try {
      const result = await this.productosApi.consultarPagina(filtros, page, pageSize);
      if (seq !== this.cargaSeq) {
        return;
      }
      this.items.set(result.items);
      this.total.set(result.total);
    } catch (err) {
      if (seq !== this.cargaSeq) {
        return;
      }
      this.error.set(readApiError(err));
    }
  }

  private async cargarStockSede(sedePreferida?: string | null): Promise<void> {
    try {
      const sedes = this.sedesApi.sedes().length > 0 ? this.sedesApi.sedes() : await this.sedesApi.refrescar();
      const sedeId = sedePreferida || sedes[0]?.id;
      if (sedeId) {
        await this.stockApi.refrescarSede(sedeId);
      }
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  cambiarPagina(page: number): void {
    this.pagina.set(page);
  }

  cambiarTamano(pageSize: number): void {
    if (esTamanoPaginaTabla(pageSize)) {
      this.pageSize.set(pageSize);
    }
  }

  abrirAlta(): void {
    this.productoEditando.set(null);
    this.formularioAbierto.set(true);
    if (this.productosApi.productos().length < this.total()) {
      void this.productosApi.refrescar();
    }
  }

  abrirEdicion(producto: ProductoTcg): void {
    this.productoEditando.set(producto);
    this.formularioAbierto.set(true);
    if (this.productosApi.productos().length < this.total()) {
      void this.productosApi.refrescar();
    }
  }

  cerrarFormulario(): void {
    this.formularioAbierto.set(false);
    this.productoEditando.set(null);
  }

  async onGuardado(producto?: ProductoTcg): Promise<void> {
    this.cerrarFormulario();
    const sedePreferida = this.stockApi.sedeActiva();
    await this.cargarStockSede(sedePreferida);
    if (producto && producto.stockLocal > 0) {
      const sedeId = sedePreferida || this.sedesApi.sedes()[0]?.id;
      if (sedeId) {
        await this.stockApi
          .confirmarStockProducto(producto.id, sedeId, producto.stockLocal)
          .catch(() => undefined);
      }
    }
    await this.cargarPagina(this.filtrosConsulta(), this.pagina(), this.pageSize());
    if (producto) {
      const libre =
        this.stockApi.filas().find((fila) => fila.productoId === producto.id)?.cantidadLibre ??
        producto.stockLocal;
      this.items.update((items) => {
        const hay = items.some((item) => item.id === producto.id);
        const actualizado = { ...producto, stockLocal: libre };
        return hay
          ? items.map((item) => (item.id === producto.id ? { ...item, stockLocal: libre } : item))
          : [actualizado, ...items];
      });
    }
  }

  abrirCatalogo(): void {
    this.tab.set('catalogo');
    if (this.productosApi.productos().length < this.total()) {
      void this.productosApi.refrescar();
    }
  }

  onVarianteCreada(): void {
    void this.cargarStockSede();
    void this.cargarPagina(this.filtrosConsulta(), this.pagina(), this.pageSize());
  }
}
