import {
  AtributosTcg,
  CondicionTcg,
  EstadoSincronizacionWoo,
  IdiomaTcg,
  ProductoTcg,
  RarezaTcg,
  TipoProductoTcg,
  TipoSellado,
  crearAtributosTcgVacios,
  crearMetadatosWooVacios,
  juegoParaApi,
  normalizarJuego,
} from '../models/producto-tcg.model';

export interface ProductoCartaApi {
  cartaCatalogoId?: string | null;
  juego: string;
  setCodigo: string;
  setNombre: string;
  numeroCarta: string;
  rareza: RarezaTcg;
  idioma: IdiomaTcg;
  condicion: CondicionTcg;
  esFoil: boolean;
  artista?: string | null;
}

export interface ProductoSelladoApi {
  juego: string;
  edicion: string;
  tipoSellado: TipoSellado;
  cartasEsperadas: number;
  permiteApertura: boolean;
  contenidoFijo?: ProductoSelladoContenidoFijoApi[] | null;
}

export interface ProductoSelladoContenidoFijoApi {
  productoId: string;
  nombre?: string;
  codigoSku?: string;
  cantidad: number;
}

export interface ProductoTcgApi {
  id: string;
  tipoProducto: TipoProductoTcg;
  nombre: string;
  codigoSku: string;
  codigoBarras?: string | null;
  precioVenta: number;
  costo?: number | null;
  activo: boolean;
  fechaCreacion: string;
  imagenes?: string[] | string | null;
  stockLibre?: number | null;
  sedeStockId?: string | null;
  tieneDependencias?: boolean;
  wooVinculado?: boolean;
  woo?: ProductoWooResumenApi | null;
  carta?: ProductoCartaApi | null;
  sellado?: ProductoSelladoApi | null;
}

export interface ProductoWooResumenApi {
  wooProductId?: number | null;
  wooVariationId?: number | null;
  estadoMapeo: string;
  precioNormalWoo?: number;
  precioRebajadoWoo?: number | null;
  stockWoo?: number | null;
  mensaje?: string | null;
  ultimaSincronizacion?: string | null;
}

export interface EliminarProductoTcgApi {
  id: string;
  accion: 'ELIMINADA' | 'DESACTIVADA';
  motivo?: string | null;
  producto?: ProductoTcgApi | null;
}

export interface UpsertProductoTcgRequest {
  tipoProducto: TipoProductoTcg;
  nombre: string;
  codigoSku: string;
  codigoBarras?: string | null;
  precioVenta: number;
  costo?: number | null;
  imagenes?: string[];
  carta?: ProductoCartaApi;
  sellado?: Omit<ProductoSelladoApi, 'juego' | 'contenidoFijo'> & {
    juego: string;
    contenidoFijo?: Array<{ productoId: string; cantidad: number }>;
  };
  /** Solo en alta: sede del stock inicial. */
  sedeId?: string | null;
  stockInicial?: number;
  tipoIngresoStock?: 'AJUSTE' | 'INGRESO_COMPRA';
}

const EXTENSION_IMAGEN = /\.(png|jpe?g|gif|webp|avif|svg)(\?|#|$)/i;

export function urlsImagenesDesde(valor: unknown): string[] {
  if (Array.isArray(valor)) {
    return valor.flatMap((item) => urlsImagenesDesde(item));
  }
  if (typeof valor !== 'string' || !valor.trim()) {
    return [];
  }

  return valor
    .split(/[,;\n]+/)
    .map((parte) => extraerUrlHttp(parte))
    .filter((url): url is string => url != null);
}

export function primeraUrlImagen(valor: unknown): string | null {
  return urlsImagenesDesde(valor)[0] ?? null;
}

export function esUrlImagenDirecta(url: string): boolean {
  try {
    return EXTENSION_IMAGEN.test(new URL(url).pathname);
  } catch {
    return EXTENSION_IMAGEN.test(url);
  }
}

function extraerUrlHttp(valor: string): string | null {
  const texto = valor.trim().replace(/^<|>$/g, '');
  if (!texto) {
    return null;
  }

  const markdown = texto.match(/\((https?:\/\/[^)\s]+)\)/);
  const candidato = markdown?.[1] ?? texto.match(/https?:\/\/[^\s]+/i)?.[0] ?? texto;

  try {
    const url = new URL(candidato);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') {
      return null;
    }
    return url.href;
  } catch {
    return null;
  }
}

export function mapProductoFromApi(dto: ProductoTcgApi, stockLocal = 0): ProductoTcg {
  const stock =
    dto.stockLibre != null && Number.isFinite(Number(dto.stockLibre))
      ? Number(dto.stockLibre)
      : stockLocal;
  const juego = normalizarJuego(dto.carta?.juego ?? dto.sellado?.juego);
  const atributos: AtributosTcg = {
    ...crearAtributosTcgVacios(),
    setCodigo: dto.carta?.setCodigo ?? '',
    setNombre: dto.carta?.setNombre ?? dto.sellado?.edicion ?? '',
    numeroCarta: dto.carta?.numeroCarta ?? '',
    rareza: dto.carta?.rareza ?? '',
    condicion: dto.carta?.condicion ?? '',
    esFoil: dto.carta?.esFoil ?? false,
    idioma: dto.carta?.idioma ?? '',
    artista: dto.carta?.artista ?? '',
    tipoSellado: dto.sellado?.tipoSellado ?? '',
    edicion: dto.sellado?.edicion ?? '',
    cartasEsperadas: dto.sellado?.cartasEsperadas ?? null,
    permiteApertura: dto.sellado?.permiteApertura ?? true,
  };

  const imagenes = urlsImagenesDesde(dto.imagenes);
  const wooBase = crearMetadatosWooVacios(dto.codigoSku);
  const wooApi = dto.woo;
  const wooVinculado = dto.wooVinculado ?? wooApi?.wooProductId != null;

  return {
    id: dto.id,
    tipoProducto: dto.tipoProducto,
    nombre: dto.nombre,
    codigoSku: dto.codigoSku,
    codigoBarras: dto.codigoBarras ?? null,
    precioVenta: dto.precioVenta,
    costo: dto.costo ?? null,
    juego,
    activo: dto.activo,
    tieneDependencias: dto.tieneDependencias ?? false,
    wooVinculado,
    stockLocal: stock,
    cartaCatalogoId: dto.carta?.cartaCatalogoId ?? null,
    atributosTcg: atributos,
    componentes: [],
    contenidoFijo: (dto.sellado?.contenidoFijo ?? []).map((item) => ({
      productoId: item.productoId,
      sku: item.codigoSku ?? '',
      nombre: item.nombre ?? '',
      cantidad: item.cantidad,
    })),
    woo: {
      ...wooBase,
      wooCommerceId: wooApi?.wooProductId ?? null,
      precioNormal: wooApi?.precioNormalWoo && wooApi.precioNormalWoo > 0
        ? wooApi.precioNormalWoo
        : dto.precioVenta,
      precioRebajado: wooApi?.precioRebajadoWoo ?? null,
      stockWoo: wooApi?.stockWoo ?? null,
      imagenes,
      estadoSincronizacion: mapEstadoMapeoWoo(wooApi?.estadoMapeo),
      ultimaSincronizacion: wooApi?.ultimaSincronizacion ?? null,
      mensajeError: wooApi?.mensaje ?? null,
    },
  };
}

function mapEstadoMapeoWoo(estado?: string | null): EstadoSincronizacionWoo {
  switch ((estado ?? '').toUpperCase()) {
    case 'SINCRONIZADO':
      return 'SINCRONIZADO';
    case 'DESFASADO':
      return 'DESFASADO';
    case 'PENDIENTE_SUBIDA':
    case 'PENDIENTE':
      return 'PENDIENTE';
    case 'ERROR':
      return 'ERROR';
    default:
      return 'NO_MAPEADO';
  }
}

export function mapProductoToUpsert(
  producto: ProductoTcg,
  opciones?: { sedeId?: string | null; esAlta?: boolean },
): UpsertProductoTcgRequest {
  const juego = juegoParaApi(producto.juego);
  const request: UpsertProductoTcgRequest = {
    tipoProducto: producto.tipoProducto,
    nombre: producto.nombre.trim(),
    codigoSku: producto.codigoSku.trim(),
    codigoBarras: producto.codigoBarras,
    precioVenta: producto.precioVenta,
    costo: producto.costo,
    imagenes: urlsImagenesDesde(producto.woo.imagenes),
  };

  if (opciones?.esAlta) {
    const stockInicial = Math.max(0, Number(producto.stockLocal) || 0);
    request.stockInicial = stockInicial;
    request.sedeId = stockInicial > 0 ? (opciones.sedeId?.trim() || null) : null;
    request.tipoIngresoStock = 'AJUSTE';
  }

  if (producto.tipoProducto === 'CARTA') {
    request.carta = {
      juego,
      setCodigo: producto.atributosTcg.setCodigo.trim() || 'SET',
      setNombre: producto.atributosTcg.setNombre.trim() || producto.nombre,
      numeroCarta: producto.atributosTcg.numeroCarta.trim() || '000',
      rareza: (producto.atributosTcg.rareza || 'COMUN') as RarezaTcg,
      idioma: (producto.atributosTcg.idioma || 'ES') as IdiomaTcg,
      condicion: (producto.atributosTcg.condicion || 'NM') as CondicionTcg,
      esFoil: producto.atributosTcg.esFoil,
      artista: producto.atributosTcg.artista || null,
    };
  }

  if (producto.tipoProducto === 'SELLADO') {
    request.sellado = {
      juego,
      edicion: producto.atributosTcg.edicion.trim() || producto.atributosTcg.setNombre.trim() || producto.nombre,
      tipoSellado: (producto.atributosTcg.tipoSellado || 'OTRO') as TipoSellado,
      cartasEsperadas: producto.atributosTcg.cartasEsperadas ?? 0,
      permiteApertura: producto.atributosTcg.permiteApertura,
      contenidoFijo: producto.atributosTcg.permiteApertura
        ? producto.contenidoFijo.map((item) => ({
            productoId: item.productoId,
            cantidad: item.cantidad,
          }))
        : [],
    };
  }

  return request;
}

