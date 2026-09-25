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
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { ProductoTcg } from '../../../productos-tcg/models/producto-tcg.model';
import {
  AjusteInventarioRequest,
  ETIQUETAS_MOVIMIENTO,
  SedeInventario,
  SentidoAjuste,
  StockFila,
} from '../../models/inventario.model';
import { StockApiService } from '../../data-access/stock.service';

interface OpcionProductoAjuste {
  id: string;
  nombre: string;
  codigoSku: string;
  codigoBarras: string | null;
  wooSku: string;
  tipoProducto: ProductoTcg['tipoProducto'] | StockFila['tipoProducto'];
}

@Component({
  selector: 'app-ajuste-stock-dialog',
  imports: [ReactiveFormsModule, DecimalPipe],
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
  readonly busqueda = signal('');
  readonly listaAbierta = signal(false);
  readonly productoSeleccionado = signal<OpcionProductoAjuste | null>(null);

  readonly form = this.fb.nonNullable.group({
    sedeId: ['', Validators.required],
    productoId: ['', Validators.required],
    sentido: this.fb.nonNullable.control<SentidoAjuste>('ENTRADA'),
    cantidad: [1, [Validators.required, Validators.min(0.001)]],
    motivo: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
  });

  /** Catálogo activo + fila preseleccionada (productos nuevos sin stock previo). */
  private readonly catalogoOpciones = computed(() => {
    const porId = new Map<string, OpcionProductoAjuste>();

    for (const producto of this.productos()) {
      if (!producto.activo) {
        continue;
      }
      porId.set(producto.id, {
        id: producto.id,
        nombre: producto.nombre,
        codigoSku: producto.codigoSku,
        codigoBarras: producto.codigoBarras,
        wooSku: producto.woo.sku || '',
        tipoProducto: producto.tipoProducto,
      });
    }

    const inicial = this.stockInicial();
    if (inicial && !porId.has(inicial.productoId)) {
      porId.set(inicial.productoId, {
        id: inicial.productoId,
        nombre: inicial.productoNombre,
        codigoSku: inicial.codigoSku,
        codigoBarras: null,
        wooSku: '',
        tipoProducto: inicial.tipoProducto,
      });
    }

    return [...porId.values()].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  });

  readonly candidatos = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    if (q.length < 1) {
      return [];
    }
    return this.catalogoOpciones()
      .filter((item) => this.coincideProducto(item, q))
      .slice(0, 12);
  });

  ngOnInit(): void {
    const inicial = this.stockInicial();
    if (inicial) {
      this.form.patchValue({
        sedeId: inicial.sedeId,
        productoId: inicial.productoId,
        sentido: 'ENTRADA',
      });
      this.productoSeleccionado.set({
        id: inicial.productoId,
        nombre: inicial.productoNombre,
        codigoSku: inicial.codigoSku,
        codigoBarras: null,
        wooSku: '',
        tipoProducto: inicial.tipoProducto,
      });
      this.busqueda.set(`${inicial.codigoSku} · ${inicial.productoNombre}`);
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
    if (this.listaAbierta()) {
      this.listaAbierta.set(false);
      return;
    }
    this.cancelled.emit();
  }

  onBusqueda(valor: string): void {
    this.busqueda.set(valor);
    this.listaAbierta.set(true);
    if (this.productoSeleccionado()) {
      this.productoSeleccionado.set(null);
      this.form.controls.productoId.setValue('');
    }
  }

  seleccionarProducto(producto: OpcionProductoAjuste): void {
    this.productoSeleccionado.set(producto);
    this.form.controls.productoId.setValue(producto.id);
    this.busqueda.set(`${producto.codigoSku} · ${producto.nombre}`);
    this.listaAbierta.set(false);
  }

  onBusquedaEnter(): void {
    const primero = this.candidatos()[0];
    if (primero) {
      this.seleccionarProducto(primero);
    }
  }

  limpiarProducto(): void {
    this.productoSeleccionado.set(null);
    this.form.controls.productoId.setValue('');
    this.busqueda.set('');
    this.listaAbierta.set(false);
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      if (!this.form.controls.productoId.value) {
        this.error.set('Busca y selecciona un producto.');
      }
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

  private coincideProducto(producto: OpcionProductoAjuste, q: string): boolean {
    return (
      producto.nombre.toLowerCase().includes(q) ||
      producto.codigoSku.toLowerCase().includes(q) ||
      (producto.codigoBarras ?? '').toLowerCase().includes(q) ||
      producto.wooSku.toLowerCase().includes(q)
    );
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
    const opcion =
      this.productoSeleccionado()?.id === productoId
        ? this.productoSeleccionado()
        : this.catalogoOpciones().find((item) => item.id === productoId);
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
            tipoProducto: opcion.tipoProducto,
            cantidadDisponible: 0,
            cantidadReservada: 0,
            cantidadLibre: 0,
          }
        : null,
    );
  }
}
