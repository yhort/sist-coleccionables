import { CanalPedidoDigital } from '../../pedidos-digitales/models/pedido-digital.model';
import { TipoProductoTcg } from '../../productos-tcg/models/producto-tcg.model';

export type CanalSubastaTcg = 'FACEBOOK_SUBASTA' | 'WEB' | 'PRESENCIAL' | 'OTRO';

export type ModoSubastaTcg = 'COMBO' | 'INDIVIDUALES';

export type EstadoSubastaDetalle = 'PENDIENTE' | 'ADJUDICADO' | 'DESIERTA';

export type EstadoSubastaTcg =
  | 'BORRADOR'
  | 'ACTIVA'
  | 'CERRADA'
  | 'ADJUDICADA'
  | 'CANCELADA';

export type MetodoEnvioCheckout = 'RECOJO_TIENDA' | 'OLVA_COURIER' | 'SHALOM';

export type OrigenPagoCheckout = 'YAPE' | 'PLIN' | 'TRANSFERENCIA';

export interface PujaTcg {
  id: string;
  subastaTcgId: string;
  subastaDetalleId: string | null;
  clienteId: string | null;
  nombrePostor: string;
  monto: number;
  fecha: string;
  esGanadora: boolean;
}

export interface SubastaDetalle {
  id: string;
  productoId: string;
  productoNombre: string;
  /** Nombre editable del ítem; si vacío se usa productoNombre. */
  tituloPersonalizado: string | null;
  codigoSku: string;
  tipoProducto: TipoProductoTcg;
  cantidad: number;
  orden: number;
  estado: EstadoSubastaDetalle;
  pujaGanadoraId: string | null;
  pedidoDigitalId: string | null;
  pedidoCodigo: string | null;
}

export interface SubastaTcg {
  id: string;
  codigo: string;
  sedeId: string;
  sedeNombre?: string;
  productoId: string;
  productoNombre?: string;
  codigoSku?: string;
  tipoProducto?: TipoProductoTcg;
  detalles: SubastaDetalle[];
  titulo: string;
  canal: CanalSubastaTcg;
  modo: ModoSubastaTcg;
  precioBase: number;
  incrementoMinimo: number;
  precioReserva: number | null;
  fechaInicio: string;
  fechaCierre: string;
  fechaCierreReal: string | null;
  estado: EstadoSubastaTcg;
  pujaGanadoraId: string | null;
  pedidoDigitalId: string | null;
  pedidoCodigo: string | null;
  observacion: string | null;
  pujas: PujaTcg[];
}

export interface SubastasTcgFiltros {
  busqueda: string;
  canal: CanalSubastaTcg | 'TODOS';
  tipoProducto: TipoProductoTcg | 'TODOS';
  sedeId: string | 'TODAS';
}

export interface SubastaDetalleInput {
  productoId: string;
  cantidad: number;
  tituloPersonalizado?: string | null;
}

export interface CrearSubastaRequest {
  sedeId: string;
  /** Compat Single Hit; preferir `detalles`. */
  productoId?: string;
  detalles: SubastaDetalleInput[];
  titulo: string;
  canal: CanalSubastaTcg;
  modo?: ModoSubastaTcg;
  precioBase: number;
  incrementoMinimo: number;
  precioReserva: number | null;
  fechaInicio: string;
  fechaCierre: string;
  observacion?: string | null;
}

export interface RegistrarPujaRequest {
  nombrePostor: string;
  clienteId?: string | null;
  subastaDetalleId?: string | null;
  monto: number;
}

export interface AdjudicarSubastaRequest {
  subastaDetalleId?: string | null;
  /** Adjudicación directa sin historial de pujas. */
  nombrePostor?: string | null;
  clienteId?: string | null;
  montoAdjudicado?: number | null;
  metodoEnvio: MetodoEnvioCheckout;
  origenPagoPreferido: OrigenPagoCheckout;
  destinatarioNombre?: string | null;
  destinatarioTelefono?: string | null;
  entregaDireccion?: string | null;
  entregaDistrito?: string | null;
  entregaProvincia?: string | null;
  entregaDepartamento?: string | null;
  agencia?: string | null;
  puntoEntrega?: string | null;
  canalContacto?: import('../../../shared/models/contacto-entrega.model').CanalContactoCliente | null;
  contactoReferencia?: string | null;
  guardarPuntoEnCliente?: boolean;
}

export interface MargenSubasta {
  precioBase: number;
  ofertaReferencia: number | null;
  diferencia: number | null;
  porcentaje: number | null;
}

export const FILTROS_SUBASTAS_VACIOS: SubastasTcgFiltros = {
  busqueda: '',
  canal: 'TODOS',
  tipoProducto: 'TODOS',
  sedeId: 'TODAS',
};

export const ESTADOS_TABLERO_SUBASTA: readonly EstadoSubastaTcg[] = [
  'BORRADOR',
  'ACTIVA',
  'CERRADA',
  'ADJUDICADA',
  'CANCELADA',
];

export const CANALES_SUBASTA: readonly CanalSubastaTcg[] = [
  'FACEBOOK_SUBASTA',
  'WEB',
  'PRESENCIAL',
  'OTRO',
];

export const METODOS_ENVIO_CHECKOUT: readonly MetodoEnvioCheckout[] = [
  'RECOJO_TIENDA',
  'OLVA_COURIER',
  'SHALOM',
];

export const ORIGENES_PAGO_CHECKOUT: readonly OrigenPagoCheckout[] = [
  'YAPE',
  'PLIN',
  'TRANSFERENCIA',
];

export const ETIQUETAS_ESTADO_SUBASTA: Record<EstadoSubastaTcg, string> = {
  BORRADOR: 'Borrador',
  ACTIVA: 'Activa',
  CERRADA: 'Cerrada',
  ADJUDICADA: 'Adjudicada',
  CANCELADA: 'Cancelada',
};

export const ETIQUETAS_MODO_SUBASTA: Record<ModoSubastaTcg, string> = {
  COMBO: 'Combo / Lote único',
  INDIVIDUALES: 'Evento · cartas individuales',
};

export const ETIQUETAS_ESTADO_DETALLE_SUBASTA: Record<EstadoSubastaDetalle, string> = {
  PENDIENTE: 'Pendiente',
  ADJUDICADO: 'Adjudicada',
  DESIERTA: 'Desierta',
};

export const ETIQUETAS_CANAL_SUBASTA: Record<CanalSubastaTcg, string> = {
  FACEBOOK_SUBASTA: 'Facebook',
  WEB: 'Web',
  PRESENCIAL: 'Evento presencial',
  OTRO: 'Otro',
};

export const ETIQUETAS_METODO_ENVIO_CHECKOUT: Record<MetodoEnvioCheckout, string> = {
  RECOJO_TIENDA: 'Recojo en tienda',
  OLVA_COURIER: 'Olva',
  SHALOM: 'Shalom',
};

export const ETIQUETAS_ORIGEN_PAGO_CHECKOUT: Record<OrigenPagoCheckout, string> = {
  YAPE: 'Yape',
  PLIN: 'Plin',
  TRANSFERENCIA: 'Transferencia',
};

export function nombreVisibleLinea(
  linea: Pick<SubastaDetalle, 'productoNombre' | 'tituloPersonalizado'>,
): string {
  const custom = linea.tituloPersonalizado?.trim();
  return custom || linea.productoNombre;
}

export function etiquetaLoteSubasta(
  subasta: Pick<SubastaTcg, 'detalles' | 'productoNombre' | 'modo'>,
): string {
  const detalles = subasta.detalles ?? [];
  if (detalles.length === 0) {
    return subasta.productoNombre ?? 'Producto';
  }
  if (detalles.length === 1) {
    const linea = detalles[0];
    const nombre = nombreVisibleLinea(linea);
    return linea.cantidad > 1 ? `${nombre} × ${linea.cantidad}` : nombre;
  }
  const unidades = detalles.reduce((sum, d) => sum + d.cantidad, 0);
  if (subasta.modo === 'INDIVIDUALES') {
    const pendientes = detalles.filter((d) => d.estado === 'PENDIENTE').length;
    return `Evento · ${detalles.length} cartas · ${pendientes} pendientes`;
  }
  return `Combo · ${detalles.length} SKUs · ${unidades} uds`;
}

export function unidadesLote(subasta: Pick<SubastaTcg, 'detalles'>): number {
  const detalles = subasta.detalles ?? [];
  if (detalles.length === 0) {
    return 1;
  }
  return detalles.reduce((sum, d) => sum + d.cantidad, 0);
}

export function esEventoIndividuales(subasta: Pick<SubastaTcg, 'modo'>): boolean {
  return subasta.modo === 'INDIVIDUALES';
}

export function subastaVencida(subasta: Pick<SubastaTcg, 'fechaCierre'>, ahora = Date.now()): boolean {
  const cierre = Date.parse(subasta.fechaCierre);
  return Number.isFinite(cierre) && cierre <= ahora;
}

export function cuentaRegresiva(fechaCierreIso: string, ahora = Date.now()): {
  totalMs: number;
  vencida: boolean;
  texto: string;
} {
  const cierre = Date.parse(fechaCierreIso);
  if (!Number.isFinite(cierre)) {
    return { totalMs: 0, vencida: true, texto: '00:00:00' };
  }
  const totalMs = Math.max(0, cierre - ahora);
  const vencida = totalMs <= 0;
  const totalSeg = Math.floor(totalMs / 1000);
  const horas = Math.floor(totalSeg / 3600);
  const minutos = Math.floor((totalSeg % 3600) / 60);
  const segundos = totalSeg % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return {
    totalMs,
    vencida,
    texto: `${pad(horas)}:${pad(minutos)}:${pad(segundos)}`,
  };
}

export function pujasOrdenadas(pujas: readonly PujaTcg[]): PujaTcg[] {
  return [...pujas].sort((a, b) => new Date(a.fecha).getTime() - new Date(b.fecha).getTime());
}

export function pujasDeDetalle(
  subasta: Pick<SubastaTcg, 'pujas'>,
  detalleId: string | null | undefined,
): PujaTcg[] {
  if (!detalleId) {
    return pujasOrdenadas(subasta.pujas);
  }
  return pujasOrdenadas(subasta.pujas.filter((p) => p.subastaDetalleId === detalleId));
}

export function ultimaPuja(subasta: SubastaTcg, detalleId?: string | null): PujaTcg | null {
  const ordenadas = detalleId
    ? pujasDeDetalle(subasta, detalleId)
    : pujasOrdenadas(subasta.pujas);
  return ordenadas.at(-1) ?? null;
}

export function pujaMaxima(subasta: SubastaTcg, detalleId?: string | null): PujaTcg | null {
  const pujas = detalleId ? pujasDeDetalle(subasta, detalleId) : subasta.pujas;
  if (pujas.length === 0) {
    return null;
  }
  return pujas.reduce((mejor, actual) => (actual.monto > mejor.monto ? actual : mejor));
}

export function pujaGanadoraActual(subasta: SubastaTcg, detalleId?: string | null): PujaTcg | null {
  if (detalleId && esEventoIndividuales(subasta)) {
    const linea = subasta.detalles.find((d) => d.id === detalleId);
    if (linea?.pujaGanadoraId) {
      return subasta.pujas.find((p) => p.id === linea.pujaGanadoraId) ?? null;
    }
    return pujaMaxima(subasta, detalleId);
  }
  return subasta.pujas.find((p) => p.id === subasta.pujaGanadoraId) ?? pujaMaxima(subasta);
}

export function montoMinimoSiguiente(subasta: SubastaTcg, detalleId?: string | null): number {
  const ultima = ultimaPuja(subasta, detalleId);
  if (!ultima) {
    return round2(subasta.precioBase);
  }
  return round2(ultima.monto + subasta.incrementoMinimo);
}

export function calcularMargenSubasta(
  precioBase: number,
  ofertaReferencia: number | null,
): MargenSubasta {
  if (ofertaReferencia === null) {
    return { precioBase, ofertaReferencia: null, diferencia: null, porcentaje: null };
  }
  const diferencia = round2(ofertaReferencia - precioBase);
  return {
    precioBase,
    ofertaReferencia,
    diferencia,
    porcentaje: precioBase > 0 ? round2((diferencia / precioBase) * 100) : null,
  };
}

export function canalPedidoDesdeSubasta(canal: CanalSubastaTcg): CanalPedidoDigital {
  switch (canal) {
    case 'FACEBOOK_SUBASTA':
      return 'FACEBOOK_SUBASTA';
    case 'WEB':
      return 'WEB';
    default:
      return 'OTRO';
  }
}

export function round2(valor: number): number {
  return Math.round(valor * 100) / 100;
}
