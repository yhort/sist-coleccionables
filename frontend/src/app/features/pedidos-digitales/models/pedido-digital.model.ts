import type { CanalContactoCliente } from '../../../shared/models/contacto-entrega.model';

export type { CanalContactoCliente };

export type CanalPedidoDigital =
  | 'FACEBOOK_SUBASTA'
  | 'FACEBOOK_MARKETPLACE'
  | 'WHATSAPP'
  | 'INSTAGRAM'
  | 'TIKTOK'
  | 'WOOCOMMERCE'
  | 'WEB'
  | 'OTRO';

export type OrigenPedidoDigital =
  | 'FACEBOOK_SUBASTA'
  | 'WEB_WOOCOMMERCE'
  | 'WHATSAPP_DIRECTO'
  | 'TIENDA_PRESENCIAL';

export type EstadoPedidoDigital =
  | 'PendientePago'
  | 'Pagado'
  | 'Empaquetado'
  | 'PendienteEntrega'
  | 'Entregado'
  | 'Cancelado'
  | 'Anulado'
  | 'Devuelto';

export type IndicadorReservaPedido = 'Reservado' | 'Liberado' | 'Confirmado';

export interface PedidoDigitalDetalle {
  id: string;
  codigo?: string;
  productoId: string;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
  total: number;
}

export interface PedidoDigitalHistorialEstado {
  id: string;
  estadoAnterior: EstadoPedidoDigital | null;
  estadoNuevo: EstadoPedidoDigital;
  usuarioNombre: string | null;
  fecha: string;
  observacion: string | null;
}

export interface PedidoDigitalEntrega {
  destinatarioNombre: string;
  destinatarioTelefono: string | null;
  direccion: string | null;
  distrito: string | null;
  provincia: string | null;
  departamento: string | null;
  courier: string | null;
  esRecojoTienda: boolean;
  numeroTracking?: string | null;
  costoEnvio?: number | null;
  notasEmpaque?: string | null;
  agencia?: string | null;
  puntoEntrega?: string | null;
  canalContacto?: CanalContactoCliente | null;
  contactoReferencia?: string | null;
}

export interface PedidoDigital {
  id: string;
  codigo: string;
  clienteId: string | null;
  clienteNombre: string;
  clienteTelefono: string | null;
  clienteTipoDocumento?: 'DNI' | 'RUC' | 'CE' | 'PASAPORTE' | 'SIN_DOCUMENTO' | null;
  clienteNumeroDocumento?: string | null;
  sedeId: string;
  sedeNombre?: string;
  canalPedido: CanalPedidoDigital;
  estado: EstadoPedidoDigital;
  indicadorReserva?: IndicadorReservaPedido;
  fechaPedido: string;
  subtotal: number;
  igv: number;
  total: number;
  referenciaExterna: string | null;
  observacion: string | null;
  subastaTcgId: string | null;
  codigoSubasta: string | null;
  /** Título del live / evento de subasta (si aplica). */
  tituloSubasta: string | null;
  /** Resumen WhatsApp copiado/enviado al cliente. */
  notificado: boolean;
  fechaNotificacion: string | null;
  ventaId: string | null;
  codigoVenta: string | null;
  /** Documento de venta (boleta/factura/NV) si ya se emitió. */
  comprobante: PedidoComprobanteResumen | null;
  /** Nota de crédito asociada, si existe. */
  notaCredito: PedidoComprobanteResumen | null;
  detalles: PedidoDigitalDetalle[];
  historialEstados: PedidoDigitalHistorialEstado[];
  entrega: PedidoDigitalEntrega;
}

export interface PedidoComprobanteResumen {
  id: string;
  tipo: 'BOLETA' | 'FACTURA' | 'NOTA_VENTA' | 'NOTA_CREDITO' | 'GUIA_REMISION';
  serie: string;
  correlativo: number;
  estado: string;
  documentoReferencia?: string | null;
  codigoMotivo?: string | null;
  descripcionMotivo?: string | null;
}

export type FiltroNotificacionPedido = 'TODOS' | 'SIN_NOTIFICAR' | 'NOTIFICADOS';

export interface PedidosDigitalesFiltros {
  origen: OrigenPedidoDigital | 'TODOS';
  /** Búsqueda: N° pedido, tracking, carta/producto… */
  busqueda: string;
  /** Filtro dedicado por subasta/evento (id o 'TODAS'). */
  subastaTcgId: string | 'TODAS';
  /** Filtro dedicado por nombre de cliente. */
  cliente: string;
  notificacion: FiltroNotificacionPedido;
  desde: string;
  hasta: string;
  montoMin: number | null;
  montoMax: number | null;
}

export interface CrearPedidoDigitalDetalleInput {
  productoId: string;
  cantidad: number;
  precioUnitario: number;
}

export interface CrearPedidoDigitalRequest {
  clienteId?: string | null;
  clienteNombre: string;
  clienteTelefono?: string | null;
  tipoDocumento?: 'DNI' | 'RUC' | 'CE' | 'PASAPORTE' | 'SIN_DOCUMENTO' | null;
  numeroDocumento?: string | null;
  esClienteVarios?: boolean;
  sedeId: string;
  canalPedido: CanalPedidoDigital;
  observacion?: string | null;
  detalles: CrearPedidoDigitalDetalleInput[];
  entrega: PedidoDigitalEntrega;
  guardarPuntoEnCliente?: boolean;
  cobroInmediato?: CobroInmediatoPedidoInput | null;
}

export interface CobroInmediatoPedidoInput {
  origen: 'YAPE' | 'PLIN' | 'EFECTIVO' | 'TARJETA' | 'TRANSFERENCIA';
  codigoOperacion?: string | null;
  referenciaExterna?: string | null;
  montoRecibido?: number | null;
}

export interface CrearPedidoDesdeSubastaRequest {
  id?: string;
  clienteId: string | null;
  clienteNombre: string;
  sedeId: string;
  canalPedido: CanalPedidoDigital;
  total: number;
  productoId: string;
  descripcion: string;
  subastaTcgId: string;
  observacion?: string | null;
}

export const TASA_IGV = 0.18;

export const COLUMNAS_KANBAN: readonly EstadoPedidoDigital[] = [
  'PendientePago',
  'Pagado',
  'Empaquetado',
  'PendienteEntrega',
  'Entregado',
  'Cancelado',
  'Anulado',
  'Devuelto',
];

export const FLUJO_OPERATIVO: readonly EstadoPedidoDigital[] = [
  'PendientePago',
  'Pagado',
  'Empaquetado',
  'PendienteEntrega',
  'Entregado',
];

export const ESTADOS_TERMINALES_PEDIDO: readonly EstadoPedidoDigital[] = [
  'Entregado',
  'Cancelado',
  'Anulado',
  'Devuelto',
];

export const ESTADOS_BLOQUEO_NC: readonly EstadoPedidoDigital[] = ['Anulado', 'Devuelto'];

export const CANALES_PEDIDO: readonly CanalPedidoDigital[] = [
  'FACEBOOK_SUBASTA',
  'FACEBOOK_MARKETPLACE',
  'WHATSAPP',
  'INSTAGRAM',
  'TIKTOK',
  'WOOCOMMERCE',
  'WEB',
  'OTRO',
];

export const ORIGENES_PEDIDO: readonly OrigenPedidoDigital[] = [
  'FACEBOOK_SUBASTA',
  'WEB_WOOCOMMERCE',
  'WHATSAPP_DIRECTO',
  'TIENDA_PRESENCIAL',
];

export const FILTROS_PEDIDOS_VACIOS: PedidosDigitalesFiltros = {
  origen: 'TODOS',
  busqueda: '',
  subastaTcgId: 'TODAS',
  cliente: '',
  notificacion: 'TODOS',
  desde: '',
  hasta: '',
  montoMin: null,
  montoMax: null,
};

export const ETIQUETAS_CANAL_PEDIDO: Record<CanalPedidoDigital, string> = {
  FACEBOOK_SUBASTA: 'Facebook subasta',
  FACEBOOK_MARKETPLACE: 'Facebook Marketplace',
  WHATSAPP: 'WhatsApp',
  INSTAGRAM: 'Instagram',
  TIKTOK: 'TikTok',
  WOOCOMMERCE: 'WooCommerce',
  WEB: 'Web',
  OTRO: 'Otro',
};

export const ETIQUETAS_ORIGEN_PEDIDO: Record<OrigenPedidoDigital, string> = {
  FACEBOOK_SUBASTA: 'FACEBOOK_SUBASTA',
  WEB_WOOCOMMERCE: 'WEB_WOOCOMMERCE',
  WHATSAPP_DIRECTO: 'WHATSAPP_DIRECTO',
  TIENDA_PRESENCIAL: 'TIENDA_PRESENCIAL',
};

export const ETIQUETAS_ESTADO_PEDIDO: Record<EstadoPedidoDigital, string> = {
  PendientePago: 'Pendiente de pago',
  Pagado: 'Pagado',
  Empaquetado: 'Empaquetado',
  PendienteEntrega: 'Pendiente de entrega',
  Entregado: 'Entregado',
  Cancelado: 'Cancelado',
  Anulado: 'Anulado',
  Devuelto: 'Devuelto',
};

/** Etiqueta de acción operativa (botones Empaquetar / Despachar / Entregar). */
export const ETIQUETAS_ACCION_ESTADO_PEDIDO: Record<EstadoPedidoDigital, string> = {
  PendientePago: 'Marcar pendiente de pago',
  Pagado: 'Marcar pagado',
  Empaquetado: 'Empaquetar',
  PendienteEntrega: 'Despachar',
  Entregado: 'Entregar',
  Cancelado: 'Cancelar',
  Anulado: 'Anulado',
  Devuelto: 'Devuelto',
};

export function etiquetaAccionEstado(destino: EstadoPedidoDigital): string {
  return ETIQUETAS_ACCION_ESTADO_PEDIDO[destino];
}

export function round2(valor: number): number {
  return Math.round(valor * 100) / 100;
}

export function desgloseIgvDesdeTotal(
  total: number,
  tasa = TASA_IGV,
): {
  subtotal: number;
  igv: number;
  total: number;
} {
  const totalR = round2(total);
  const subtotal = round2(totalR / (1 + tasa));
  return { subtotal, igv: round2(totalR - subtotal), total: totalR };
}

export function origenDeCanal(canal: CanalPedidoDigital): OrigenPedidoDigital {
  switch (canal) {
    case 'FACEBOOK_SUBASTA':
    case 'FACEBOOK_MARKETPLACE':
      return 'FACEBOOK_SUBASTA';
    case 'WOOCOMMERCE':
    case 'WEB':
    case 'INSTAGRAM':
    case 'TIKTOK':
      return 'WEB_WOOCOMMERCE';
    case 'WHATSAPP':
      return 'WHATSAPP_DIRECTO';
    default:
      return 'TIENDA_PRESENCIAL';
  }
}

export function canalDesdeOrigen(origen: OrigenPedidoDigital): CanalPedidoDigital {
  switch (origen) {
    case 'FACEBOOK_SUBASTA':
      return 'FACEBOOK_SUBASTA';
    case 'WEB_WOOCOMMERCE':
      return 'WOOCOMMERCE';
    case 'WHATSAPP_DIRECTO':
      return 'WHATSAPP';
    case 'TIENDA_PRESENCIAL':
      return 'OTRO';
  }
}

export function indicadorReservaDe(estado: EstadoPedidoDigital): IndicadorReservaPedido {
  if (estado === 'Cancelado' || estado === 'Anulado' || estado === 'Devuelto') {
    return 'Liberado';
  }
  if (estado === 'Entregado') {
    return 'Confirmado';
  }
  return 'Reservado';
}

export function pedidoBloqueadoPorNc(
  pedido: Pick<PedidoDigital, 'estado' | 'notaCredito'>,
): boolean {
  if (pedido.estado === 'Anulado' || pedido.estado === 'Devuelto') {
    return true;
  }
  return cpeEstadoEmitido(pedido.notaCredito?.estado);
}

export function transicionesPermitidas(
  pedido: Pick<PedidoDigital, 'estado' | 'entrega' | 'notaCredito'>,
): EstadoPedidoDigital[] {
  const { estado } = pedido;
  if (
    estado === 'Entregado' ||
    estado === 'Cancelado' ||
    estado === 'Anulado' ||
    estado === 'Devuelto' ||
    pedidoBloqueadoPorNc(pedido)
  ) {
    return [];
  }

  const siguientes: EstadoPedidoDigital[] = [];
  const indice = FLUJO_OPERATIVO.indexOf(estado);
  const siguiente = indice >= 0 ? FLUJO_OPERATIVO[indice + 1] : undefined;
  if (siguiente && siguiente !== 'Entregado') {
    siguientes.push(siguiente);
  }

  const puedeEntregar =
    estado === 'PendienteEntrega' ||
    (pedido.entrega.esRecojoTienda && (estado === 'Pagado' || estado === 'Empaquetado'));
  if (puedeEntregar) {
    siguientes.push('Entregado');
  }

  siguientes.push('Cancelado');
  return siguientes;
}

/**
 * Acceso directo "Entregar":
 * - Recojo: desde Pagado / Empaquetado / PendienteEntrega (mostrador).
 * - Envío: solo desde PendienteEntrega (tras Empaquetar → Despachar).
 */
export function puedeEntregarRapido(
  pedido: Pick<PedidoDigital, 'estado' | 'entrega' | 'notaCredito'>,
): boolean {
  return transicionesPermitidas(pedido).includes('Entregado');
}

export function puedeEmpaquetar(
  pedido: Pick<PedidoDigital, 'estado' | 'entrega' | 'notaCredito'>,
): boolean {
  if (pedidoBloqueadoPorNc(pedido)) {
    return false;
  }
  return pedido.estado === 'Pagado' && transicionesPermitidas(pedido).includes('Empaquetado');
}

export function puedeDespachar(
  pedido: Pick<PedidoDigital, 'estado' | 'entrega' | 'notaCredito'>,
): boolean {
  if (pedidoBloqueadoPorNc(pedido)) {
    return false;
  }
  return (
    pedido.estado === 'Empaquetado' &&
    transicionesPermitidas(pedido).includes('PendienteEntrega')
  );
}

export function puedeTransicionarA(
  pedido: Pick<PedidoDigital, 'estado' | 'entrega' | 'notaCredito'>,
  destino: EstadoPedidoDigital,
): boolean {
  return transicionesPermitidas(pedido).includes(destino);
}

export function puedeAnularPedido(pedido: Pick<PedidoDigital, 'estado'>): boolean {
  return pedido.estado === 'PendientePago';
}

export function puedeImprimirEtiqueta(
  pedido: Pick<PedidoDigital, 'estado' | 'notaCredito'>,
): boolean {
  if (pedidoBloqueadoPorNc(pedido)) {
    return false;
  }
  return (
    pedido.estado === 'Pagado' ||
    pedido.estado === 'Empaquetado' ||
    pedido.estado === 'PendienteEntrega'
  );
}

export function puedeSeleccionarParaCobro(pedido: Pick<PedidoDigital, 'estado'>): boolean {
  return pedido.estado === 'PendientePago';
}

export function puedeSeleccionarEnLote(
  pedido: Pick<PedidoDigital, 'estado' | 'notaCredito'>,
  tab: EstadoPedidoDigital | 'TODOS',
): boolean {
  if (
    pedido.estado === 'Cancelado' ||
    pedido.estado === 'Anulado' ||
    pedido.estado === 'Devuelto' ||
    pedidoBloqueadoPorNc(pedido)
  ) {
    return false;
  }
  if (tab === 'TODOS') {
    return true;
  }
  return pedido.estado === tab;
}

export function etiquetaCantidadPedidos(cantidad: number): string {
  return cantidad === 1 ? '1 pedido' : `${cantidad} pedidos`;
}

export function mismoClienteCobro(
  pedidos: ReadonlyArray<Pick<PedidoDigital, 'clienteId' | 'clienteNombre'>>,
): boolean {
  if (pedidos.length <= 1) {
    return true;
  }
  const nombres = [
    ...new Set(
      pedidos
        .map((pedido) => pedido.clienteNombre.trim().toLowerCase())
        .filter((nombre) => nombre.length > 0),
    ),
  ];
  if (nombres.length === 1) {
    return true;
  }
  const ids = [
    ...new Set(pedidos.map((pedido) => pedido.clienteId).filter((id): id is string => !!id?.trim())),
  ];
  return ids.length === 1 && pedidos.every((pedido) => pedido.clienteId === ids[0]);
}

/** Estados en los que el CPE se considera emitido (no se debe reemitir). */
export function cpeEstadoEmitido(estado: string | null | undefined): boolean {
  return (
    estado === 'ACEPTADO' ||
    estado === 'PENDIENTE_CONSOLIDAR' ||
    estado === 'CONSOLIDADA' ||
    estado === 'SIMULADO'
  );
}

export function pedidoTieneCpeEmitido(
  pedido: Pick<PedidoDigital, 'comprobante'>,
): boolean {
  return cpeEstadoEmitido(pedido.comprobante?.estado);
}

export function etiquetaNumeroCpe(
  cpe: Pick<PedidoComprobanteResumen, 'serie' | 'correlativo'> | null | undefined,
): string {
  if (!cpe?.serie) {
    return '';
  }
  return `${cpe.serie}-${String(cpe.correlativo).padStart(8, '0')}`;
}

/** Puede emitir NC si hay boleta/factura aceptada y aún no hay NC final. */
export function pedidoPuedeNotaCredito(pedido: PedidoDigital): boolean {
  if (pedidoBloqueadoPorNc(pedido)) {
    return false;
  }
  const cpe = pedido.comprobante;
  if (!cpe || (cpe.tipo !== 'BOLETA' && cpe.tipo !== 'FACTURA')) {
    return false;
  }
  if (!cpeEstadoEmitido(cpe.estado)) {
    return false;
  }
  if (pedido.notaCredito && cpeEstadoEmitido(pedido.notaCredito.estado)) {
    return false;
  }
  return (
    pedido.estado === 'Pagado' ||
    pedido.estado === 'Empaquetado' ||
    pedido.estado === 'PendienteEntrega' ||
    pedido.estado === 'Entregado'
  );
}

/** Motivos Catálogo 09 SUNAT (subset operativo). */
export const MOTIVOS_NOTA_CREDITO_SUNAT: readonly {
  codigo: string;
  etiqueta: string;
}[] = [
  { codigo: '01', etiqueta: '01 — Anulación de la operación' },
  { codigo: '02', etiqueta: '02 — Anulación por error en el RUC' },
  { codigo: '03', etiqueta: '03 — Corrección por error en la descripción' },
  { codigo: '04', etiqueta: '04 — Descuento global' },
  { codigo: '05', etiqueta: '05 — Descuento por ítem' },
  { codigo: '06', etiqueta: '06 — Devolución total' },
  { codigo: '07', etiqueta: '07 — Devolución por ítem' },
];

export function descripcionMotivoNotaCredito(codigo: string): string {
  switch (codigo) {
    case '01':
      return 'Anulación de la operación';
    case '02':
      return 'Anulación por error en el RUC';
    case '03':
      return 'Corrección por error en la descripción';
    case '04':
      return 'Descuento global';
    case '05':
      return 'Descuento por ítem';
    case '06':
      return 'Devolución total';
    case '07':
      return 'Devolución por ítem';
    default:
      return 'Nota de crédito';
  }
}

export function entregaRecojo(destinatarioNombre: string): PedidoDigitalEntrega {
  return {
    destinatarioNombre: destinatarioNombre.trim() || 'Cliente',
    destinatarioTelefono: null,
    direccion: null,
    distrito: null,
    provincia: null,
    departamento: null,
    courier: 'Recojo en tienda',
    esRecojoTienda: true,
    puntoEntrega: null,
    canalContacto: null,
    contactoReferencia: null,
  };
}
