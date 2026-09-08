import { TipoProductoTcg } from '../../productos-tcg/models/producto-tcg.model';

export type TipoMovimientoInventario =
  | 'AJUSTE'
  | 'INGRESO_COMPRA'
  | 'RESERVA'
  | 'LIBERACION_RESERVA'
  | 'VENTA'
  | 'ANULACION_VENTA'
  | 'APERTURA_SALIDA_SELLADO'
  | 'APERTURA_INGRESO_CARTA'
  | 'ARMADO_COMPUESTO'
  | 'DESARME_COMPUESTO'
  | 'PUJA_GANADORA_RESERVA';

export type SentidoAjuste = 'ENTRADA' | 'SALIDA';

export type ReferenciaMovimiento =
  | 'AJUSTE_MANUAL'
  | 'APERTURA_TCG'
  | 'SUBASTA_TCG'
  | 'PEDIDO_DIGITAL'
  | 'VENTA'
  | 'COMPRA'
  | 'ENTREGA'
  | 'COMPUESTO';

export interface SedeInventario {
  id: string;
  nombre: string;
  tipo: 'TIENDA' | 'ALMACEN';
}

export interface StockProductoSede {
  id: string;
  sedeId: string;
  productoId: string;
  cantidadDisponible: number;
  cantidadReservada: number;
}

export interface MovimientoInventario {
  id: string;
  sedeId: string;
  productoId: string;
  tipoMovimiento: TipoMovimientoInventario;
  cantidad: number;
  stockAnterior: number;
  stockPosterior: number;
  reservadoAnterior: number;
  reservadoPosterior: number;
  referenciaTipo: ReferenciaMovimiento;
  referenciaId: string | null;
  motivo: string;
  usuario: string;
  fechaCreacion: string;
}

export interface StockFila extends StockProductoSede {
  productoNombre: string;
  codigoSku: string;
  tipoProducto: TipoProductoTcg;
  sedeNombre: string;
  cantidadLibre: number;
}

export interface KardexFila extends MovimientoInventario {
  productoNombre: string;
  codigoSku: string;
  tipoProducto: TipoProductoTcg;
  sedeNombre: string;
}

export interface KardexFiltros {
  busqueda: string;
  sedeId: string | 'TODAS';
  productoId: string | 'TODOS';
  tipoMovimiento: TipoMovimientoInventario | 'TODOS';
  desde: string;
  hasta: string;
}

export interface AjusteInventarioRequest {
  sedeId: string;
  productoId: string;
  tipoMovimiento: TipoMovimientoInventario;
  sentido: SentidoAjuste;
  cantidad: number;
  motivo: string;
}

/** Movimiento de kardex con origen de negocio (apertura, compra, etc.). */
export interface RegistrarMovimientoRequest extends AjusteInventarioRequest {
  referenciaTipo: ReferenciaMovimiento;
  referenciaId: string | null;
  usuario?: string;
  /** Invierte el efecto en stock (anulación de una operación confirmada). */
  invertir?: boolean;
}

export const SEDES_INVENTARIO: readonly SedeInventario[] = [
  { id: 'sede-mira', nombre: 'Tienda Miraflores', tipo: 'TIENDA' },
  { id: 'sede-surco', nombre: 'Almacén Surco', tipo: 'ALMACEN' },
];

export const TIPOS_MOVIMIENTO_TCG: readonly TipoMovimientoInventario[] = [
  'APERTURA_SALIDA_SELLADO',
  'APERTURA_INGRESO_CARTA',
  'ARMADO_COMPUESTO',
  'DESARME_COMPUESTO',
  'PUJA_GANADORA_RESERVA',
];

export const ETIQUETAS_MOVIMIENTO: Record<TipoMovimientoInventario, string> = {
  AJUSTE: 'Ajuste manual',
  INGRESO_COMPRA: 'Ingreso por compra',
  RESERVA: 'Reserva (pedido)',
  LIBERACION_RESERVA: 'Liberación de reserva',
  VENTA: 'Venta',
  ANULACION_VENTA: 'Anulación de venta',
  APERTURA_SALIDA_SELLADO: 'Apertura · salida sellado',
  APERTURA_INGRESO_CARTA: 'Apertura · ingreso carta',
  ARMADO_COMPUESTO: 'Armado de compuesto',
  DESARME_COMPUESTO: 'Desarme de compuesto',
  PUJA_GANADORA_RESERVA: 'Puja ganadora · reserva',
};

export const GRUPOS_MOVIMIENTO: readonly {
  etiqueta: string;
  tipos: readonly TipoMovimientoInventario[];
}[] = [
  { etiqueta: 'Ajuste', tipos: ['AJUSTE'] },
  {
    etiqueta: 'TCG',
    tipos: TIPOS_MOVIMIENTO_TCG,
  },
  {
    etiqueta: 'Reservas y ventas',
    tipos: ['RESERVA', 'LIBERACION_RESERVA', 'VENTA', 'ANULACION_VENTA', 'INGRESO_COMPRA'],
  },
];

export function cantidadLibreDe(stock: StockProductoSede): number {
  return stock.cantidadDisponible - stock.cantidadReservada;
}

export function esTipoMovimientoTcg(tipo: TipoMovimientoInventario): boolean {
  return (TIPOS_MOVIMIENTO_TCG as readonly string[]).includes(tipo);
}

export function sentidoPorDefecto(tipo: TipoMovimientoInventario): SentidoAjuste | 'RESERVA' | 'LIBERACION' {
  switch (tipo) {
    case 'APERTURA_INGRESO_CARTA':
    case 'INGRESO_COMPRA':
    case 'ANULACION_VENTA':
      return 'ENTRADA';
    case 'APERTURA_SALIDA_SELLADO':
    case 'VENTA':
      return 'SALIDA';
    case 'PUJA_GANADORA_RESERVA':
    case 'RESERVA':
      return 'RESERVA';
    case 'LIBERACION_RESERVA':
      return 'LIBERACION';
    default:
      return 'ENTRADA';
  }
}

export function requiereSentidoManual(tipo: TipoMovimientoInventario): boolean {
  return tipo === 'AJUSTE' || tipo === 'ARMADO_COMPUESTO' || tipo === 'DESARME_COMPUESTO';
}

export const FILTROS_KARDEX_VACIOS: KardexFiltros = {
  busqueda: '',
  sedeId: 'TODAS',
  productoId: 'TODOS',
  tipoMovimiento: 'TODOS',
  desde: '',
  hasta: '',
};
