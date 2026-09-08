import { ProductoTcg } from '../../productos-tcg/models/producto-tcg.model';
import { PedidoDigitalEntrega } from '../../pedidos-digitales/models/pedido-digital.model';

export type EstadoConexionWoo = 'CONECTADO' | 'DESCONECTADO' | 'ERROR_AUTENTICACION';

export type EstadoMapeoWoo =
  | 'SINCRONIZADO'
  | 'DESFASADO'
  | 'PENDIENTE_SUBIDA'
  | 'NO_MAPEADO';

export type TipoEventoSyncWoo = 'STOCK' | 'PRECIO' | 'PEDIDO';

export type ResultadoEventoSyncWoo = 'OK' | 'ERROR' | 'REINTENTO';

export type ModoRecepcionPedidosWoo = 'POLLING' | 'WEBHOOK';

/** Fila del CSV oficial: ID, SKU, Precio normal, Precio rebajado, Inventario. */
export interface FilaCsvWoo {
  ID: number | '';
  SKU: string;
  'Precio normal': number;
  'Precio rebajado': number | '';
  Inventario: number;
}

export interface ConfiguracionWooCommerce {
  urlTienda: string;
  consumerKey: string;
  consumerSecret: string;
  sedeOrigenId: string;
  tieneSecret?: boolean;
}

export interface EstadoConexionPanel {
  estado: EstadoConexionWoo;
  mensaje: string;
  ultimoIntento: string | null;
}

export interface FilaMapeoWoo {
  productoId: string;
  nombre: string;
  sku: string;
  wooCommerceId: number | null;
  wooVariationId: number | null;
  precioLocal: number;
  precioNormalWoo: number;
  precioRebajadoWoo: number | null;
  stockLocal: number;
  stockWoo: number | null;
  estadoMapeo: EstadoMapeoWoo;
  mensaje: string | null;
  ultimaSincronizacion: string | null;
}

export interface PedidoWooPendiente {
  idWoo: number;
  referenciaExterna: string;
  clienteNombre: string;
  clienteTelefono: string | null;
  pagado: boolean;
  observacion: string;
  lineas: Array<{
    productoId: string;
    sku: string;
    cantidad: number;
    precioUnitario: number;
  }>;
  entrega: PedidoDigitalEntrega;
}

export interface EventoSyncWoo {
  id: string;
  fechaHora: string;
  tipo: TipoEventoSyncWoo;
  itemsAfectados: string[];
  resultado: ResultadoEventoSyncWoo;
  detalle: string;
  intentos: number;
  proximoReintento: string | null;
}

export interface ResultadoSyncCatalogo {
  enviadas: number;
  omitidas: number;
  errores: number;
  filasCsv: FilaCsvWoo[];
}

export interface ResultadoSyncStock {
  publicados: number;
  omitidos: number;
  errores: number;
}

export interface ResultadoImportacionPedidos {
  importados: number;
  omitidos: number;
  errores: number;
  referencias: string[];
}

export interface WooKpis {
  conectado: boolean;
  sincronizados: number;
  desfasados: number;
  pendientesSubida: number;
  noMapeados: number;
  colaPedidos: number;
}

export const CONFIG_WOO_VACIA: ConfiguracionWooCommerce = {
  urlTienda: 'https://trunqitcg.com',
  consumerKey: '',
  consumerSecret: '',
  sedeOrigenId: 'sede-mira',
};

export const ETIQUETAS_CONEXION_WOO: Record<EstadoConexionWoo, string> = {
  CONECTADO: 'Conectado',
  DESCONECTADO: 'Desconectado',
  ERROR_AUTENTICACION: 'Error de Autenticación',
};

export const ETIQUETAS_MAPEO_WOO: Record<EstadoMapeoWoo, string> = {
  SINCRONIZADO: 'Sincronizado',
  DESFASADO: 'Desfasado',
  PENDIENTE_SUBIDA: 'Pendiente de Subida',
  NO_MAPEADO: 'No Mapeado',
};

export const ETIQUETAS_TIPO_SYNC: Record<TipoEventoSyncWoo, string> = {
  STOCK: 'Stock',
  PRECIO: 'Precio',
  PEDIDO: 'Pedido',
};

export function estadoMapeoDe(producto: ProductoTcg): EstadoMapeoWoo {
  if (producto.woo.wooCommerceId == null) {
    return 'NO_MAPEADO';
  }
  if (producto.woo.estadoSincronizacion === 'PENDIENTE') {
    return 'PENDIENTE_SUBIDA';
  }
  const stockDesfasado =
    producto.woo.stockWoo == null || producto.woo.stockWoo !== producto.stockLocal;
  const precioDesfasado = producto.woo.precioNormal !== producto.precioVenta;
  if (
    stockDesfasado ||
    precioDesfasado ||
    producto.woo.estadoSincronizacion === 'DESFASADO' ||
    producto.woo.estadoSincronizacion === 'ERROR'
  ) {
    return 'DESFASADO';
  }
  return 'SINCRONIZADO';
}

export function filaCsvDesdeProducto(producto: ProductoTcg): FilaCsvWoo {
  return {
    ID: producto.woo.wooCommerceId ?? '',
    SKU: producto.woo.sku || producto.codigoSku,
    'Precio normal': producto.woo.precioNormal || producto.precioVenta,
    'Precio rebajado': producto.woo.precioRebajado ?? '',
    Inventario: producto.stockLocal,
  };
}
