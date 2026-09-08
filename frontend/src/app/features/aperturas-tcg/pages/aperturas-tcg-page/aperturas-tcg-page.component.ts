import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { StockApiService } from '../../../inventario/data-access/stock.service';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import { AperturaWizardComponent } from '../../components/apertura-wizard/apertura-wizard.component';
import {
  AperturaHistorialFila,
  AperturasHistorialTableComponent,
} from '../../components/aperturas-historial-table/aperturas-historial-table.component';
import { AperturaYieldCardComponent } from '../../components/apertura-yield-card/apertura-yield-card.component';
import { AperturasTcgApiService } from '../../data-access/aperturas-tcg.service';
import {
  AperturaTcg,
  AperturaTcgFiltros,
  ETIQUETAS_ESTADO_APERTURA,
  EstadoAperturaTcg,
  FILTROS_APERTURAS_VACIOS,
  calcularRendimiento,
  totalCartasObtenidas,
} from '../../models/apertura-tcg.model';

@Component({
  selector: 'app-aperturas-tcg-page',
  imports: [
    DecimalPipe,
    FormsModule,
    AperturaWizardComponent,
    AperturasHistorialTableComponent,
    AperturaYieldCardComponent,
  ],
  templateUrl: './aperturas-tcg-page.component.html',
  styleUrl: './aperturas-tcg-page.component.scss',
})
export class AperturasTcgPageComponent {
  private readonly aperturasApi = inject(AperturasTcgApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly stockApi = inject(StockApiService);

  readonly sedes = this.aperturasApi.sedes;
  readonly estados = ETIQUETAS_ESTADO_APERTURA;
  readonly estadosOpciones: readonly EstadoAperturaTcg[] = ['BORRADOR', 'CONFIRMADA', 'ANULADA'];
  readonly filtros = signal<AperturaTcgFiltros>({ ...FILTROS_APERTURAS_VACIOS });
  readonly wizardAbierto = signal(false);
  readonly aperturaEnCurso = signal<string | null>(null);
  readonly error = signal('');

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    try {
      await Promise.all([
        this.productosApi.refrescar(),
        this.sedesApi.refrescar(),
        this.aperturasApi.refrescar(),
      ]);
      const sedeId = this.sedesApi.sedes()[0]?.id;
      if (sedeId) {
        await this.stockApi.refrescarSede(sedeId);
      }
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  readonly filas = computed<AperturaHistorialFila[]>(() => {
    this.aperturasApi.aperturas();
    return this.aperturasApi.listar(this.filtros()).map((apertura) => ({
      apertura,
      sedeNombre:
        apertura.sedeNombre ||
        this.sedes().find((sede) => sede.id === apertura.sedeId)?.nombre ||
        apertura.sedeId,
      productoSelladoNombre:
        apertura.productoSelladoNombre ||
        this.productosApi.obtenerPorId(apertura.productoSelladoId)?.nombre ||
        'Sellado',
      rendimiento: this.aperturasApi.rendimientoDe(apertura),
    }));
  });

  readonly resumen = computed(() => {
    const filas = this.filas();
    const confirmadas = filas.filter((fila) => fila.apertura.estado === 'CONFIRMADA');
    const cartas = confirmadas.reduce((sum, fila) => sum + totalCartasObtenidas(fila.apertura), 0);
    const rendimiento = calcularRendimiento(
      confirmadas.reduce((sum, fila) => sum + fila.rendimiento.costoSellado, 0),
      confirmadas.reduce((sum, fila) => sum + fila.rendimiento.valorEstimadoCartas, 0),
    );
    return {
      total: filas.length,
      confirmadas: confirmadas.length,
      cartas,
      rendimiento,
    };
  });

  actualizarFiltro<K extends keyof AperturaTcgFiltros>(clave: K, valor: AperturaTcgFiltros[K]): void {
    this.filtros.update((actual) => ({ ...actual, [clave]: valor }));
  }

  limpiarFiltros(): void {
    this.filtros.set({ ...FILTROS_APERTURAS_VACIOS });
  }

  abrirWizard(apertura?: AperturaTcg): void {
    this.error.set('');
    this.aperturaEnCurso.set(apertura?.id ?? null);
    this.wizardAbierto.set(true);
  }

  cerrarWizard(): void {
    this.wizardAbierto.set(false);
    this.aperturaEnCurso.set(null);
  }

  onGuardado(): void {
    this.cerrarWizard();
  }

  async anular(apertura: AperturaTcg): Promise<void> {
    this.error.set('');
    const etiqueta = apertura.estado === 'BORRADOR' ? 'descartar el borrador' : 'anular la apertura';
    if (!globalThis.confirm(`¿Confirmas ${etiqueta}? Esta acción queda registrada en el kardex si ya estaba confirmada.`)) {
      return;
    }
    try {
      await this.aperturasApi.anular(apertura.id);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo anular la apertura.');
    }
  }
}
