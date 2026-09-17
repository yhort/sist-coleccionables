import {
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  ViewChild,
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
  /** Clave local única (permite el mismo SKU en varias filas en modo Individuales). */
  id: string;
  productoId: string;
  nombre: string;
  /** Título editable para Live / Checkout; por defecto = nombre de catálogo. */
  tituloPersonalizado: string;
  codigoSku: string;
  tipoProducto: ProductoTcg['tipoProducto'];
  cantidad: number;
  stockLibre: number;
}

/** Alias de lista temporal pedido por el flujo de lote. */
type LoteItem = LineaBorrador;

export type ModoCreacionSubasta = 'COMBO' | 'INDIVIDUALES';

@Component({
  selector: 'app-subasta-form-dialog',
  imports: [FormsModule, ReactiveFormsModule, SelectorProductoCascadaComponent],
  templateUrl: './subasta-form-dialog.component.html',
  styleUrl: './subasta-form-dialog.component.scss',
})
export class SubastaFormDialogComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly subastasApi = inject(SubastasTcgApiService);
  private toastTimer: ReturnType<typeof setTimeout> | null = null;
  private lineaSeq = 0;

  @ViewChild(SelectorProductoCascadaComponent)
  private selectorProducto?: SelectorProductoCascadaComponent;

  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly toastInfo = signal('');
  readonly enviando = signal(false);
  /** Lista temporal de productos del lote (`loteItems`). */
  readonly loteItems = signal<LoteItem[]>([]);
  readonly productoPendienteId = signal('');
  readonly cantidadPendiente = signal(1);
  readonly modoCreacion = signal<ModoCreacionSubasta>('COMBO');
  readonly sedes = this.subastasApi.sedes;
  readonly canales = CANALES_SUBASTA;
  readonly etiquetasCanal = ETIQUETAS_CANAL_SUBASTA;
  readonly etiquetasTipo = ETIQUETAS_TIPO;

  /** Compat: mismos ítems que `loteItems`. */
  readonly lineas = this.loteItems;

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
    const items = this.loteItems();
    const filas = items.length;
    const unidades = items.reduce((sum, l) => sum + l.cantidad, 0);
    const modo = this.modoCreacion();
    if (filas === 0) {
      return 'Agrega al menos un producto (Single Hit, Bulk o Combo).';
    }
    if (modo === 'INDIVIDUALES') {
      return `1 evento / sala · ${filas} carta${filas === 1 ? '' : 's'} independientes · ${unidades} uds`;
    }
    const skus = new Set(items.map((l) => l.productoId)).size;
    if (skus === 1 && unidades === 1) {
      return 'Single Hit · 1 carta/producto';
    }
    if (skus === 1) {
      return `Bulk · ${unidades} unidades del mismo SKU`;
    }
    return `Combo / Lote único · ${skus} productos · ${unidades} unidades`;
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

  ngOnDestroy(): void {
    this.clearToastTimer();
  }

  stockLibreFn = (productoId: string): number => {
    const sedeId = this.form.controls.sedeId.value;
    return sedeId ? this.subastasApi.stockLibre(sedeId, productoId) : 0;
  };

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.enviando()) {
      this.cancelled.emit();
    }
  }

  onProductoSeleccionado(producto: ProductoTcg | null): void {
    this.productoPendienteId.set(producto?.id ?? '');
  }

  setModoCreacion(modo: ModoCreacionSubasta): void {
    if (this.modoCreacion() === modo) {
      return;
    }
    this.modoCreacion.set(modo);
    this.cantidadPendiente.set(1);
    this.error.set('');

    if (modo === 'INDIVIDUALES') {
      this.loteItems.update((items) => this.expandirAFilasUnitarias(items));
    } else {
      this.loteItems.update((items) => this.consolidarPorProducto(items));
    }
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
    const yaEnLote = this.loteItems()
      .filter((l) => l.productoId === productoId)
      .reduce((sum, l) => sum + l.cantidad, 0);
    if (stockLibre < yaEnLote + cantidad) {
      this.error.set(`Stock libre insuficiente para ${producto.nombre} (libre: ${stockLibre}).`);
      return;
    }

    if (this.modoCreacion() === 'INDIVIDUALES') {
      const nuevas: LoteItem[] = Array.from({ length: cantidad }, () =>
        this.crearLinea(producto, 1, stockLibre),
      );
      this.loteItems.update((items) => [...items, ...nuevas]);
    } else {
      let consolidado = false;
      this.loteItems.update((items) => {
        const existente = items.find((l) => l.productoId === productoId);
        if (existente) {
          consolidado = true;
          return items.map((l) =>
            l.productoId === productoId
              ? { ...l, cantidad: l.cantidad + cantidad, stockLibre }
              : l,
          );
        }
        return [...items, this.crearLinea(producto, cantidad, stockLibre)];
      });
      if (consolidado) {
        this.mostrarToast('Se actualizó la cantidad del producto en el combo');
      }
    }

    this.limpiarPicker();
  }

  /** Carga masiva de un set/prefijo (modo Individuales). Omite SKUs ya presentes en el lote. */
  agregarSetAlLote(productos: ProductoTcg[]): void {
    this.error.set('');
    if (this.modoCreacion() !== 'INDIVIDUALES') {
      this.error.set('La carga masiva de set solo está disponible en subastas individuales.');
      return;
    }
    if (productos.length === 0) {
      return;
    }

    const sedeId = this.form.controls.sedeId.value;
    const idsEnLote = new Set(this.loteItems().map((l) => l.productoId));
    const nuevas: LoteItem[] = [];
    let omitidosDup = 0;
    let omitidosStock = 0;

    for (const producto of productos) {
      if (idsEnLote.has(producto.id)) {
        omitidosDup += 1;
        continue;
      }
      const stockLibre = sedeId ? this.subastasApi.stockLibre(sedeId, producto.id) : 0;
      if (stockLibre < 1) {
        omitidosStock += 1;
        continue;
      }
      idsEnLote.add(producto.id);
      nuevas.push(this.crearLinea(producto, 1, stockLibre));
    }

    if (nuevas.length > 0) {
      this.loteItems.update((items) => [...items, ...nuevas]);
    }

    const partes = [`${nuevas.length} carta${nuevas.length === 1 ? '' : 's'} agregada${nuevas.length === 1 ? '' : 's'}`];
    if (omitidosDup > 0) {
      partes.push(`${omitidosDup} ya estaban en el lote`);
    }
    if (omitidosStock > 0) {
      partes.push(`${omitidosStock} sin stock libre`);
    }
    this.mostrarToast(partes.join(' · '));
    this.limpiarPicker();
  }

  actualizarCantidad(lineaId: string, valor: string | number): void {
    const cantidad = Math.floor(Number(valor));
    if (!Number.isFinite(cantidad) || cantidad < 1) {
      return;
    }

    if (this.modoCreacion() === 'INDIVIDUALES') {
      this.loteItems.update((items) => {
        const idx = items.findIndex((l) => l.id === lineaId);
        if (idx < 0) {
          return items;
        }
        const base = items[idx];
        const sedeId = this.form.controls.sedeId.value;
        const stockLibre = sedeId
          ? this.subastasApi.stockLibre(sedeId, base.productoId)
          : base.stockLibre;
        const otras = items.filter((l) => l.id !== lineaId);
        const yaOtras = otras
          .filter((l) => l.productoId === base.productoId)
          .reduce((sum, l) => sum + l.cantidad, 0);
        if (stockLibre < yaOtras + cantidad) {
          this.error.set(
            `Stock libre insuficiente para ${base.nombre} (libre: ${stockLibre}).`,
          );
          return items;
        }
        const expansiones = Array.from({ length: cantidad }, () =>
          this.crearLinea(
            {
              id: base.productoId,
              nombre: base.nombre,
              codigoSku: base.codigoSku,
              tipoProducto: base.tipoProducto,
            },
            1,
            stockLibre,
            base.tituloPersonalizado,
          ),
        );
        return [...otras.slice(0, idx), ...expansiones, ...otras.slice(idx)];
      });
      return;
    }

    this.loteItems.update((items) =>
      items.map((l) => (l.id === lineaId ? { ...l, cantidad } : l)),
    );
  }

  quitarLinea(lineaId: string): void {
    this.loteItems.update((items) => items.filter((l) => l.id !== lineaId));
  }

  actualizarTituloPersonalizado(lineaId: string, valor: string): void {
    this.loteItems.update((items) =>
      items.map((l) => (l.id === lineaId ? { ...l, tituloPersonalizado: valor } : l)),
    );
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }
    const lineas = this.loteItems();
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

    const base = {
      sedeId: raw.sedeId,
      canal: raw.canal,
      precioBase: Number(raw.precioBase),
      incrementoMinimo: Number(raw.incrementoMinimo),
      precioReserva,
      fechaInicio: new Date(raw.fechaInicio).toISOString(),
      fechaCierre: new Date(raw.fechaCierre).toISOString(),
      observacion: raw.observacion,
    };

    this.enviando.set(true);
    try {
      await this.subastasApi.crear({
        ...base,
        titulo: raw.titulo,
        modo: this.modoCreacion(),
        productoId: lineas[0].productoId,
        detalles: lineas.map((l) => ({
          productoId: l.productoId,
          cantidad: l.cantidad,
          tituloPersonalizado: l.tituloPersonalizado.trim() || l.nombre,
        })),
      });
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo crear la subasta.');
    } finally {
      this.enviando.set(false);
    }
  }

  private crearLinea(
    producto: {
      id: string;
      nombre: string;
      codigoSku: string;
      tipoProducto: ProductoTcg['tipoProducto'];
    },
    cantidad: number,
    stockLibre: number,
    tituloPersonalizado?: string,
  ): LoteItem {
    return {
      id: this.nuevaClaveLinea(producto.id),
      productoId: producto.id,
      nombre: producto.nombre,
      tituloPersonalizado: (tituloPersonalizado ?? producto.nombre).trim() || producto.nombre,
      codigoSku: producto.codigoSku,
      tipoProducto: producto.tipoProducto,
      cantidad,
      stockLibre,
    };
  }

  private expandirAFilasUnitarias(items: LoteItem[]): LoteItem[] {
    const resultado: LoteItem[] = [];
    for (const item of items) {
      const n = Math.max(1, Math.floor(item.cantidad));
      for (let i = 0; i < n; i++) {
        resultado.push(
          this.crearLinea(
            {
              id: item.productoId,
              nombre: item.nombre,
              codigoSku: item.codigoSku,
              tipoProducto: item.tipoProducto,
            },
            1,
            item.stockLibre,
            item.tituloPersonalizado,
          ),
        );
      }
    }
    return resultado;
  }

  private consolidarPorProducto(items: LoteItem[]): LoteItem[] {
    const mapa = new Map<string, LoteItem>();
    for (const item of items) {
      const prev = mapa.get(item.productoId);
      if (prev) {
        mapa.set(item.productoId, {
          ...prev,
          cantidad: prev.cantidad + item.cantidad,
          stockLibre: Math.max(prev.stockLibre, item.stockLibre),
        });
      } else {
        mapa.set(item.productoId, {
          ...item,
          id: this.nuevaClaveLinea(item.productoId),
        });
      }
    }
    return [...mapa.values()];
  }

  private nuevaClaveLinea(productoId: string): string {
    this.lineaSeq += 1;
    return `lote-${this.lineaSeq}-${productoId}`;
  }

  private mostrarToast(mensaje: string): void {
    this.clearToastTimer();
    this.toastInfo.set(mensaje);
    this.toastTimer = setTimeout(() => {
      this.toastInfo.set('');
      this.toastTimer = null;
    }, 2800);
  }

  private clearToastTimer(): void {
    if (this.toastTimer) {
      clearTimeout(this.toastTimer);
      this.toastTimer = null;
    }
  }

  private limpiarPicker(): void {
    this.productoPendienteId.set('');
    this.cantidadPendiente.set(1);
    this.selectorProducto?.resetCompleto();
  }
}

function toDatetimeLocal(date: Date): string {
  const pad = (valor: number) => String(valor).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
