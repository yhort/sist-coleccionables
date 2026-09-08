import { CanalPedidoDigital } from '../../pedidos-digitales/models/pedido-digital.model';
import { TipoProductoTcg } from '../../productos-tcg/models/producto-tcg.model';

export type CanalSubastaTcg = 'FACEBOOK_SUBASTA' | 'WEB' | 'PRESENCIAL' | 'OTRO';

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
  codigoSku: string;
  tipoProducto: TipoProductoTcg;
  cantidad: number;
  orden: number;
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
  canal: CanalSubastaTcg | 'TODOS';
  tipoProducto: TipoProductoTcg | 'TODOS';
  sedeId: string | 'TODAS';
}

export interface SubastaDetalleInput {
  productoId: string;
  cantidad: number;
}

export interface CrearSubastaRequest {
  sedeId: string;
  /** Compat Single Hit; preferir `detalles`. */
  productoId?: string;
  detalles: SubastaDetalleInput[];
  titulo: string;
  canal: CanalSubastaTcg;
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
  monto: number;
}

export interface AdjudicarSubastaRequest {
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

export function etiquetaLoteSubasta(subasta: Pick<SubastaTcg, 'detalles' | 'productoNombre'>): string {
  const detalles = subasta.detalles ?? [];
  if (detalles.length === 0) {
    return subasta.productoNombre ?? 'Producto';
  }
  if (detalles.length === 1) {
    const linea = detalles[0];
    return linea.cantidad > 1
      ? `${linea.productoNombre} × ${linea.cantidad}`
      : linea.productoNombre;
  }
  const unidades = detalles.reduce((sum, d) => sum + d.cantidad, 0);
  return `Combo · ${detalles.length} SKUs · ${unidades} uds`;
}

export function unidadesLote(subasta: Pick<SubastaTcg, 'detalles'>): number {
  const detalles = subasta.detalles ?? [];
  if (detalles.length === 0) {
    return 1;
  }
  return detalles.reduce((sum, d) => sum + d.cantidad, 0);
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

export function ultimaPuja(subasta: SubastaTcg): PujaTcg | null {
  const ordenadas = pujasOrdenadas(subasta.pujas);
  return ordenadas.at(-1) ?? null;
}

export function pujaMaxima(subasta: SubastaTcg): PujaTcg | null {
  if (subasta.pujas.length === 0) {
    return null;
  }
  return subasta.pujas.reduce((mejor, actual) => (actual.monto > mejor.monto ? actual : mejor));
}

export function pujaGanadoraActual(subasta: SubastaTcg): PujaTcg | null {
  return subasta.pujas.find((p) => p.id === subasta.pujaGanadoraId) ?? pujaMaxima(subasta);
}

export function montoMinimoSiguiente(subasta: SubastaTcg): number {
  const ultima = ultimaPuja(subasta);
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
