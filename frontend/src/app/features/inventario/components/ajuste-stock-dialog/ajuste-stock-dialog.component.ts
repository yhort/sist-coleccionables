import { DecimalPipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  HostListener,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';

import { ProductoTcg } from '../../../productos-tcg/models/producto-tcg.model';
import {
  AjusteInventarioRequest,
  ETIQUETAS_MOVIMIENTO,
  SedeInventario,
  SentidoAjuste,
  StockFila,
} from '../../models/inventario.model';
import { StockApiService } from '../../data-access/stock.service';

@Component({
  selector: 'app-ajuste-stock-dialog',
  imports: [FormsModule, ReactiveFormsModule, DecimalPipe],
  templateUrl: './ajuste-stock-dialog.component.html',
  styleUrl: './ajuste-stock-dialog.component.scss',
})
export class AjusteStockDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly stockApi = inject(StockApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly sedes = input.required<readonly SedeInventario[]>();
  readonly productos = input.required<readonly ProductoTcg[]>();
  readonly stockInicial = input<StockFila | null>(null);
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly etiquetas = ETIQUETAS_MOVIMIENTO;
  readonly error = signal('');
  readonly stockActual = signal<StockFila | null>(null);
  readonly busquedaProducto = signal('');

  readonly form = this.fb.nonNullable.group({
    sedeId: ['', Validators.required],
    productoId: ['', Validators.required],
    sentido: this.fb.nonNullable.control<SentidoAjuste>('ENTRADA'),
    cantidad: [1, [Validators.required, Validators.min(0.001)]],
    motivo: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
  });

  /** Catálogo activo + fila preseleccionada (productos nuevos sin stock previo). */
  readonly opcionesProducto = computed(() => {
    const q = this.busquedaProducto().trim().toLowerCase();
    const porId = new Map<string, { id: string; nombre: string; codigoSku: string }>();

    for (const producto of this.productos()) {
      if (!producto.activo) {
        continue;
      }
      porId.set(producto.id, {
        id: producto.id,
        nombre: producto.nombre,
        codigoSku: producto.codigoSku,
      });
    }

    const inicial = this.stockInicial();
    if (inicial && !porId.has(inicial.productoId)) {
      porId.set(inicial.productoId, {
        id: inicial.productoId,
        nombre: inicial.productoNombre,
        codigoSku: inicial.codigoSku,
      });
    }

    return [...porId.values()]
      .filter((item) => {
        if (!q) {
          return true;
        }
        return `${item.nombre} ${item.codigoSku}`.toLowerCase().includes(q);
      })
      .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  });

  ngOnInit(): void {
    const inicial = this.stockInicial();
    if (inicial) {
      this.form.patchValue({
        sedeId: inicial.sedeId,
        productoId: inicial.productoId,
        sentido: 'ENTRADA',
      });
      this.stockActual.set(inicial);
    } else if (this.sedes()[0]) {
      this.form.patchValue({ sedeId: this.sedes()[0].id });
    }

    this.form.controls.sedeId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refrescarStock());
    this.form.controls.productoId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refrescarStock());
  }

  get etiquetaEfecto(): string {
    return this.form.controls.sentido.value === 'ENTRADA'
      ? 'Entrada: suma al stock disponible (crea el registro de stock si el SKU es nuevo).'
      : 'Salida: descuenta del stock disponible.';
  }

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
    const request: AjusteInventarioRequest = {
      sedeId: raw.sedeId,
      productoId: raw.productoId,
      tipoMovimiento: 'AJUSTE',
      sentido: raw.sentido,
      cantidad: Number(raw.cantidad),
      motivo: raw.motivo.trim(),
    };

    try {
      await this.stockApi.ajustar(request);
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo registrar el ajuste.');
    }
  }

  private refrescarStock(): void {
    const sedeId = this.form.controls.sedeId.value;
    const productoId = this.form.controls.productoId.value;
    if (!sedeId || !productoId) {
      this.stockActual.set(null);
      return;
    }
    const fila = this.stockApi.obtener(sedeId, productoId);
    if (fila) {
      this.stockActual.set(fila);
      return;
    }
    const opcion = this.opcionesProducto().find((item) => item.id === productoId);
    const sedeNombre = this.sedes().find((s) => s.id === sedeId)?.nombre ?? '';
    this.stockActual.set(
      opcion
        ? {
            id: `${sedeId}-${productoId}`,
            sedeId,
            sedeNombre,
            productoId,
            productoNombre: opcion.nombre,
            codigoSku: opcion.codigoSku,
            tipoProducto: this.stockInicial()?.tipoProducto ?? 'ACCESORIO',
            cantidadDisponible: 0,
            cantidadReservada: 0,
            cantidadLibre: 0,
          }
        : null,
    );
  }
}
