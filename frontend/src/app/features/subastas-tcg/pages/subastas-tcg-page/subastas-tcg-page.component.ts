import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { readApiError } from '../../../../core/http/api-error';
import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { StockApiService } from '../../../inventario/data-access/stock.service';
import { PedidosDigitalesApiService } from '../../../pedidos-digitales/data-access/pedidos-digitales.service';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import { ETIQUETAS_TIPO, TipoProductoTcg } from '../../../productos-tcg/models/producto-tcg.model';
import { RegistrarPujaDialogComponent } from '../../components/registrar-puja-dialog/registrar-puja-dialog.component';
import { SubastaFormDialogComponent } from '../../components/subasta-form-dialog/subasta-form-dialog.component';
import { SubastaDetallePanelComponent } from '../../components/subasta-detalle-panel/subasta-detalle-panel.component';
import { SubastaMargenCardComponent } from '../../components/subasta-margen-card/subasta-margen-card.component';
import { SubastaTableroItem } from '../../components/subasta-card/subasta-card.component';
import {
  PedidoVinculoSubasta,
  SubastasTablaComponent,
} from '../../components/subastas-tabla/subastas-tabla.component';
import { SubastasTableroComponent } from '../../components/subastas-tablero/subastas-tablero.component';
import { SubastasTcgApiService } from '../../data-access/subastas-tcg.service';
import {
  CANALES_SUBASTA,
  ETIQUETAS_CANAL_SUBASTA,
  FILTROS_SUBASTAS_VACIOS,
  SubastaTcg,
  SubastasTcgFiltros,
  calcularMargenSubasta,
  esEventoIndividuales,
  etiquetaLoteSubasta,
  subastaVencida,
} from '../../models/subasta-tcg.model';

const VISTA_STORAGE_KEY = 'subastas_view_mode';

export type SubastasVistaMode = 'kanban' | 'tabla';

function leerVistaPreferida(): SubastasVistaMode {
  try {
    const valor = localStorage.getItem(VISTA_STORAGE_KEY);
    return valor === 'tabla' ? 'tabla' : 'kanban';
  } catch {
    return 'kanban';
  }
}

@Component({
  selector: 'app-subastas-tcg-page',
  imports: [
    FormsModule,
    RegistrarPujaDialogComponent,
    SubastaFormDialogComponent,
    SubastaDetallePanelComponent,
    SubastaMargenCardComponent,
    SubastasTablaComponent,
    SubastasTableroComponent,
  ],
  templateUrl: './subastas-tcg-page.component.html',
  styleUrl: './subastas-tcg-page.component.scss',
})
export class SubastasTcgPageComponent {
  private readonly subastasApi = inject(SubastasTcgApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly stockApi = inject(StockApiService);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly sedes = this.subastasApi.sedes;
  readonly canales = CANALES_SUBASTA;
  readonly etiquetasCanal = ETIQUETAS_CANAL_SUBASTA;
  readonly tiposProducto: readonly TipoProductoTcg[] = ['CARTA', 'SELLADO', 'ACCESORIO', 'COMPUESTO'];
  readonly etiquetasTipo = ETIQUETAS_TIPO;

  readonly filtros = signal<SubastasTcgFiltros>({ ...FILTROS_SUBASTAS_VACIOS });
  readonly vista = signal<SubastasVistaMode>(leerVistaPreferida());
  readonly seleccionadaId = signal<string | null>(null);
  readonly pujaSubasta = signal<SubastaTcg | null>(null);
  readonly formAbierta = signal(false);
  readonly error = signal('');

  constructor() {
    void this.cargar().then(() => this.aplicarQueryParams());
  }

  async cargar(): Promise<void> {
    this.error.set('');
    try {
      await Promise.all([
        this.productosApi.refrescar(),
        this.sedesApi.refrescar(),
        this.subastasApi.refrescar(),
        this.pedidosApi.refrescar().catch(() => undefined),
      ]);
      const sedeId = this.sedesApi.sedes()[0]?.id;
      if (sedeId) {
        await this.stockApi.refrescarSede(sedeId);
      }
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  readonly items = computed<SubastaTableroItem[]>(() => {
    this.subastasApi.subastas();
    return this.subastasApi.listar(this.filtros()).map((subasta) => {
      const producto = this.subastasApi.productos().find((item) => item.id === subasta.productoId);
      return {
        subasta,
        productoNombre: etiquetaLoteSubasta(subasta),
        tipoProducto: producto?.tipoProducto ?? subasta.tipoProducto ?? 'ACCESORIO',
        sedeNombre:
          subasta.sedeNombre ||
          this.sedes().find((sede) => sede.id === subasta.sedeId)?.nombre ||
          subasta.sedeId,
        stockLibre: this.subastasApi.stockLibreLote(subasta),
      };
    });
  });

  readonly margenResumen = computed(() => {
    const adjudicadas = this.items()
      .map((item) => item.subasta)
      .filter((subasta) => subasta.estado === 'ADJUDICADA');
    const base = adjudicadas.reduce((sum, subasta) => sum + subasta.precioBase, 0);
    const ganadora = adjudicadas.reduce((sum, subasta) => {
      const puja = subasta.pujas.find((item) => item.id === subasta.pujaGanadoraId);
      return sum + (puja?.monto ?? 0);
    }, 0);
    return calcularMargenSubasta(base, adjudicadas.length === 0 ? null : ganadora);
  });

  readonly resumen = computed(() => {
    const items = this.items();
    return {
      total: items.length,
      activas: items.filter((item) => item.subasta.estado === 'ACTIVA').length,
      adjudicadas: items.filter((item) => item.subasta.estado === 'ADJUDICADA').length,
      pujas: items.reduce((sum, item) => sum + item.subasta.pujas.length, 0),
    };
  });

  setVista(mode: SubastasVistaMode): void {
    this.vista.set(mode);
    try {
      localStorage.setItem(VISTA_STORAGE_KEY, mode);
    } catch {
      /* ignore quota / private mode */
    }
  }

  actualizarFiltro<K extends keyof SubastasTcgFiltros>(
    clave: K,
    valor: SubastasTcgFiltros[K],
  ): void {
    this.filtros.update((actual) => ({ ...actual, [clave]: valor }));
  }

  limpiarFiltros(): void {
    this.filtros.set({ ...FILTROS_SUBASTAS_VACIOS });
  }

  abrirDetalle(subasta: SubastaTcg): void {
    this.error.set('');
    this.seleccionadaId.set(subasta.id);
  }

  cerrarDetalle(): void {
    this.seleccionadaId.set(null);
  }

  abrirAdjudicar(subasta: SubastaTcg): void {
    this.error.set('');
    this.seleccionadaId.set(subasta.id);
  }

  abrirSala(subasta: SubastaTcg): void {
    void this.router.navigate(['/app/subastas-tcg', subasta.id]);
  }

  editarSubasta(subasta: SubastaTcg): void {
    this.error.set('');
    this.seleccionadaId.set(subasta.id);
  }

  irAPedido(pedido: PedidoVinculoSubasta): void {
    void this.router.navigate(['/app/pedidos-digitales'], {
      queryParams: { pedidoId: pedido.id },
    });
  }

  async activarSubasta(subasta: SubastaTcg): Promise<void> {
    if (!globalThis.confirm(`¿Activar ${subasta.codigo}? Pasará a estado Activa.`)) {
      return;
    }
    this.error.set('');
    try {
      await this.subastasApi.activar(subasta.id);
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  async declararDesierta(subasta: SubastaTcg): Promise<void> {
    // En Cerrada o eventos individuales: abrir panel (carta / adjudicar / anular).
    if (esEventoIndividuales(subasta) || subasta.estado === 'CERRADA') {
      this.seleccionadaId.set(subasta.id);
      return;
    }
    if (!globalThis.confirm('¿Declarar desierta esta subasta? No se creará pedido.')) {
      return;
    }
    this.error.set('');
    try {
      await this.subastasApi.declararDesierta(subasta.id);
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  abrirPuja(subasta: SubastaTcg): void {
    this.error.set('');
    if (subastaVencida(subasta)) {
      this.error.set('La subasta ha finalizado y no acepta más pujas.');
      return;
    }
    // Eventos individuales: la puja es por carta dentro de la sala.
    if (subasta.modo === 'INDIVIDUALES') {
      this.seleccionadaId.set(subasta.id);
      return;
    }
    this.pujaSubasta.set(subasta);
  }

  cerrarPuja(): void {
    this.pujaSubasta.set(null);
  }

  onPujaGuardada(): void {
    this.cerrarPuja();
  }

  private aplicarQueryParams(): void {
    if (this.route.snapshot.queryParamMap.get('crear') !== '1') {
      return;
    }
    this.formAbierta.set(true);
    void this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }
}
