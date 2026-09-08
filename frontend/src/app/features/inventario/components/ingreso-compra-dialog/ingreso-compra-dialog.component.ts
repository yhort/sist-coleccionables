import { DecimalPipe } from '@angular/common';
import {
  Component,
  HostListener,
  OnInit,
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

  readonly error = signal('');
  readonly proveedores = this.proveedoresApi.proveedores;
  readonly etiquetaProveedor = etiquetaProveedor;

  readonly form = this.fb.nonNullable.group({
    proveedorId: ['', Validators.required],
    sedeId: ['', Validators.required],
    observacion: [''],
    detalles: this.fb.array([this.nuevaLinea()]),
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
    this.cancelled.emit();
  }

  agregarLinea(): void {
    this.detalles.push(this.nuevaLinea());
  }

  quitarLinea(index: number): void {
    if (this.detalles.length === 1) {
      return;
    }
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
    if (this.form.invalid) {
      this.error.set('Selecciona proveedor, sede e ítems.');
      return;
    }
    const raw = this.form.getRawValue();
    try {
      await this.comprasApi.crear({
        proveedorId: raw.proveedorId,
        sedeId: raw.sedeId,
        observacion: raw.observacion || null,
        detalles: raw.detalles.map((linea) => ({
          productoId: linea.productoId,
          cantidad: Number(linea.cantidad),
          costoUnitario: Number(linea.costoUnitario),
        })),
      });
      await this.stockApi.refrescarSede(raw.sedeId).catch(() => undefined);
      this.saved.emit();
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  private nuevaLinea() {
    return this.fb.nonNullable.group({
      productoId: ['', Validators.required],
      cantidad: [1, [Validators.required, Validators.min(0.001)]],
      costoUnitario: [0, [Validators.required, Validators.min(0)]],
    });
  }
}
