/**
 * Catálogo TCG alineado con el blueprint y con la CSV oficial de WooCommerce:
 * ID, SKU, Name, Regular price, Sale price, Stock, Categories, Images, Attributes.
 */
export type TipoProductoTcg = 'CARTA' | 'SELLADO' | 'ACCESORIO' | 'COMPUESTO';

export type JuegoTcg = 'POKEMON' | 'MAGIC' | 'YUGIOH';

export type RarezaTcg =
  | 'COMUN'
  | 'INFRECUENTE'
  | 'RARA'
  | 'RARA_HOLO'
  | 'ULTRA'
  | 'SECRETA'
  | 'ESPECIAL'
  | 'DOBLE_RARA'
  | 'ILUSTRACION_RARA'
  | 'ILUSTRACION_ESPECIAL'
  | 'HIPER_RARA'
  | 'MEGA_HIPER_RARA'
  | 'PROMO';

export type TipoCartaTcg = 'POKEMON' | 'ENTRENADOR' | 'ENERGIA' | 'OTRO';

export type IdiomaTcg = 'ES' | 'EN' | 'JP' | 'OTRO';

export type CondicionTcg = 'NM' | 'LP' | 'MP' | 'HP' | 'DMG';

export type TipoSellado = 'SOBRE' | 'CAJA' | 'CASE' | 'TIN' | 'COLECCION' | 'OTRO';

export type TipoAccesorio = 'FUNDAS' | 'BINDER' | 'PLAYMAT' | 'DECKBOX' | 'TOALLA' | 'OTRO';

export type EstadoStock = 'EN_STOCK' | 'STOCK_BAJO' | 'SIN_STOCK';

export type EstadoSincronizacionWoo =
  | 'SINCRONIZADO'
  | 'DESFASADO'
  | 'PENDIENTE'
  | 'NO_MAPEADO'
  | 'ERROR';

export interface AtributosTcg {
  setCodigo: string;
  setNombre: string;
  numeroCarta: string;
  rareza: RarezaTcg | '';
  condicion: CondicionTcg | '';
  esFoil: boolean;
  idioma: IdiomaTcg | '';
  artista: string;
  tipoSellado: TipoSellado | '';
  edicion: string;
  cartasEsperadas: number | null;
  permiteApertura: boolean;
  tipoAccesorio: TipoAccesorio | '';
}

/** Atributo WooCommerce (columnas Attribute N name / Attribute N value(s)). */
export interface AtributoWoo {
  nombre: string;
  valores: string[];
}

export interface ComponenteCompuesto {
  productoHijoId: string;
  sku: string;
  nombre: string;
  cantidad: number;
}

export interface ContenidoFijoApertura {
  productoId: string;
  sku: string;
  nombre: string;
  cantidad: number;
}

/**
 * Metadatos de sincronización con WooCommerce.
 * Nombres de dominio Trunqi; el comentario indica la columna CSV.
 */
export interface MetadatosWooCommerce {
  /** CSV: ID */
  wooCommerceId: number | null;
  /** CSV: SKU */
  sku: string;
  /** CSV: Categories  (jerarquía con `>`) */
  categoriasWoo: string[];
  /** CSV: Regular price */
  precioNormal: number;
  /** CSV: Sale price */
  precioRebajado: number | null;
  /** CSV: Stock */
  stockWoo: number | null;
  /** CSV: Images (URLs separadas por coma) */
  imagenes: string[];
  /** CSV: Attribute N name / value(s) */
  atributos: AtributoWoo[];
  estadoSincronizacion: EstadoSincronizacionWoo;
  ultimaSincronizacion: string | null;
  mensajeError: string | null;
}

export interface ProductoTcg {
  id: string;
  tipoProducto: TipoProductoTcg;
  nombre: string;
  codigoSku: string;
  codigoBarras: string | null;
  precioVenta: number;
  costo: number | null;
  /** Código conocido (POKEMON/…) o etiqueta libre de juego/categoría. */
  juego: string;
  activo: boolean;
  stockLocal: number;
  cartaCatalogoId: string | null;
  atributosTcg: AtributosTcg;
  componentes: ComponenteCompuesto[];
  contenidoFijo: ContenidoFijoApertura[];
  woo: MetadatosWooCommerce;
}

export interface ProductosTcgFiltros {
  busqueda: string;
  tipoProducto: TipoProductoTcg | 'TODOS';
  juego: string | 'TODOS';
  estadoStock: EstadoStock | 'TODOS';
  sincronizacionWoo: EstadoSincronizacionWoo | 'TODOS';
}

export const FILTROS_PRODUCTOS_VACIOS: ProductosTcgFiltros = {
  busqueda: '',
  tipoProducto: 'TODOS',
  juego: 'TODOS',
  estadoStock: 'TODOS',
  sincronizacionWoo: 'TODOS',
};

export const UMBRAL_STOCK_BAJO = 3;

export function crearAtributosTcgVacios(): AtributosTcg {
  return {
    setCodigo: '',
    setNombre: '',
    numeroCarta: '',
    rareza: '',
    condicion: '',
    esFoil: false,
    idioma: '',
    artista: '',
    tipoSellado: '',
    edicion: '',
    cartasEsperadas: null,
    permiteApertura: true,
    tipoAccesorio: '',
  };
}

export function crearMetadatosWooVacios(sku = ''): MetadatosWooCommerce {
  return {
    wooCommerceId: null,
    sku,
    categoriasWoo: [],
    precioNormal: 0,
    precioRebajado: null,
    stockWoo: null,
    imagenes: [],
    atributos: [],
    estadoSincronizacion: 'NO_MAPEADO',
    ultimaSincronizacion: null,
    mensajeError: null,
  };
}

export function estadoStockDe(cantidad: number): EstadoStock {
  if (cantidad <= 0) {
    return 'SIN_STOCK';
  }
  if (cantidad <= UMBRAL_STOCK_BAJO) {
    return 'STOCK_BAJO';
  }
  return 'EN_STOCK';
}

export function precioVigente(producto: ProductoTcg): number {
  if (producto.precioVenta > 0) {
    return producto.precioVenta;
  }
  const rebajado = producto.woo.precioRebajado;
  if (rebajado != null && rebajado > 0 && rebajado < producto.woo.precioNormal) {
    return rebajado;
  }
  return producto.woo.precioNormal || 0;
}

export const ETIQUETAS_TIPO: Record<TipoProductoTcg, string> = {
  CARTA: 'Carta',
  SELLADO: 'Sellado',
  ACCESORIO: 'Accesorio',
  COMPUESTO: 'Compuesto',
};

export const ETIQUETAS_JUEGO: Record<JuegoTcg, string> = {
  POKEMON: 'Pokémon',
  MAGIC: 'Magic',
  YUGIOH: 'Yu-Gi-Oh!',
};

/** Etiqueta visible: código conocido → nombre; custom → tal cual. */
export function etiquetaJuego(juego: string | null | undefined): string {
  const valor = (juego ?? '').trim();
  if (!valor) {
    return '';
  }
  return ETIQUETAS_JUEGO[valor as JuegoTcg] ?? valor;
}

/** Texto que espera el API (siempre etiqueta legible). */
export function juegoParaApi(juego: string | null | undefined): string {
  const etiqueta = etiquetaJuego(juego);
  return etiqueta || 'Pokémon';
}

/**
 * Normaliza un juego desde API o input libre a valor de formulario:
 * códigos conocidos si coincide; si no, la etiqueta trimmeada.
 */
export function normalizarJuego(valor?: string | null): string {
  const texto = (valor ?? '').trim();
  if (!texto) {
    return '';
  }
  const lower = texto.toLowerCase();
  for (const [codigo, etiqueta] of Object.entries(ETIQUETAS_JUEGO) as [JuegoTcg, string][]) {
    if (codigo === texto || etiqueta.toLowerCase() === lower) {
      return codigo;
    }
  }
  if (lower.includes('pok')) {
    return 'POKEMON';
  }
  if (lower.includes('magic')) {
    return 'MAGIC';
  }
  if (lower.includes('yu') || lower.includes('gi-oh') || lower.includes('yugioh')) {
    return 'YUGIOH';
  }
  return texto;
}

export const ETIQUETAS_RAREZA: Record<RarezaTcg, string> = {
  COMUN: 'Común',
  INFRECUENTE: 'Infrecuente',
  RARA: 'Rara',
  RARA_HOLO: 'Rara holo',
  ULTRA: 'Ultra rara',
  SECRETA: 'Secreta',
  ESPECIAL: 'Especial',
  DOBLE_RARA: 'Doble rara',
  ILUSTRACION_RARA: 'Illustration rare',
  ILUSTRACION_ESPECIAL: 'Special illustration rare',
  HIPER_RARA: 'Hiper rara',
  MEGA_HIPER_RARA: 'Mega hyper rare',
  PROMO: 'Promo',
};

export const ETIQUETAS_TIPO_CARTA: Record<TipoCartaTcg, string> = {
  POKEMON: 'Pokémon',
  ENTRENADOR: 'Entrenador',
  ENERGIA: 'Energía',
  OTRO: 'Otro',
};

export const ETIQUETAS_IDIOMA: Record<IdiomaTcg, string> = {
  ES: 'Español',
  EN: 'Inglés',
  JP: 'Japonés',
  OTRO: 'Otro',
};

export const ETIQUETAS_CONDICION: Record<CondicionTcg, string> = {
  NM: 'Near Mint',
  LP: 'Lightly Played',
  MP: 'Moderately Played',
  HP: 'Heavily Played',
  DMG: 'Damaged',
};

export const ETIQUETAS_SELLADO: Record<TipoSellado, string> = {
  SOBRE: 'Sobre',
  CAJA: 'Caja',
  CASE: 'Case',
  TIN: 'Tin',
  COLECCION: 'Colección',
  OTRO: 'Otro',
};

export const ETIQUETAS_ACCESORIO: Record<TipoAccesorio, string> = {
  FUNDAS: 'Fundas',
  BINDER: 'Binder',
  PLAYMAT: 'Playmat',
  DECKBOX: 'Deck box',
  TOALLA: 'Toalla',
  OTRO: 'Otro',
};

export const ETIQUETAS_STOCK: Record<EstadoStock, string> = {
  EN_STOCK: 'En stock',
  STOCK_BAJO: 'Stock bajo',
  SIN_STOCK: 'Sin stock',
};

export const ETIQUETAS_SYNC: Record<EstadoSincronizacionWoo, string> = {
  SINCRONIZADO: 'Sincronizado',
  DESFASADO: 'Desfasado',
  PENDIENTE: 'Pendiente',
  NO_MAPEADO: 'No mapeado',
  ERROR: 'Error',
};
