import {
  ETIQUETAS_ORIGEN_PEDIDO,
  ORIGENES_PEDIDO,
  round2,
} from '../../pedidos-digitales/models/pedido-digital.model';
import { ETIQUETAS_JUEGO, JuegoTcg } from '../../productos-tcg/models/producto-tcg.model';
import { ETIQUETAS_ORIGEN_PAGO, OrigenPago } from '../../pagos/models/pago.model';

export type FranquiciaReporte = JuegoTcg | 'OTROS';

export type EstadoComprobanteReporte = 'EMITIDO_SUNAT' | 'PENDIENTE';

export interface ReportesPeriodo {
  desde: string;
  hasta: string;
}

export interface ReportesKpis {
  ventasTotales: number;
  pedidosCobrados: number;
  margenBruto: number;
  margenPorcentaje: number | null;
  inventarioSellado: number;
  inventarioCartas: number;
  inventarioActivo: number;
  unidadesSellado: number;
  unidadesCartas: number;
  totalConciliado: number;
  pagosConciliados: number;
}

export interface SerieReporte {
  clave: string;
  etiqueta: string;
  monto: number;
  cantidad: number;
  porcentaje: number;
}

export interface FilaYieldApertura {
  id: string;
  fecha: string;
  sedeNombre: string;
  selladoNombre: string;
  cantidadSellados: number;
  cartasObtenidas: number;
  cartasIngresadasKardex: number;
  costoCompra: number;
  valorComercial: number;
  diferencia: number;
  yieldPorcentaje: number | null;
}

export interface ResumenYieldAperturas {
  filas: FilaYieldApertura[];
  costoCompra: number;
  valorComercial: number;
  diferencia: number;
  yieldPorcentaje: number | null;
}

export interface FilaArqueoMetodo {
  origen: OrigenPago;
  etiqueta: string;
  monto: number;
  cantidad: number;
  emitidoSunat: number;
  pendiente: number;
}

export interface FilaArqueoComprobante {
  estado: EstadoComprobanteReporte;
  etiqueta: string;
  monto: number;
  cantidad: number;
}

export interface ReporteArqueo {
  porMetodo: FilaArqueoMetodo[];
  porComprobante: FilaArqueoComprobante[];
  totalConfirmado: number;
}

export interface ReporteConsolidado {
  periodo: ReportesPeriodo;
  kpis: ReportesKpis;
  ventasPorCanal: SerieReporte[];
  ventasPorFranquicia: SerieReporte[];
  yieldAperturas: ResumenYieldAperturas;
  arqueo: ReporteArqueo;
}

export const ORIGENES_CANAL_REPORTE = ORIGENES_PEDIDO;

export const FRANQUICIAS_REPORTE: readonly FranquiciaReporte[] = [
  'POKEMON',
  'MAGIC',
  'YUGIOH',
  'OTROS',
];

export const METODOS_ARQUEO: readonly OrigenPago[] = ['YAPE', 'IZIPAY', 'TRANSFERENCIA', 'EFECTIVO'];

export const ETIQUETAS_CANAL_REPORTE = ETIQUETAS_ORIGEN_PEDIDO;

export const ETIQUETAS_FRANQUICIA: Record<FranquiciaReporte, string> = {
  ...ETIQUETAS_JUEGO,
  OTROS: 'Otros / accesorios',
};

export const ETIQUETAS_METODO_ARQUEO = ETIQUETAS_ORIGEN_PAGO;

export const ETIQUETAS_COMPROBANTE: Record<EstadoComprobanteReporte, string> = {
  EMITIDO_SUNAT: 'Emitido SUNAT',
  PENDIENTE: 'Pendiente',
};

export function periodoMesActual(referencia = new Date()): ReportesPeriodo {
  const year = referencia.getFullYear();
  const month = String(referencia.getMonth() + 1).padStart(2, '0');
  const day = String(referencia.getDate()).padStart(2, '0');
  return {
    desde: `${year}-${month}-01`,
    hasta: `${year}-${month}-${day}`,
  };
}

export function franquiciaDeJuego(juego: JuegoTcg | ''): FranquiciaReporte {
  return juego === 'POKEMON' || juego === 'MAGIC' || juego === 'YUGIOH' ? juego : 'OTROS';
}

export { round2 };
