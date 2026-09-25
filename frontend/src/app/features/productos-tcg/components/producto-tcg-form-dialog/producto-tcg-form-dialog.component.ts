import {
  Component,
  HostListener,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';

import {
  AtributoWoo,
  ComponenteCompuesto,
  CondicionTcg,
  ContenidoFijoApertura,
  ETIQUETAS_ACCESORIO,
  ETIQUETAS_CONDICION,
  ETIQUETAS_IDIOMA,
  ETIQUETAS_JUEGO,
  ETIQUETAS_RAREZA,
  ETIQUETAS_SELLADO,
  ETIQUETAS_SYNC,
  ETIQUETAS_TIPO,
  FILTROS_PRODUCTOS_VACIOS,
  IdiomaTcg,
  JuegoTcg,
  ProductoTcg,
  RarezaTcg,
  TipoAccesorio,
  TipoProductoTcg,
  TipoSellado,
  crearAtributosTcgVacios,
  crearMetadatosWooVacios,
  etiquetaJuego,
  normalizarJuego,
} from '../../models/producto-tcg.model';
import { ProductosTcgApiService } from '../../data-access/productos-tcg.service';
import { esUrlImagenDirecta, urlsImagenesDesde } from '../../data-access/producto-tcg.mapper';
import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { StockApiService } from '../../../inventario/data-access/stock.service';

type TabFormulario = 'tcg' | 'woo';

const OPCION_NUEVO_JUEGO = '__nuevo_juego__';

@Component({
  selector: 'app-producto-tcg-form-dialog',
  imports: [ReactiveFormsModule, FormsModule],
  templateUrl: './producto-tcg-form-dialog.component.html',
  styleUrl: './producto-tcg-form-dialog.component.scss',
})
export class ProductoTcgFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly stockApi = inject(StockApiService);

  readonly producto = input<ProductoTcg | null>(null);
  readonly catalogo = input<ProductoTcg[]>([]);
  readonly saved = output<ProductoTcg>();
  readonly cancelled = output<void>();

  tabActiva: TabFormulario = 'tcg';
  errorSku = '';
  readonly contenidoFijo = signal<ContenidoFijoApertura[]>([]);
  readonly busquedaContenido = signal('');
  readonly cantidadContenido = signal(1);
  readonly listaContenidoAbierta = signal(false);
  readonly sedes = this.sedesApi.sedes;
  /** Juegos/categorías añadidos en esta sesión (además de los del catálogo). */
  readonly juegosExtra = signal<string[]>([]);
  readonly creandoJuego = signal(false);
  readonly nuevoJuegoNombre = signal('');
  readonly errorJuego = signal('');
  readonly opcionNuevoJuego = OPCION_NUEVO_JUEGO;

  readonly tipos = Object.entries(ETIQUETAS_TIPO) as [TipoProductoTcg, string][];
  readonly rarezas = Object.entries(ETIQUETAS_RAREZA) as [RarezaTcg, string][];
  readonly idiomas = Object.entries(ETIQUETAS_IDIOMA) as [IdiomaTcg, string][];
  readonly condiciones = Object.entries(ETIQUETAS_CONDICION) as [CondicionTcg, string][];
  readonly sellados = Object.entries(ETIQUETAS_SELLADO) as [TipoSellado, string][];
  readonly accesorios = Object.entries(ETIQUETAS_ACCESORIO) as [TipoAccesorio, string][];
  readonly etiquetasSync = ETIQUETAS_SYNC;

  /** Opciones del selector: conocidos + catálogo + recién creados. */
  readonly opcionesJuego = computed(() => {
    const porValor = new Map<string, string>();
    for (const [codigo, etiqueta] of Object.entries(ETIQUETAS_JUEGO) as [JuegoTcg, string][]) {
      porValor.set(codigo, etiqueta);
    }
    for (const producto of this.catalogo()) {
      const valor = normalizarJuego(producto.juego);
      if (valor && !porValor.has(valor)) {
        porValor.set(valor, etiquetaJuego(valor) || valor);
      }
    }
    for (const extra of this.juegosExtra()) {
      const valor = normalizarJuego(extra);
      if (valor && !porValor.has(valor)) {
        porValor.set(valor, etiquetaJuego(valor) || valor);
      }
    }
    return [...porValor.entries()].sort((a, b) => a[1].localeCompare(b[1], 'es'));
  });

  readonly form: FormGroup = this.fb.nonNullable.group({
    nombre: ['', Validators.required],
    codigoSku: ['', Validators.required],
    tipoProducto: this.fb.nonNullable.control<TipoProductoTcg>('CARTA'),
    juego: this.fb.nonNullable.control<string>(''),
    codigoBarras: [''],
    costo: this.fb.control<number | null>(null),
    stockLocal: [0, [Validators.required, Validators.min(0)]],
    sedeId: [''],
    precioVenta: [0, [Validators.required, Validators.min(0)]],
    activo: [true],
    atributosTcg: this.fb.nonNullable.group({
      setCodigo: [''],
      setNombre: [''],
      numeroCarta: [''],
      rareza: this.fb.nonNullable.control<RarezaTcg | ''>(''),
      condicion: this.fb.nonNullable.control<CondicionTcg | ''>(''),
      esFoil: [false],
      idioma: this.fb.nonNullable.control<IdiomaTcg | ''>(''),
      artista: [''],
      tipoSellado: this.fb.nonNullable.control<TipoSellado | ''>(''),
      edicion: [''],
      cartasEsperadas: this.fb.control<number | null>(null),
      permiteApertura: [true],
      tipoAccesorio: this.fb.nonNullable.control<TipoAccesorio | ''>(''),
    }),
    woo: this.fb.nonNullable.group({
      wooCommerceId: this.fb.control<number | null>(null),
      sku: [''],
      categoriasWoo: [''],
      precioNormal: [0, [Validators.required, Validators.min(0)]],
      precioRebajado: this.fb.control<number | null>(null),
      stockWoo: this.fb.control<number | null>(null),
      imagenes: [''],
    }),
    atributosWoo: this.fb.array<FormGroup>([]),
    componentes: this.fb.array<FormGroup>([]),
  });

  readonly esEdicion = computed(() => this.producto() !== null);
  readonly titulo = computed(() =>
    this.esEdicion() ? 'Editar producto TCG' : 'Registrar producto TCG',
  );

  ngOnInit(): void {
    void this.asegurarSedes();
    const actual = this.producto();
    if (actual) {
      this.cargarProducto(actual);
    } else {
      const sedeId = this.sedes()[0]?.id ?? '';
      this.form.patchValue({ sedeId });
    }
  }

  private async asegurarSedes(): Promise<void> {
    if (this.sedes().length === 0) {
      await this.sedesApi.refrescar().catch(() => undefined);
    }
    if (!this.form.controls['sedeId'].value && this.sedes()[0]) {
      this.form.patchValue({ sedeId: this.sedes()[0].id });
    }
  }

  get tipoProducto(): TipoProductoTcg {
    return this.form.controls['tipoProducto'].value as TipoProductoTcg;
  }

  get atributosWoo(): FormArray<FormGroup> {
    return this.form.controls['atributosWoo'] as FormArray<FormGroup>;
  }

  get componentes(): FormArray<FormGroup> {
    return this.form.controls['componentes'] as FormArray<FormGroup>;
  }

  get candidatosBom(): ProductoTcg[] {
    const idActual = this.producto()?.id;
    return this.catalogo().filter(
      (item) => item.id !== idActual && item.tipoProducto !== 'COMPUESTO',
    );
  }

  get candidatosContenido(): ProductoTcg[] {
    const q = this.busquedaContenido().trim().toLowerCase();
    if (q.length < 2) {
      return [];
    }
    const idActual = this.producto()?.id;
    const yaVinculados = new Set(this.contenidoFijo().map((item) => item.productoId));
    return this.catalogo()
      .filter(
        (item) =>
          item.id !== idActual &&
          item.tipoProducto !== 'COMPUESTO' &&
          !yaVinculados.has(item.id) &&
          this.coincideSkuONombre(item, q),
      )
      .slice(0, 8);
  }

  get permiteAperturaActiva(): boolean {
    return (
      this.tipoProducto === 'SELLADO' &&
      Boolean(this.form.get('atributosTcg')?.get('permiteApertura')?.value)
    );
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  seleccionarTab(tab: TabFormulario): void {
    this.tabActiva = tab;
  }

  onJuegoSelectChange(valor: string): void {
    if (valor === OPCION_NUEVO_JUEGO) {
      this.form.controls['juego'].setValue('');
      this.errorJuego.set('');
      this.nuevoJuegoNombre.set('');
      this.creandoJuego.set(true);
      return;
    }
    this.creandoJuego.set(false);
    this.form.controls['juego'].setValue(valor);
  }

  confirmarNuevoJuego(): void {
    const nombre = this.nuevoJuegoNombre().trim();
    if (!nombre) {
      this.errorJuego.set('Escribe el nombre del juego o categoría.');
      return;
    }
    if (nombre.length > 80) {
      this.errorJuego.set('Máximo 80 caracteres.');
      return;
    }
    const valor = normalizarJuego(nombre);
    const etiqueta = etiquetaJuego(valor) || valor;
    const existe = this.opcionesJuego().some(
      ([codigo, label]) =>
        codigo === valor || label.toLowerCase() === etiqueta.toLowerCase(),
    );
    if (!existe) {
      this.juegosExtra.update((items) => [...items, valor]);
    }
    this.form.controls['juego'].setValue(valor);
    this.creandoJuego.set(false);
    this.nuevoJuegoNombre.set('');
    this.errorJuego.set('');
  }

  cancelarNuevoJuego(): void {
    this.creandoJuego.set(false);
    this.nuevoJuegoNombre.set('');
    this.errorJuego.set('');
  }

  agregarAtributoWoo(nombre = '', valores = ''): void {
    this.atributosWoo.push(
      this.fb.nonNullable.group({
        nombre: [nombre],
        valores: [valores],
      }),
    );
  }

  quitarAtributoWoo(index: number): void {
    this.atributosWoo.removeAt(index);
  }

  agregarComponente(): void {
    this.componentes.push(
      this.fb.nonNullable.group({
        productoHijoId: ['', Validators.required],
        cantidad: [1, [Validators.required, Validators.min(0.001)]],
      }),
    );
  }

  quitarComponente(index: number): void {
    this.componentes.removeAt(index);
  }

  async onBusquedaContenido(valor: string): Promise<void> {
    this.busquedaContenido.set(valor);
    this.listaContenidoAbierta.set(true);
    const q = valor.trim();
    if (q.length < 2) {
      return;
    }
    try {
      await this.productosApi.consultarPagina(
        { ...FILTROS_PRODUCTOS_VACIOS, busqueda: q },
        1,
        20,
      );
    } catch {
      /* el catálogo local sigue filtrando */
    }
  }

  elegirContenido(producto: ProductoTcg): void {
    this.busquedaContenido.set(`${producto.codigoSku} · ${producto.nombre}`);
    this.listaContenidoAbierta.set(false);
    this.agregarContenidoFijo(producto);
  }

  agregarContenidoFijo(producto?: ProductoTcg): void {
    const elegido =
      producto ??
      this.candidatosContenido[0] ??
      this.catalogo().find((item) => this.coincideSkuONombre(item, this.busquedaContenido().trim().toLowerCase()));
    if (!elegido || elegido.id === this.producto()?.id) {
      return;
    }
    const cantidad = Math.max(1, Math.floor(Number(this.cantidadContenido()) || 1));
    this.contenidoFijo.update((items) => {
      const resto = items.filter((item) => item.productoId !== elegido.id);
      return [
        ...resto,
        {
          productoId: elegido.id,
          sku: elegido.codigoSku,
          nombre: elegido.nombre,
          cantidad,
        },
      ];
    });
    this.busquedaContenido.set('');
    this.cantidadContenido.set(1);
    this.listaContenidoAbierta.set(false);
  }

  quitarContenidoFijo(productoId: string): void {
    this.contenidoFijo.update((items) => items.filter((item) => item.productoId !== productoId));
  }

  onCantidadContenido(valor: string): void {
    this.cantidadContenido.set(Math.max(1, Math.floor(Number(valor) || 1)));
  }

  urlsImagenNoDirectas(): string[] {
    return urlsImagenesDesde(this.form.controls['woo'].get('imagenes')?.value).filter(
      (url) => !esUrlImagenDirecta(url),
    );
  }

  copiarAtributosTcgAWoo(): void {
    const tcg = this.form.controls['atributosTcg'].getRawValue();
    const tipo = this.tipoProducto;
    this.atributosWoo.clear();

    if (tipo === 'CARTA') {
      this.agregarAtributoWoo('Set', tcg.setCodigo);
      this.agregarAtributoWoo('Número', tcg.numeroCarta);
      this.agregarAtributoWoo('Rareza', tcg.rareza ? ETIQUETAS_RAREZA[tcg.rareza as RarezaTcg] : '');
      this.agregarAtributoWoo(
        'Condición',
        tcg.condicion ? ETIQUETAS_CONDICION[tcg.condicion as CondicionTcg] : '',
      );
      this.agregarAtributoWoo('Foil', tcg.esFoil ? 'Sí' : 'No');
      this.agregarAtributoWoo('Idioma', tcg.idioma ? ETIQUETAS_IDIOMA[tcg.idioma as IdiomaTcg] : '');
    } else if (tipo === 'SELLADO') {
      this.agregarAtributoWoo(
        'Tipo sellado',
        tcg.tipoSellado ? ETIQUETAS_SELLADO[tcg.tipoSellado as TipoSellado] : '',
      );
      this.agregarAtributoWoo('Edición', tcg.edicion);
    } else if (tipo === 'ACCESORIO') {
      this.agregarAtributoWoo(
        'Tipo accesorio',
        tcg.tipoAccesorio ? ETIQUETAS_ACCESORIO[tcg.tipoAccesorio as TipoAccesorio] : '',
      );
    }

    this.tabActiva = 'woo';
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.errorSku = '';

    if (this.form.invalid) {
      this.tabActiva = this.form.controls['nombre'].invalid || this.form.controls['codigoSku'].invalid
        ? 'tcg'
        : this.tabActiva;
      return;
    }

    const sku = String(this.form.controls['codigoSku'].value).trim();
    if (this.productosApi.skuExiste(sku, this.producto()?.id)) {
      this.errorSku = 'Ese SKU ya existe en el catálogo.';
      this.tabActiva = 'tcg';
      return;
    }

    const raw = this.form.getRawValue();
    const stockLocal = Number(raw.stockLocal) || 0;
    const sedeId = String(raw.sedeId || '').trim();
    const esAlta = this.producto() === null;

    if (esAlta && stockLocal > 0 && !sedeId) {
      this.errorSku = 'Selecciona la sede del stock inicial.';
      this.tabActiva = 'tcg';
      return;
    }

    const woo = raw.woo;
    const precioVenta = Number(raw.precioVenta) || 0;
    const precioRebajado = this.asNumberOrNull(woo.precioRebajado);
    const precioNormal = Number(woo.precioNormal) || precioVenta;

    if (precioRebajado != null && precioRebajado >= precioNormal && precioNormal > 0) {
      this.form.controls['woo'].get('precioRebajado')?.setErrors({ mayorQueNormal: true });
      this.tabActiva = 'woo';
      return;
    }

    const atributosTcg = { ...crearAtributosTcgVacios(), ...raw.atributosTcg };
    const existentes = this.producto();
    const wooSku = String(woo.sku || sku).trim();

    const persistido: ProductoTcg = {
      id: existentes?.id ?? globalThis.crypto.randomUUID(),
      tipoProducto: raw.tipoProducto,
      nombre: String(raw.nombre).trim(),
      codigoSku: sku,
      codigoBarras: String(raw.codigoBarras || '').trim() || null,
      precioVenta,
      costo: this.asNumberOrNull(raw.costo),
      juego: normalizarJuego(raw.juego),
      activo: Boolean(raw.activo),
      stockLocal,
      cartaCatalogoId: existentes?.cartaCatalogoId ?? null,
      atributosTcg,
      componentes: this.mapearComponentes(),
      contenidoFijo: this.permiteAperturaActiva ? this.contenidoFijo() : [],
      woo: {
        ...crearMetadatosWooVacios(wooSku),
        ...(existentes?.woo ?? {}),
        wooCommerceId: this.asNumberOrNull(woo.wooCommerceId),
        sku: wooSku,
        categoriasWoo: this.splitList(woo.categoriasWoo),
        precioNormal,
        precioRebajado,
        stockWoo: this.asNumberOrNull(woo.stockWoo),
        imagenes: urlsImagenesDesde(woo.imagenes),
        atributos: this.mapearAtributosWoo(),
        estadoSincronizacion: this.resolverSync(existentes, woo.wooCommerceId),
      },
    };

    try {
      const guardado = await this.productosApi.guardar(persistido, {
        esAlta,
        sedeId: esAlta && stockLocal > 0 ? sedeId : null,
      });
      if (esAlta && stockLocal > 0 && sedeId) {
        await this.stockApi
          .confirmarStockProducto(guardado.id, sedeId, stockLocal)
          .catch(() => this.stockApi.refrescarSede(sedeId));
      }
      this.saved.emit(guardado);
    } catch (err) {
      this.errorSku = err instanceof Error ? err.message : 'No se pudo guardar el producto.';
      this.tabActiva = 'tcg';
    }
  }

  private cargarProducto(producto: ProductoTcg): void {
    const juego = normalizarJuego(producto.juego);
    if (juego && !(juego in ETIQUETAS_JUEGO)) {
      this.juegosExtra.update((items) => (items.includes(juego) ? items : [...items, juego]));
    }
    this.form.patchValue({
      nombre: producto.nombre,
      codigoSku: producto.codigoSku,
      tipoProducto: producto.tipoProducto,
      juego,
      codigoBarras: producto.codigoBarras ?? '',
      costo: producto.costo,
      stockLocal: producto.stockLocal,
      precioVenta: producto.precioVenta || producto.woo.precioNormal || 0,
      activo: producto.activo,
      atributosTcg: producto.atributosTcg,
      woo: {
        wooCommerceId: producto.woo.wooCommerceId,
        sku: producto.woo.sku,
        categoriasWoo: producto.woo.categoriasWoo.join(', '),
        precioNormal: producto.woo.precioNormal,
        precioRebajado: producto.woo.precioRebajado,
        stockWoo: producto.woo.stockWoo,
        imagenes: producto.woo.imagenes.join(', '),
      },
    });

    this.atributosWoo.clear();
    for (const atributo of producto.woo.atributos) {
      this.agregarAtributoWoo(atributo.nombre, atributo.valores.join(', '));
    }

    this.componentes.clear();
    for (const componente of producto.componentes) {
      this.componentes.push(
        this.fb.nonNullable.group({
          productoHijoId: [componente.productoHijoId, Validators.required],
          cantidad: [componente.cantidad, [Validators.required, Validators.min(0.001)]],
        }),
      );
    }
    this.contenidoFijo.set(producto.contenidoFijo.map((item) => ({ ...item })));
  }

  private coincideSkuONombre(producto: ProductoTcg, q: string): boolean {
    return (
      producto.codigoSku.toLowerCase().includes(q) ||
      producto.nombre.toLowerCase().includes(q) ||
      (producto.woo.sku || '').toLowerCase().includes(q)
    );
  }

  private mapearAtributosWoo(): AtributoWoo[] {
    return this.atributosWoo.controls
      .map((grupo) => ({
        nombre: String(grupo.controls['nombre'].value ?? '').trim(),
        valores: this.splitList(grupo.controls['valores'].value),
      }))
      .filter((atributo) => atributo.nombre.length > 0);
  }

  private mapearComponentes(): ComponenteCompuesto[] {
    return this.componentes.controls
      .map((grupo) => {
        const id = String(grupo.controls['productoHijoId'].value ?? '');
        const hijo = this.catalogo().find((item) => item.id === id);
        return {
          productoHijoId: id,
          sku: hijo?.codigoSku ?? '',
          nombre: hijo?.nombre ?? '',
          cantidad: Number(grupo.controls['cantidad'].value) || 0,
        };
      })
      .filter((item) => item.productoHijoId && item.cantidad > 0);
  }

  private splitList(value: unknown): string[] {
    return String(value ?? '')
      .split(/[,;\n]+/)
      .map((parte) => parte.trim())
      .filter(Boolean);
  }

  private asNumberOrNull(value: unknown): number | null {
    if (value === null || value === undefined || value === '') {
      return null;
    }
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private resolverSync(
    existente: ProductoTcg | null | undefined,
    wooCommerceId: unknown,
  ): ProductoTcg['woo']['estadoSincronizacion'] {
    if (this.asNumberOrNull(wooCommerceId) == null) {
      return 'NO_MAPEADO';
    }
    return existente?.woo.estadoSincronizacion === 'SINCRONIZADO' ? 'PENDIENTE' : existente?.woo.estadoSincronizacion ?? 'PENDIENTE';
  }
}
