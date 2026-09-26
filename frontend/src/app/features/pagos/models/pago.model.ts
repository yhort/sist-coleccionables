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
};

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
