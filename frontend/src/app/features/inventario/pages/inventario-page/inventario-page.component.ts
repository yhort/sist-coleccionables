import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { readApiError } from '../../../../core/http/api-error';
import {
  TAMANO_PAGINA_TABLA_DEFECTO,
  TamanoPaginaTabla,
  esTamanoPaginaTabla,
} from '../../../../shared/ui/tabla-paginacion/paginacion';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import { AjusteStockDialogComponent } from '../../components/ajuste-stock-dialog/ajuste-stock-dialog.component';
import { IngresoCompraDialogComponent } from '../../components/ingreso-compra-dialog/ingreso-compra-dialog.component';
import { KardexFiltersComponent } from '../../components/kardex-filters/kardex-filters.component';
import { KardexHistorialComponent } from '../../components/kardex-historial/kardex-historial.component';
import { StockSedeTableComponent } from '../../components/stock-sede-table/stock-sede-table.component';
import { KardexApiService } from '../../data-access/kardex.service';
import { StockApiService } from '../../data-access/stock.service';
import { FILTROS_KARDEX_VACIOS, KardexFiltros, StockFila } from '../../models/inventario.model';

@Component({
  selector: 'app-inventario-page',
  imports: [
    DecimalPipe,
    FormsModule,
    AjusteStockDialogComponent,
    IngresoCompraDialogComponent,
    KardexFiltersComponent,
    KardexHistorialComponent,
    StockSedeTableComponent,
  ],
  templateUrl: './inventario-page.component.html',
  styleUrl: './inventario-page.component.scss',
})
export class InventarioPageComponent {
  private readonly router = inject(Router);
  private readonly stockApi = inject(StockApiService);
  private readonly kardexApi = inject(KardexApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);

  readonly sedes = this.sedesApi.sedes;
  readonly productos = this.productosApi.productos;
  readonly productosActivos = computed(() => this.productos().filter((p) => p.activo));
  readonly error = signal('');

  readonly sedeId = signal('');
  readonly busquedaStock = signal('');
  readonly busquedaConsulta = signal('');
  readonly pageSize = signal<TamanoPaginaTabla>(TAMANO_PAGINA_TABLA_DEFECTO);
  readonly pagina = linkedSignal({
    source: () => ({
      sedeId: this.sedeId(),
      busqueda: this.busquedaConsulta(),
      pageSize: this.pageSize(),
    }),
    computation: () => 1,
  });
  readonly totalStock = signal(0);
  readonly filasStock = signal<StockFila[]>([]);
  readonly filtrosKardex = signal<KardexFiltros>({ ...FILTROS_KARDEX_VACIOS });
  readonly ajusteAbierto = signal(false);
  readonly compraAbierta = signal(false);
  readonly stockAjuste = signal<StockFila | null>(null);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly tab = computed(() => (this.url().includes('/kardex') ? 'kardex' : 'stock'));

  readonly resumen = signal({
    skus: 0,
    disponible: 0,
    reservado: 0,
    libre: 0,
  });

  readonly movimientos = computed(() => {
    this.kardexApi.filas();
    return this.kardexApi.listar(this.filtrosKardex());
  });
  readonly sedeActual = computed(
    () => this.sedes().find((sede) => sede.id === this.sedeId())?.nombre ?? '',
  );

  constructor() {
    void this.cargarInicial();
    effect((onCleanup) => {
      const busqueda = this.busquedaStock();
      const handle = setTimeout(() => this.busquedaConsulta.set(busqueda), 300);
      onCleanup(() => clearTimeout(handle));
    });
    effect(() => {
      const sedeId = this.sedeId();
      const busqueda = this.busquedaConsulta();
      const page = this.pagina();
      const pageSize = this.pageSize();
      if (sedeId) {
        void this.cargarStock(sedeId, busqueda, page, pageSize);
      }
    });
    effect((onCleanup) => {
      const filtros = this.filtrosKardex();
      this.tab();
      const handle = setTimeout(() => {
        void this.kardexApi.refrescar(filtros).catch((err) => this.error.set(readApiError(err)));
      }, 250);
      onCleanup(() => clearTimeout(handle));
    });
  }

  private async cargarInicial(): Promise<void> {
    this.error.set('');
    try {
      await this.productosApi.refrescar();
      const sedes = await this.sedesApi.refrescar();
      if (!this.sedeId() && sedes[0]) {
        this.sedeId.set(sedes[0].id);
      }
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  private cargaStockSeq = 0;

  private async cargarStock(
    sedeId: string,
    busqueda: string,
    page: number,
    pageSize: number,
  ): Promise<void> {
    const seq = ++this.cargaStockSeq;
    this.error.set('');
    try {
      const result = await this.stockApi.consultarPagina(sedeId, busqueda, page, pageSize);
      if (seq !== this.cargaStockSeq) {
        return;
      }
      this.filasStock.set(result.items);
      this.totalStock.set(result.total);
      this.resumen.set({
        skus: result.resumen.skus,
        disponible: result.resumen.disponible,
        reservado: result.resumen.reservado,
        libre: result.resumen.libre,
      });
    } catch (err) {
      if (seq !== this.cargaStockSeq) {
        return;
      }
      this.error.set(readApiError(err));
    }
  }

  irA(tab: 'stock' | 'kardex'): void {
    void this.router.navigateByUrl(tab === 'kardex' ? '/app/inventario/kardex' : '/app/inventario');
  }

  cambiarPagina(page: number): void {
    this.pagina.set(page);
  }

  cambiarTamano(pageSize: number): void {
    if (esTamanoPaginaTabla(pageSize)) {
      this.pageSize.set(pageSize);
    }
  }

  abrirAjuste(fila?: StockFila): void {
    this.stockAjuste.set(fila ?? null);
    void this.prepararAjuste();
  }

  private async prepararAjuste(): Promise<void> {
    this.error.set('');
    try {
      await this.productosApi.refrescar();
      this.ajusteAbierto.set(true);
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  abrirCompra(): void {
    void this.productosApi.refrescar()
      .then(() => this.compraAbierta.set(true))
      .catch((err) => this.error.set(readApiError(err)));
  }

  cerrarCompra(): void {
    this.compraAbierta.set(false);
    void this.recargarListas();
  }

  cerrarAjuste(): void {
    this.ajusteAbierto.set(false);
    this.stockAjuste.set(null);
    void this.recargarListas();
  }

  private async recargarListas(): Promise<void> {
    const sedeId = this.sedeId();
    if (sedeId) {
      await this.cargarStock(sedeId, this.busquedaConsulta(), this.pagina(), this.pageSize()).catch(
        (err) => this.error.set(readApiError(err)),
      );
    }
    void this.kardexApi.refrescar(this.filtrosKardex()).catch((err) => this.error.set(readApiError(err)));
  }
}
