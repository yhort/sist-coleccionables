import { Component, HostListener, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { StockApiService } from '../../../inventario/data-access/stock.service';
import {
  CondicionTcg,
  ETIQUETAS_CONDICION,
  ETIQUETAS_IDIOMA,
  IdiomaTcg,
} from '../../../productos-tcg/models/producto-tcg.model';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import { TcgCarta } from '../../models/catalogo-tcg.model';

@Component({
  selector: 'app-variante-sku-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './variante-sku-dialog.component.html',
  styleUrl: './variante-sku-dialog.component.scss',
})
export class VarianteSkuDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly stockApi = inject(StockApiService);

  readonly ficha = input.required<TcgCarta>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly guardando = signal(false);
  readonly sedes = this.sedesApi.sedes;
  readonly idiomas = Object.entries(ETIQUETAS_IDIOMA) as [IdiomaTcg, string][];
  readonly condiciones = Object.entries(ETIQUETAS_CONDICION) as [CondicionTcg, string][];

  readonly form = this.fb.nonNullable.group({
    esFoil: [false],
    condicion: this.fb.nonNullable.control<CondicionTcg>('NM'),
    idioma: this.fb.nonNullable.control<IdiomaTcg>('EN'),
    precioVenta: [0, [Validators.required, Validators.min(0)]],
    costo: this.fb.control<number | null>(null),
    codigoSku: [''],
    sedeId: [this.sedes()[0]?.id ?? ''],
    stockInicial: [0, [Validators.required, Validators.min(0)]],
  });

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue();
    if (raw.stockInicial > 0 && !raw.sedeId) {
      this.error.set('Elige una sede para el stock inicial.');
      return;
    }
    this.guardando.set(true);
    try {
      await this.productosApi.crearVariante({
        cartaCatalogoId: this.ficha().id,
        esFoil: raw.esFoil,
        condicion: raw.condicion,
        idioma: raw.idioma,
        precioVenta: Number(raw.precioVenta) || 0,
        costo: raw.costo,
        codigoSku: raw.codigoSku.trim() || null,
        sedeId: raw.stockInicial > 0 ? raw.sedeId : null,
        stockInicial: Number(raw.stockInicial) || 0,
        tipoIngresoStock: 'AJUSTE',
      });
      if (raw.sedeId && raw.stockInicial > 0) {
        await this.stockApi.refrescarSede(raw.sedeId);
      }
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo crear la variante.');
    } finally {
      this.guardando.set(false);
    }
  }
}
