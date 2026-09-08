import { PedidoDigital, PedidoDigitalEntrega } from '../../pedidos-digitales/models/pedido-digital.model';

export type MetodoEnvio = 'RECOJO_TIENDA' | 'OLVA_COURIER' | 'SHALOM' | 'DELIVERY_MOTO';

export type EstadoLogistica = 'PROGRAMADA' | 'DESPACHADA' | 'EN_TRANSITO' | 'ENTREGADA' | 'FALLIDA';

export interface Entrega {
  id: string;
  pedidoDigitalId: string;
  sedeOrigenId: string;
  metodoEnvio: MetodoEnvio;
  estado: EstadoLogistica;
  destinatarioNombre: string;
  destinatarioTelefono: string | null;
  direccion: string | null;
  distrito: string | null;
  provincia: string | null;
  departamento: string | null;
  agencia: string | null;
  puntoEntrega: string | null;
  canalContacto: import('../../../shared/models/contacto-entrega.model').CanalContactoCliente | null;
  contactoReferencia: string | null;
  numeroTracking: string | null;
  costoEnvio: number;
  notasEmpaque: string | null;
  fechaProgramada: string | null;
  fechaDespacho: string | null;
  fechaEntrega: string | null;
  observacion: string | null;
}

export interface EntregasFiltros {
  metodoEnvio: MetodoEnvio | 'TODOS';
  estado: EstadoLogistica | 'TODOS';
  sedeId: string | 'TODAS';
  busqueda: string;
}

export interface EmpaquetarEntregaRequest {
  notasEmpaque: string;
}

export interface DespacharEntregaRequest {
  metodoEnvio: MetodoEnvio;
  numeroTracking: string | null;
  agencia: string | null;
  costoEnvio: number;
  observacion?: string | null;
}

export interface RemitenteTrunqi {
  nombre: string;
  razonSocial: string;
  direccion: string;
  distrito: string;
  telefono: string;
}

export interface EntregaFila {
  pedido: PedidoDigital;
  entrega: Entrega;
  sedeNombre: string;
}

export const METODOS_ENVIO: readonly MetodoEnvio[] = [
  'RECOJO_TIENDA',
  'OLVA_COURIER',
  'SHALOM',
  'DELIVERY_MOTO',
];

export const ESTADOS_LOGISTICA: readonly EstadoLogistica[] = [
  'PROGRAMADA',
  'DESPACHADA',
  'EN_TRANSITO',
  'ENTREGADA',
  'FALLIDA',
];

export const FILTROS_ENTREGAS_VACIOS: EntregasFiltros = {
  metodoEnvio: 'TODOS',
  estado: 'TODOS',
  sedeId: 'TODAS',
  busqueda: '',
};

export const ETIQUETAS_METODO_ENVIO: Record<MetodoEnvio, string> = {
  RECOJO_TIENDA: 'RECOJO_TIENDA',
  OLVA_COURIER: 'OLVA_COURIER',
  SHALOM: 'SHALOM',
  DELIVERY_MOTO: 'DELIVERY_MOTO',
};

export const ETIQUETAS_ESTADO_LOGISTICA: Record<EstadoLogistica, string> = {
  PROGRAMADA: 'Programada',
  DESPACHADA: 'Despachada',
  EN_TRANSITO: 'En tránsito',
  ENTREGADA: 'Entregada',
  FALLIDA: 'Fallida',
};

const REMITENTE_MIRAFLORES: RemitenteTrunqi = {
  nombre: 'Trunqi TCG',
  razonSocial: 'TRUNQI TCG SAC',
  direccion: 'Av. José Larco 1230, interior 4',
  distrito: 'Miraflores, Lima',
  telefono: '999 220 110',
};

const REMITENTE_SURCO: RemitenteTrunqi = {
  nombre: 'Trunqi TCG · Almacén',
  razonSocial: 'TRUNQI TCG SAC',
  direccion: 'Av. El Polo 710, almacén 2',
  distrito: 'Santiago de Surco, Lima',
  telefono: '999 220 111',
};

export const REMITENTES_POR_SEDE: Record<string, RemitenteTrunqi> = {
  'sede-mira': REMITENTE_MIRAFLORES,
  'c0a1e001-0000-4000-8000-000000000010': REMITENTE_MIRAFLORES,
  'sede-surco': REMITENTE_SURCO,
};

export function metodoDesdeSnapshot(entrega: PedidoDigitalEntrega): MetodoEnvio {
  if (entrega.esRecojoTienda) {
    return 'RECOJO_TIENDA';
  }
  const courier = (entrega.courier ?? '').toLowerCase();
  if (courier.includes('shalom')) {
    return 'SHALOM';
  }
  if (courier.includes('olva')) {
    return 'OLVA_COURIER';
  }
  if (courier.includes('moto') || courier.includes('delivery')) {
    return 'DELIVERY_MOTO';
  }
  return 'OLVA_COURIER';
}

export function snapshotDesdeMetodo(
  metodo: MetodoEnvio,
  extras: Partial<PedidoDigitalEntrega> = {},
): Partial<PedidoDigitalEntrega> {
  const recojo = metodo === 'RECOJO_TIENDA';
  return {
    esRecojoTienda: recojo,
    courier: etiquetaCourier(metodo),
    ...extras,
  };
}

export function etiquetaCourier(metodo: MetodoEnvio): string {
  switch (metodo) {
    case 'RECOJO_TIENDA':
      return 'Recojo en tienda';
    case 'OLVA_COURIER':
      return 'Olva Courier';
    case 'SHALOM':
      return 'Shalom';
    case 'DELIVERY_MOTO':
      return 'Delivery moto';
  }
}

export function pareceUuid(valor: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    valor.trim(),
  );
}

export function nombreLegible(valor: string | null | undefined): string | null {
  const texto = valor?.trim() ?? '';
  if (!texto || pareceUuid(texto)) {
    return null;
  }
  return texto;
}

export function nombreSedeDe(
  pedido: PedidoDigital,
  sedes: readonly { id: string; nombre: string }[],
): string {
  const porCatalogo = sedes.find((sede) => sede.id === pedido.sedeId)?.nombre;
  return nombreLegible(pedido.sedeNombre) ?? nombreLegible(porCatalogo) ?? 'Sede';
}

export function destinoDe(entrega: Entrega, sedeNombre: string): string {
  const punto = nombreLegible(entrega.puntoEntrega);
  if (punto) {
    return punto;
  }
  if (entrega.metodoEnvio === 'RECOJO_TIENDA') {
    return nombreLegible(sedeNombre) ?? nombreLegible(entrega.agencia) ?? 'Tienda';
  }
  if (entrega.agencia) {
    return nombreLegible(entrega.agencia) ?? entrega.agencia;
  }
  const partes = [entrega.direccion, entrega.distrito].filter(Boolean);
  return partes.join(' · ') || '—';
}

export function remitenteDe(sedeId: string): RemitenteTrunqi {
  return REMITENTES_POR_SEDE[sedeId] ?? REMITENTES_POR_SEDE['sede-mira'];
}
