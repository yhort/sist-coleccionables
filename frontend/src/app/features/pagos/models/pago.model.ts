import { round2 } from '../../pedidos-digitales/models/pedido-digital.model';

export type OrigenPago =
  | 'YAPE'
  | 'PLIN'
  | 'IZIPAY'
  | 'TARJETA'
  | 'EFECTIVO'
  | 'TRANSFERENCIA'
  | 'OTRO';

export type EstadoPago = 'NOTIFICADO' | 'ASOCIADO' | 'CONFIRMADO' | 'RECHAZADO' | 'ANULADO';

export interface Pago {
  id: string;
  origen: OrigenPago;
  estado: EstadoPago;
  monto: number;
  codigoOperacion: string | null;
  referenciaExterna: string | null;
  pedidoDigitalId: string | null;
  pedidoCodigo: string | null;
  ventaId: string | null;
  clienteNombre: string | null;
  fechaNotificacion: string;
  fechaConfirmacion: string | null;
  usuarioAsocioNombre: string | null;
  observacion: string | null;
}

export interface PagosFiltros {
  origen: OrigenPago | 'TODOS';
  estado: EstadoPago | 'TODOS';
  busqueda: string;
  montoMin: number | null;
  montoMax: number | null;
  /** Fecha local `YYYY-MM-DD` (input type="date"). Vacío = sin límite. */
  desde: string;
  /** Fecha local `YYYY-MM-DD` (input type="date"). Vacío = sin límite. */
  hasta: string;
}

export interface RegistrarPagoRequest {
  origen: OrigenPago;
  monto: number;
  codigoOperacion?: string | null;
  referenciaExterna?: string | null;
  pedidoDigitalId?: string | null;
  clienteNombre?: string | null;
  observacion?: string | null;
  confirmar?: boolean;
}

export interface RegistrarPagoLoteRequest {
  origen: OrigenPago;
  monto: number;
  codigoOperacion?: string | null;
  referenciaExterna?: string | null;
  observacion?: string | null;
  confirmar?: boolean;
  pedidoDigitalIds: string[];
}

export interface PagosKpis {
  recaudadoHoy: number;
  cantidadHoy: number;
  pendientesConciliar: number;
  montoPendienteConciliar: number;
  desglose: Record<OrigenPago, number>;
}

export const ORIGENES_PAGO: readonly OrigenPago[] = [
  'YAPE',
  'PLIN',
  'IZIPAY',
  'TARJETA',
  'TRANSFERENCIA',
  'EFECTIVO',
  'OTRO',
];

/** Métodos rápidos del modo POS / caja en Nuevo Pedido. */
export const ORIGENES_PAGO_POS: readonly OrigenPago[] = [
  'YAPE',
  'PLIN',
  'EFECTIVO',
  'TARJETA',
  'TRANSFERENCIA',
];

export const ESTADOS_PAGO: readonly EstadoPago[] = [
  'NOTIFICADO',
  'ASOCIADO',
  'CONFIRMADO',
  'RECHAZADO',
  'ANULADO',
];

export const FILTROS_PAGOS_VACIOS: PagosFiltros = {
  origen: 'TODOS',
  estado: 'TODOS',
  busqueda: '',
  montoMin: null,
  montoMax: null,
  desde: '',
  hasta: '',
};

/** Fecha local en formato `YYYY-MM-DD` (input type="date"). */
export function fechaLocalHoy(): string {
  const ahora = new Date();
  const yyyy = ahora.getFullYear();
  const mm = String(ahora.getMonth() + 1).padStart(2, '0');
  const dd = String(ahora.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}

/** Filtros iniciales de la bandeja: operación del día en curso (UTC-5 Perú vía zona del navegador). */
export function crearFiltrosPagosDiaActual(): PagosFiltros {
  const hoy = fechaLocalHoy();
  return {
    ...FILTROS_PAGOS_VACIOS,
    desde: hoy,
    hasta: hoy,
  };
}

export const ETIQUETAS_ORIGEN_PAGO: Record<OrigenPago, string> = {
  YAPE: 'Yape',
  PLIN: 'Plin',
  IZIPAY: 'Izipay',
  TARJETA: 'Tarjeta',
  TRANSFERENCIA: 'Transferencia',
  EFECTIVO: 'Efectivo',
  OTRO: 'Otro',
};

export const ETIQUETAS_ESTADO_PAGO: Record<EstadoPago, string> = {
  NOTIFICADO: 'Notificado',
  ASOCIADO: 'Asociado',
  CONFIRMADO: 'Confirmado',
  RECHAZADO: 'Rechazado',
  ANULADO: 'Anulado',
};

export function normalizarCodigoOperacion(codigo: string | null | undefined): string {
  return (codigo ?? '').trim().toUpperCase();
}

export function esOrigenDigital(origen: OrigenPago): boolean {
  return origen === 'YAPE' || origen === 'PLIN' || origen === 'IZIPAY' || origen === 'TARJETA';
}

export { round2 };
