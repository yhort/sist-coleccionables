import { DecimalPipe } from '@angular/common';
import {
  Component,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ProductoTcg } from '../../../productos-tcg/models/producto-tcg.model';
import { ProveedoresApiService } from '../../../proveedores/data-access/proveedores.service';
import { etiquetaProveedor } from '../../../proveedores/models/proveedor.model';
import { SedeInventario } from '../../models/inventario.model';
import { ComprasApiService } from '../../data-access/compras.service';
import { StockApiService } from '../../data-access/stock.service';

@Component({
  selector: 'app-ingreso-compra-dialog',
  imports: [ReactiveFormsModule, DecimalPipe],
  templateUrl: './ingreso-compra-dialog.component.html',
  styleUrl: './ingreso-compra-dialog.component.scss',
})
export class IngresoCompraDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly comprasApi = inject(ComprasApiService);
  private readonly proveedoresApi = inject(ProveedoresApiService);
  private readonly stockApi = inject(StockApiService);

  readonly sedes = input.required<readonly SedeInventario[]>();
  readonly productos = input.required<readonly ProductoTcg[]>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  @ViewChild('buscadorInput') private buscadorInput?: ElementRef<HTMLInputElement>;

  readonly error = signal('');
  readonly proveedores = this.proveedoresApi.proveedores;
  readonly etiquetaProveedor = etiquetaProveedor;
  readonly busqueda = signal('');
  readonly listaAbierta = signal(false);

  readonly form = this.fb.nonNullable.group({
    proveedorId: ['', Validators.required],
    sedeId: ['', Validators.required],
    observacion: [''],
    detalles: this.fb.array([]),
  });

  readonly candidatos = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    if (q.length < 1) {
      return [];
    }
    return this.productos()
      .filter((producto) => this.coincideProducto(producto, q))
      .slice(0, 12);
  });

  get detalles(): FormArray {
    return this.form.controls.detalles;
  }

  ngOnInit(): void {
    void this.proveedoresApi.refrescar('', true).catch((err) => this.error.set(readApiError(err)));
    const sede = this.sedes()[0];
    if (sede) {
      this.form.controls.sedeId.setValue(sede.id);
    }
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
  }

  agregarProducto(producto: ProductoTcg): void {
    const existente = this.detalles.controls.find(
      (control) => control.get('productoId')?.value === producto.id,
    );
    if (existente) {
      const actual = Number(existente.get('cantidad')?.value) || 0;
      existente.get('cantidad')?.setValue(actual + 1);
    } else {
      const costo =
        producto.costo != null && Number.isFinite(producto.costo) ? Number(producto.costo) : 0;
      this.detalles.push(
        this.fb.nonNullable.group({
          productoId: [producto.id, Validators.required],
          cantidad: [1, [Validators.required, Validators.min(0.001)]],
          costoUnitario: [costo, [Validators.required, Validators.min(0)]],
        }),
      );
    }

    this.busqueda.set('');
    this.listaAbierta.set(false);
    queueMicrotask(() => this.buscadorInput?.nativeElement.focus());
  }

  onBusquedaEnter(): void {
    const primero = this.candidatos()[0];
    if (primero) {
      this.agregarProducto(primero);
    }
  }

  nombreProducto(productoId: string): string {
    const producto = this.productos().find((item) => item.id === productoId);
    if (!producto) {
      return productoId;
    }
    return `${producto.nombre} (${producto.codigoSku})`;
  }

  quitarLinea(index: number): void {
    this.detalles.removeAt(index);
  }

  readonly total = () =>
    this.detalles.controls.reduce((sum, control) => {
      const cantidad = Number(control.get('cantidad')?.value) || 0;
      const costo = Number(control.get('costoUnitario')?.value) || 0;
      return sum + cantidad * costo;
    }, 0);

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.detalles.length === 0) {
      this.error.set('Agrega al menos un producto con el buscador.');
      this.buscadorInput?.nativeElement.focus();
      return;
    }
    if (this.form.invalid) {
      this.error.set('Selecciona proveedor, sede e ítems válidos.');
      return;
    }
    const raw = this.form.getRawValue();
    try {
      await this.comprasApi.crear({
        proveedorId: raw.proveedorId,
        sedeId: raw.sedeId,
        observacion: raw.observacion || null,
        detalles: this.detalles.controls.map((control) => ({
          productoId: String(control.get('productoId')?.value ?? ''),
          cantidad: Number(control.get('cantidad')?.value),
          costoUnitario: Number(control.get('costoUnitario')?.value),
        })),
      });
      await this.stockApi.refrescarSede(raw.sedeId).catch(() => undefined);
      this.saved.emit();
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  private coincideProducto(producto: ProductoTcg, q: string): boolean {
    return (
      producto.nombre.toLowerCase().includes(q) ||
      producto.codigoSku.toLowerCase().includes(q) ||
      (producto.codigoBarras ?? '').toLowerCase().includes(q) ||
      (producto.woo.sku || '').toLowerCase().includes(q)
    );
  }
}
