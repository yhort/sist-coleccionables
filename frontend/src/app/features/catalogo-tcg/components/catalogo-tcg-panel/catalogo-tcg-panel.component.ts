import { Component, DestroyRef, computed, inject, output, signal } from '@angular/core';

import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { readApiError } from '../../../../core/http/api-error';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import {
  ETIQUETAS_RAREZA,
  ETIQUETAS_TIPO_CARTA,
} from '../../../productos-tcg/models/producto-tcg.model';
import { CatalogoTcgApiService } from '../../data-access/catalogo-tcg.service';
import { ImportarSetTcgResponse, TcgCarta } from '../../models/catalogo-tcg.model';
import { ImportarCatalogoDialogComponent } from '../importar-catalogo-dialog/importar-catalogo-dialog.component';
import { VarianteSkuDialogComponent } from '../variante-sku-dialog/variante-sku-dialog.component';

@Component({
  selector: 'app-catalogo-tcg-panel',
  imports: [VarianteSkuDialogComponent, ImportarCatalogoDialogComponent],
  templateUrl: './catalogo-tcg-panel.component.html',
  styleUrl: './catalogo-tcg-panel.component.scss',
})
export class CatalogoTcgPanelComponent {
  private readonly catalogoApi = inject(CatalogoTcgApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly destroyRef = inject(DestroyRef);
  private toastTimer: ReturnType<typeof setTimeout> | null = null;

  readonly varianteCreada = output<void>();

  readonly series = this.catalogoApi.series;
  readonly sets = this.catalogoApi.sets;
  readonly cartas = this.catalogoApi.cartas;
  readonly etiquetasRareza = ETIQUETAS_RAREZA;
  readonly etiquetasTipo = ETIQUETAS_TIPO_CARTA;

  readonly serieId = signal('');
  readonly setId = signal('');
  readonly busquedaFicha = signal('');
  readonly error = signal('');
  readonly toastExito = signal('');
  readonly cargando = signal(false);
  readonly importadorAbierto = signal(false);
  readonly fichaVariante = signal<TcgCarta | null>(null);

  readonly cartasFiltradas = computed(() => {
    const q = this.busquedaFicha().trim().toLowerCase();
    const items = this.cartas();
    if (!q) {
      return items;
    }
    return items.filter((carta) =>
      [carta.numero, carta.nombre, carta.rareza, carta.tipoCarta]
        .join(' ')
        .toLowerCase()
        .includes(q),
    );
  });

  readonly serieSeleccionada = computed(
    () => this.series().find((item) => item.id === this.serieId()) ?? null,
  );
  readonly setSeleccionado = computed(
    () => this.sets().find((item) => item.id === this.setId()) ?? null,
  );

  constructor() {
    this.destroyRef.onDestroy(() => this.limpiarToast());
    void this.inicializar();
  }

  skusDeFicha(fichaId: string): number {
    return this.productosApi.productos().filter((item) => item.cartaCatalogoId === fichaId).length;
  }

  async onSerieChange(serieId: string): Promise<void> {
    this.serieId.set(serieId);
    this.setId.set('');
    this.error.set('');
    if (!serieId) {
      return;
    }
    this.cargando.set(true);
    try {
      const sets = await this.catalogoApi.listarSets(serieId);
      if (sets[0]) {
        await this.onSetChange(sets[0].id);
      }
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.cargando.set(false);
    }
  }

  async onSetChange(setId: string): Promise<void> {
    this.setId.set(setId);
    this.error.set('');
    if (!setId) {
      return;
    }
    this.cargando.set(true);
    try {
      await this.catalogoApi.listarCartas(setId);
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.cargando.set(false);
    }
  }

  async onCatalogoImportado(resultado: ImportarSetTcgResponse): Promise<void> {
    this.importadorAbierto.set(false);
    this.error.set('');
    this.mostrarToast(
      `¡Catálogo importado con éxito: ${resultado.cartasCreadas} fichas creadas!`,
    );
    this.serieId.set(resultado.serie.id);
    await this.catalogoApi.listarSets(resultado.serie.id);
    this.setId.set(resultado.set.id);
    await this.catalogoApi.listarCartas(resultado.set.id);
  }

  private mostrarToast(mensaje: string): void {
    this.limpiarToast();
    this.toastExito.set(mensaje);
    this.toastTimer = setTimeout(() => {
      this.toastExito.set('');
      this.toastTimer = null;
    }, 5000);
  }

  private limpiarToast(): void {
    if (this.toastTimer) {
      clearTimeout(this.toastTimer);
      this.toastTimer = null;
    }
  }

  abrirVariante(ficha: TcgCarta): void {
    this.fichaVariante.set(ficha);
  }

  onVarianteGuardada(): void {
    this.fichaVariante.set(null);
    this.varianteCreada.emit();
  }

  private async inicializar(): Promise<void> {
    this.cargando.set(true);
    try {
      if (this.sedesApi.sedes().length === 0) {
        await this.sedesApi.refrescar();
      }
      const series = await this.catalogoApi.listarSeries();
      const mega = series.find((item) => item.codigo === 'MEGA') ?? series[0];
      if (mega) {
        await this.onSerieChange(mega.id);
      }
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.cargando.set(false);
    }
  }
}
