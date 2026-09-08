import {
  Component,
  HostListener,
  OnInit,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';

import { SelectorProductoCascadaComponent } from '../../../catalogo-tcg/components/selector-producto-cascada/selector-producto-cascada.component';
import { ProductoTcg, ETIQUETAS_TIPO } from '../../../productos-tcg/models/producto-tcg.model';
import { SubastasTcgApiService } from '../../data-access/subastas-tcg.service';
import { CANALES_SUBASTA, ETIQUETAS_CANAL_SUBASTA } from '../../models/subasta-tcg.model';

interface LineaBorrador {
  productoId: string;
  nombre: string;
  codigoSku: string;
  tipoProducto: ProductoTcg['tipoProducto'];
  cantidad: number;
  stockLibre: number;
}

@Component({
  selector: 'app-subasta-form-dialog',
  imports: [FormsModule, ReactiveFormsModule, SelectorProductoCascadaComponent],
  templateUrl: './subasta-form-dialog.component.html',
  styleUrl: './subasta-form-dialog.component.scss',
})
export class SubastaFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly subastasApi = inject(SubastasTcgApiService);

  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly lineas = signal<LineaBorrador[]>([]);
  readonly productoPendienteId = signal('');
  readonly cantidadPendiente = signal(1);
  readonly sedes = this.subastasApi.sedes;
  readonly canales = CANALES_SUBASTA;
  readonly etiquetasCanal = ETIQUETAS_CANAL_SUBASTA;
  readonly etiquetasTipo = ETIQUETAS_TIPO;

  readonly form = this.fb.nonNullable.group({
    titulo: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
    sedeId: ['', Validators.required],
    canal: this.fb.nonNullable.control<(typeof CANALES_SUBASTA)[number]>('FACEBOOK_SUBASTA'),
    precioBase: [1, [Validators.required, Validators.min(0.01)]],
    incrementoMinimo: [5, [Validators.required, Validators.min(0.01)]],
    precioReserva: [null as number | null],
    fechaInicio: ['', Validators.required],
    fechaCierre: ['', Validators.required],
    observacion: [''],
  });

  readonly resumenLote = computed(() => {
    const items = this.lineas();
    const skus = items.length;
    const unidades = items.reduce((sum, l) => sum + l.cantidad, 0);
    if (skus === 0) {
      return 'Agrega al menos un producto (Single Hit, Bulk o Combo).';
    }
    if (skus === 1 && unidades === 1) {
      return 'Single Hit · 1 carta/producto';
    }
    if (skus === 1) {
      return `Bulk · ${unidades} unidades del mismo SKU`;
    }
    return `Combo Multi-SKU · ${skus} productos · ${unidades} unidades`;
  });

  ngOnInit(): void {
    const sedeId = this.sedes()[0]?.id;
    const inicio = new Date();
    const cierre = new Date(inicio.getTime() + 2 * 60 * 60 * 1000);
    this.form.patchValue({
      sedeId: sedeId || '',
      fechaInicio: toDatetimeLocal(inicio),
      fechaCierre: toDatetimeLocal(cierre),
    });
  }

  stockLibreFn = (productoId: string): number => {
    const sedeId = this.form.controls.sedeId.value;
    return sedeId ? this.subastasApi.stockLibre(sedeId, productoId) : 0;
  };

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  onProductoSeleccionado(producto: ProductoTcg | null): void {
    this.productoPendienteId.set(producto?.id ?? '');
  }

  agregarLinea(): void {
    this.error.set('');
    const productoId = this.productoPendienteId();
    const cantidad = Math.floor(Number(this.cantidadPendiente()));
    if (!productoId) {
      this.error.set('Busca y selecciona un producto del inventario.');
      return;
    }
    if (!Number.isFinite(cantidad) || cantidad < 1) {
      this.error.set('La cantidad debe ser un entero mayor o igual a 1.');
      return;
    }

    const producto = this.subastasApi.productos().find((p) => p.id === productoId);
    if (!producto) {
      this.error.set('No se encontró el producto seleccionado.');
      return;
    }

    const sedeId = this.form.controls.sedeId.value;
    const stockLibre = sedeId ? this.subastasApi.stockLibre(sedeId, productoId) : 0;
    if (stockLibre < cantidad) {
      this.error.set(`Stock libre insuficiente para ${producto.nombre} (libre: ${stockLibre}).`);
      return;
    }

    this.lineas.update((items) => {
      const existente = items.find((l) => l.productoId === productoId);
      if (existente) {
        return items.map((l) =>
          l.productoId === productoId
            ? { ...l, cantidad: l.cantidad + cantidad, stockLibre }
            : l,
        );
      }
      return [
        ...items,
        {
          productoId,
          nombre: producto.nombre,
          codigoSku: producto.codigoSku,
          tipoProducto: producto.tipoProducto,
          cantidad,
          stockLibre,
        },
      ];
    });

    this.productoPendienteId.set('');
    this.cantidadPendiente.set(1);
  }

  actualizarCantidad(productoId: string, valor: string | number): void {
    const cantidad = Math.floor(Number(valor));
    if (!Number.isFinite(cantidad) || cantidad < 1) {
      return;
    }
    this.lineas.update((items) =>
      items.map((l) => (l.productoId === productoId ? { ...l, cantidad } : l)),
    );
  }

  quitarLinea(productoId: string): void {
    this.lineas.update((items) => items.filter((l) => l.productoId !== productoId));
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }
    const lineas = this.lineas();
    if (lineas.length === 0) {
      this.error.set('Agrega al menos un producto al lote de la subasta.');
      return;
    }

    const raw = this.form.getRawValue();
    const reservaRaw = Number(raw.precioReserva);
    const precioReserva =
      raw.precioReserva === null || !Number.isFinite(reservaRaw) || reservaRaw <= 0
        ? null
        : reservaRaw;

    try {
      await this.subastasApi.crear({
        titulo: raw.titulo,
        sedeId: raw.sedeId,
        productoId: lineas[0].productoId,
        detalles: lineas.map((l) => ({
          productoId: l.productoId,
          cantidad: l.cantidad,
        })),
        canal: raw.canal,
        precioBase: Number(raw.precioBase),
        incrementoMinimo: Number(raw.incrementoMinimo),
        precioReserva,
        fechaInicio: new Date(raw.fechaInicio).toISOString(),
        fechaCierre: new Date(raw.fechaCierre).toISOString(),
        observacion: raw.observacion,
      });
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo crear la subasta.');
    }
  }
}

function toDatetimeLocal(date: Date): string {
  const pad = (valor: number) => String(valor).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
