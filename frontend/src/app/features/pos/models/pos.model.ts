import type { PedidoDigital } from '../../pedidos-digitales/models/pedido-digital.model';
import type {
  EstadoEmisionSunat,
  TipoComprobanteSunat,
} from '../../ecosistema/models/ecosistema.model';

export type OrigenPagoPos = 'EFECTIVO' | 'YAPE' | 'PLIN' | 'TARJETA';

export type TipoComprobantePos = Extract<TipoComprobanteSunat, 'BOLETA' | 'FACTURA' | 'NOTA_VENTA'>;

export interface PosLineaCarrito {
  key: string;
  productoId: string;
  nombre: string;
  imagenUrl: string | null;
  precioUnitario: number;
  cantidad: number;
  /** Si viene de un pedido pendiente FB, no se recrea como ítem mostrador. */
  pedidoDigitalId?: string;
  pedidoCodigo?: string;
}

export interface CobrarYEmitirPosRequest {
  sedeId: string;
  pedidoDigitalIds?: string[];
  items?: Array<{
    productoId: string;
    cantidad: number;
    precioUnitario?: number;
  }>;
  clienteNombre?: string;
  tipoDocumento?: 'DNI' | 'RUC' | 'CE' | 'PASAPORTE' | 'SIN_DOCUMENTO' | null;
  numeroDocumento?: string | null;
  origenPago: OrigenPagoPos;
  codigoOperacion?: string;
  referenciaExterna?: string;
  montoRecibido?: number | null;
  tipoComprobante: TipoComprobantePos;
  observacion?: string;
}

export interface PosComprobanteResultado {
  id: string;
  ventaId: string;
  tipo: TipoComprobanteSunat;
  serie: string;
  correlativo: number;
  estado: EstadoEmisionSunat;
  mensaje: string | null;
  clienteNombre: string | null;
  total: number | null;
  tieneXml: boolean;
  tieneCdr: boolean;
  tienePdf: boolean;
  fechaEmision: string;
  hashFirma: string | null;
}

export interface CobrarYEmitirPosResponse {
  ventaId: string;
  comprobante: PosComprobanteResultado;
  pedidos: PedidoDigital[];
  pagos: Array<{
    id: string;
    origen: string;
    estado: string;
    monto: number;
    codigoOperacion: string | null;
    pedidoDigitalId: string | null;
  }>;
  totalCobrado: number;
  mensaje: string | null;
}

export const MEDIOS_PAGO_POS: ReadonlyArray<{ id: OrigenPagoPos; label: string }> = [
  { id: 'EFECTIVO', label: 'Efectivo' },
  { id: 'YAPE', label: 'Yape' },
  { id: 'PLIN', label: 'Plin' },
  { id: 'TARJETA', label: 'Tarjeta' },
];

export const TIPOS_CPE_POS: ReadonlyArray<{ id: TipoComprobantePos; label: string }> = [
  { id: 'BOLETA', label: 'Boleta' },
  { id: 'FACTURA', label: 'Factura' },
  { id: 'NOTA_VENTA', label: 'Nota de venta' },
];

export function round2(n: number): number {
  return Math.round((n + Number.EPSILON) * 100) / 100;
}
