import {
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';

import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import {
  ETIQUETAS_CONDICION,
  ETIQUETAS_IDIOMA,
  ETIQUETAS_RAREZA,
  ETIQUETAS_TIPO,
  ProductoTcg,
} from '../../../productos-tcg/models/producto-tcg.model';
import { CatalogoTcgApiService } from '../../data-access/catalogo-tcg.service';
import { TcgCarta, TcgSerie, TcgSet } from '../../models/catalogo-tcg.model';

export interface OpcionAgregarSet {
  prefijo: string;
  etiqueta: string;
  productos: ProductoTcg[];
}

@Component({
  selector: 'app-selector-producto-cascada',
  templateUrl: './selector-producto-cascada.component.html',
  styleUrl: './selector-producto-cascada.component.scss',
})
export class SelectorProductoCascadaComponent {
  private readonly catalogoApi = inject(CatalogoTcgApiService);
  private readonly productosApi = inject(ProductosTcgApiService);

  readonly sedeId = input.required<string>();
  readonly productoId = input<string>('');
  /** Habilita la acción masiva «Agregar todo el set» en el desplegable de SKU. */
  readonly permitirAgregarSet = input(false);
  /** Productos ya en el lote: no se cuentan ni se re-emiten en la carga masiva. */
  readonly productoIdsExcluidos = input<readonly string[]>([]);
  readonly stockLibreFn = input.required<(productoId: string) => number>();
  readonly productoChange = output<ProductoTcg | null>();
  readonly agregarSet = output<ProductoTcg[]>();

  readonly series = signal<TcgSerie[]>([]);
  readonly sets = signal<TcgSet[]>([]);
  readonly cartas = signal<TcgCarta[]>([]);
  readonly serieId = signal('');
  readonly setId = signal('');
  readonly cartaId = signal('');
  readonly busqueda = signal('');
  readonly busquedaFicha = signal('');
  readonly listaFichasAbierta = signal(false);
  readonly errorScan = signal('');
  readonly etiquetasTipo = ETIQUETAS_TIPO;
  readonly etiquetasCondicion = ETIQUETAS_CONDICION;
  readonly etiquetasIdioma = ETIQUETAS_IDIOMA;
  readonly etiquetasRareza = ETIQUETAS_RAREZA;

  readonly productos = computed(() => this.productosApi.productos().filter((item) => item.activo));

  readonly seleccionado = computed(() => {
    const id = this.productoId();
    return this.productos().find((item) => item.id === id) ?? null;
  });

  readonly coincidencias = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    if (q.length < 2) {
      return [];
    }
    return this.productos()
      .filter((producto) => this.coincideCodigoONombre(producto, q))
      .slice(0, 8);
  });

  /** Prefijo/Set detectado a partir de la búsqueda de SKU (p. ej. ME-ASC). */
  readonly opcionAgregarSet = computed<OpcionAgregarSet | null>(() => {
    if (!this.permitirAgregarSet()) {
      return null;
    }
    const raw = this.busqueda().trim();
    if (raw.length < 2) {
      return null;
    }
    const excluidos = new Set(this.productoIdsExcluidos());
    const productos = this.productosDelPrefijoSet(raw).filter((p) => !excluidos.has(p.id));
    if (productos.length < 2) {
      return null;
    }
    const etiqueta = etiquetaPrefijoSet(raw, productos);
    return { prefijo: etiqueta, etiqueta, productos };
  });

  readonly mostrarSugerencias = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    if (!q) {
      return false;
    }
    if (this.opcionAgregarSet()) {
      return true;
    }
    const skuSel = this.seleccionado()?.codigoSku.toLowerCase() ?? '';
    return this.coincidencias().length > 0 && q !== skuSel;
  });

  readonly fichasFiltradas = computed(() => {
    const q = this.busquedaFicha().trim();
    const items = this.cartas();
    if (!q) {
      return items.slice(0, 12);
    }
    return items.filter((ficha) => coincideFicha(ficha, q)).slice(0, 12);
  });

  readonly variantesFicha = computed(() => {
    const cartaId = this.cartaId();
    if (!cartaId) {
      return [];
    }
    return this.productos()
      .filter((item) => item.tipoProducto === 'CARTA' && item.cartaCatalogoId === cartaId)
      .sort((a, b) => this.stockLibreFn()(b.id) - this.stockLibreFn()(a.id));
  });

  readonly variantesConStock = computed(() => {
    this.sedeId();
    return this.variantesFicha().filter((item) => this.stockLibreFn()(item.id) > 0);
  });

  constructor() {
    void this.cargarSeries();
  }

  onBusquedaInput(valor: string): void {
    this.busqueda.set(valor);
    this.errorScan.set('');
    const exacto = this.buscarExacto(valor);
    if (exacto) {
      this.aplicarProducto(exacto);
    }
  }

  confirmarScan(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const exacto = this.buscarExacto(this.busqueda()) ?? this.buscarUnicoPrefijo(this.busqueda());
    if (!exacto) {
      this.errorScan.set('No hay un SKU o código de barras coincidente.');
      return;
    }
    this.aplicarProducto(exacto);
  }

  elegirCoincidencia(producto: ProductoTcg): void {
    this.aplicarProducto(producto);
  }

  confirmarAgregarSet(): void {
    const opcion = this.opcionAgregarSet();
    if (!opcion || opcion.productos.length === 0) {
      return;
    }
    this.agregarSet.emit(opcion.productos);
    this.busqueda.set('');
    this.errorScan.set('');
    this.productoChange.emit(null);
  }

  limpiar(): void {
    this.busqueda.set('');
    this.errorScan.set('');
    this.productoChange.emit(null);
  }

  /** Limpia SKU, cascada (serie/set/ficha/variante) y selección para escanear el siguiente ítem. */
  resetCompleto(): void {
    this.busqueda.set('');
    this.busquedaFicha.set('');
    this.serieId.set('');
    this.setId.set('');
    this.cartaId.set('');
    this.sets.set([]);
    this.cartas.set([]);
    this.listaFichasAbierta.set(false);
    this.errorScan.set('');
    this.productoChange.emit(null);
  }

  async onSerieChange(serieId: string): Promise<void> {
    this.serieId.set(serieId);
    this.setId.set('');
    this.cartaId.set('');
    this.busquedaFicha.set('');
    this.listaFichasAbierta.set(false);
    this.sets.set([]);
    this.cartas.set([]);
    if (!serieId) {
      return;
    }
    try {
      this.sets.set(await this.catalogoApi.listarSets(serieId));
    } catch {
      this.errorScan.set('No se pudieron cargar los sets de la serie.');
    }
  }

  async onSetChange(setId: string): Promise<void> {
    this.setId.set(setId);
    this.cartaId.set('');
    this.busquedaFicha.set('');
    this.listaFichasAbierta.set(false);
    this.cartas.set([]);
    if (!setId) {
      return;
    }
    try {
      this.cartas.set(await this.catalogoApi.listarCartas(setId));
    } catch {
      this.errorScan.set('No se pudieron cargar las fichas del set.');
    }
  }

  onFichaInput(valor: string): void {
    this.busquedaFicha.set(valor);
    this.listaFichasAbierta.set(true);
    this.errorScan.set('');
    const exactas = this.cartas().filter((ficha) => numeroFichaExacto(ficha.numero, valor));
    const q = valor.trim().replace(/^#/, '');
    if (exactas.length === 1 && /^\d{3,}$/.test(q)) {
      this.elegirFicha(exactas[0]);
    }
  }

  confirmarFicha(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const q = this.busquedaFicha();
    const porNumero = this.cartas().filter(
      (ficha) => normalizarNumeroFicha(ficha.numero) === normalizarNumeroFicha(q),
    );
    const filtradas = this.fichasFiltradas();
    const elegida =
      porNumero.length === 1 ? porNumero[0] : filtradas.length === 1 ? filtradas[0] : null;
    if (!elegida) {
      this.errorScan.set('Escribe el número o el nombre de la ficha y elige una coincidencia.');
      this.listaFichasAbierta.set(true);
      return;
    }
    this.elegirFicha(elegida);
  }

  elegirFicha(ficha: TcgCarta): void {
    this.cartaId.set(ficha.id);
    this.busquedaFicha.set(`#${ficha.numero} · ${ficha.nombre}`);
    this.listaFichasAbierta.set(false);
    this.errorScan.set('');
  }

  abrirListaFichas(): void {
    if (!this.setId()) {
      return;
    }
    this.listaFichasAbierta.set(true);
  }

  onVarianteChange(productoId: string): void {
    const producto = this.productos().find((item) => item.id === productoId);
    if (producto) {
      this.aplicarProducto(producto);
    }
  }

  etiquetaVariante(producto: ProductoTcg): string {
    const foil = producto.atributosTcg.esFoil ? 'Foil' : 'Standard';
    const condicion = producto.atributosTcg.condicion
      ? this.etiquetasCondicion[producto.atributosTcg.condicion]
      : '—';
    const idioma = producto.atributosTcg.idioma
      ? this.etiquetasIdioma[producto.atributosTcg.idioma]
      : '—';
    return `${producto.codigoSku} · ${foil} · ${condicion} · ${idioma} · libre ${this.stockLibreFn()(producto.id)}`;
  }

  private async cargarSeries(): Promise<void> {
    try {
      this.series.set(await this.catalogoApi.listarSeries());
    } catch {
      this.series.set([]);
    }
  }

  private aplicarProducto(producto: ProductoTcg): void {
    this.busqueda.set(producto.codigoSku);
    this.errorScan.set('');
    this.productoChange.emit(producto);
  }

  private productosDelPrefijoSet(texto: string): ProductoTcg[] {
    const q = texto.trim().toLowerCase();
    if (!q) {
      return [];
    }
    return this.productos()
      .filter((producto) => coincidePrefijoSet(producto, q))
      .sort((a, b) => a.codigoSku.localeCompare(b.codigoSku, 'es'));
  }

  private buscarExacto(texto: string): ProductoTcg | undefined {
    const q = texto.trim().toLowerCase();
    if (!q) {
      return undefined;
    }
    return this.productos().find(
      (producto) =>
        producto.codigoSku.toLowerCase() === q ||
        producto.woo.sku.toLowerCase() === q ||
        (producto.codigoBarras?.toLowerCase() ?? '') === q,
    );
  }

  private buscarUnicoPrefijo(texto: string): ProductoTcg | undefined {
    const q = texto.trim().toLowerCase();
    if (q.length < 4) {
      return undefined;
    }
    const hits = this.productos().filter(
      (producto) =>
        producto.codigoSku.toLowerCase().startsWith(q) ||
        producto.woo.sku.toLowerCase().startsWith(q) ||
        (producto.codigoBarras?.toLowerCase() ?? '').startsWith(q),
    );
    return hits.length === 1 ? hits[0] : undefined;
  }

  private coincideCodigoONombre(producto: ProductoTcg, q: string): boolean {
    return [
      producto.codigoSku,
      producto.woo.sku,
      producto.codigoBarras ?? '',
      producto.nombre,
    ]
      .join(' ')
      .toLowerCase()
      .includes(q);
  }
}

function normalizarClaveSet(valor: string): string {
  return valor.trim().toLowerCase().replace(/[-_\s]+/g, '');
}

function coincidePrefijoSet(producto: ProductoTcg, q: string): boolean {
  const qLower = q.trim().toLowerCase();
  const qNorm = normalizarClaveSet(q);
  if (!qLower || qNorm.length < 2) {
    return false;
  }

  const setCodigo = producto.atributosTcg.setCodigo?.trim().toLowerCase() ?? '';
  if (setCodigo) {
    if (setCodigo === qLower || normalizarClaveSet(setCodigo) === qNorm) {
      return true;
    }
  }

  const sku = producto.codigoSku.toLowerCase();
  const woo = producto.woo.sku.toLowerCase();
  // Prefijo de set en SKU: ME-ASC, ME-ASC-, ME-ASC-047…
  if (
    sku === qLower ||
    sku.startsWith(`${qLower}-`) ||
    sku.startsWith(`${qLower}_`) ||
    woo === qLower ||
    woo.startsWith(`${qLower}-`) ||
    woo.startsWith(`${qLower}_`)
  ) {
    return true;
  }

  // Sin guiones: MEASC ≈ ME-ASC-047 / ME_ASC_047
  const skuNorm = normalizarClaveSet(sku);
  const wooNorm = normalizarClaveSet(woo);
  return (
    (skuNorm.startsWith(qNorm) && (skuNorm.length === qNorm.length || /\d/.test(skuNorm[qNorm.length] ?? ''))) ||
    (wooNorm.startsWith(qNorm) && (wooNorm.length === qNorm.length || /\d/.test(wooNorm[qNorm.length] ?? '')))
  );
}

function etiquetaPrefijoSet(raw: string, productos: ProductoTcg[]): string {
  const setCodigos = [
    ...new Set(
      productos
        .map((p) => p.atributosTcg.setCodigo?.trim())
        .filter((c): c is string => !!c),
    ),
  ];
  if (setCodigos.length === 1) {
    return setCodigos[0];
  }
  return raw.trim().toUpperCase();
}

function normalizarNumeroFicha(valor: string): string {
  const texto = valor.trim().replace(/^#/, '').replace(/\/.*$/, '');
  if (!/^\d+$/.test(texto)) {
    return texto.toLowerCase();
  }
  return String(Number(texto));
}

function numeroFichaExacto(numero: string, query: string): boolean {
  const q = query.trim();
  if (!q) {
    return false;
  }
  return normalizarNumeroFicha(numero) === normalizarNumeroFicha(q);
}

function coincideFicha(ficha: TcgCarta, query: string): boolean {
  const q = query.trim().toLowerCase().replace(/^#/, '');
  if (!q) {
    return true;
  }
  if (/^\d+$/.test(q)) {
    return normalizarNumeroFicha(ficha.numero) === String(Number(q));
  }
  return (
    ficha.nombre.toLowerCase().includes(q) || ficha.numero.toLowerCase().includes(q)
  );
}
