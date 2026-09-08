export type EstadoCajaSesion = 'ABIERTA' | 'CERRADA';
export type TipoCajaMovimiento = 'INGRESO' | 'EGRESO';
export type TipoDiferenciaCaja = 'CUADRADO' | 'SOBRANTE' | 'FALTANTE';
export type TipoComprobanteCaja = 'BOLETA' | 'FACTURA' | 'NOTA_VENTA';

export interface CajaMovimiento {
  id: string;
  cajaSesionId: string;
  tipo: TipoCajaMovimiento;
  monto: number;
  concepto: string;
  usuarioId: string | null;
  usuarioNombre: string | null;
  fecha: string;
}

export interface CajaMedioPagoFila {
  grupo: string;
  etiqueta: string;
  monto: number;
  cantidad: number;
}

export interface CajaDocumentoFila {
  tipo: TipoComprobanteCaja;
  etiqueta: string;
  cantidad: number;
  total: number;
}

export interface CajaVentaFila {
  ventaId: string;
  fecha: string;
  total: number;
  clienteNombre: string | null;
  mediosPago: string;
  comprobante: string | null;
}

export interface CajaEfectivoResumen {
  montoApertura: number;
  ventasEfectivo: number;
  ingresos: number;
  egresos: number;
  montoEfectivoTeorico: number;
  montoEfectivoReal: number | null;
  diferencia: number | null;
  tipoDiferencia: TipoDiferenciaCaja | null;
}

export interface CajaResumen {
  cajaSesionId: string;
  sedeId: string;
  sedeNombre: string;
  estado: EstadoCajaSesion;
  fechaApertura: string;
  fechaCierre: string | null;
  usuarioAperturaNombre: string;
  usuarioCierreNombre: string | null;
  cantidadVentas: number;
  totalVentas: number;
  efectivo: CajaEfectivoResumen;
  mediosPago: CajaMedioPagoFila[];
  documentos: CajaDocumentoFila[];
  movimientos: CajaMovimiento[];
  ventas: CajaVentaFila[];
}

export interface CajaSesion {
  id: string;
  sedeId: string;
  sedeNombre: string;
  usuarioId: string;
  usuarioNombre: string;
  usuarioCierreId: string | null;
  usuarioCierreNombre: string | null;
  montoApertura: number;
  fechaApertura: string;
  fechaCierre: string | null;
  estado: EstadoCajaSesion;
  montoEfectivoTeorico: number;
  montoEfectivoReal: number | null;
  diferencia: number | null;
  tipoDiferencia: TipoDiferenciaCaja | null;
  observacionApertura: string | null;
  observacionCierre: string | null;
  cantidadVentas: number;
  totalVentas: number;
}

export interface CajaEstadoActual {
  abierta: boolean;
  sedeId: string;
  sedeNombre: string;
  mensaje: string | null;
  sesion: CajaSesion | null;
  resumen: CajaResumen | null;
}

export interface CajaTicketLinea {
  etiqueta: string;
  valor: string;
}

export interface CajaTicketEmpresa {
  ruc: string;
  razonSocial: string;
  nombreComercial: string;
}

export interface CajaTicketReporte {
  cajaSesionId: string;
  titulo: string;
  anchoMm: string;
  empresa: CajaTicketEmpresa;
  sedeNombre: string;
  usuarioAperturaNombre: string;
  usuarioCierreNombre: string | null;
  fechaAperturaLocal: string;
  fechaCierreLocal: string | null;
  estado: EstadoCajaSesion;
  encabezado: CajaTicketLinea[];
  mediosPago: CajaMedioPagoFila[];
  documentos: CajaDocumentoFila[];
  movimientos: CajaMovimiento[];
  efectivo: CajaEfectivoResumen;
  leyendaDiferencia: string;
  pie: string[];
}

export interface AbrirCajaRequest {
  sedeId: string;
  montoApertura: number;
  observacion?: string | null;
}

export interface CerrarCajaRequest {
  sedeId?: string | null;
  cajaSesionId?: string | null;
  montoEfectivoReal: number;
  observacion?: string | null;
}

export interface RegistrarCajaMovimientoRequest {
  sedeId?: string | null;
  cajaSesionId?: string | null;
  tipo: TipoCajaMovimiento;
  monto: number;
  concepto: string;
}

export const ETIQUETAS_ESTADO_CAJA: Record<EstadoCajaSesion, string> = {
  ABIERTA: 'Abierta',
  CERRADA: 'Cerrada',
};

export const ETIQUETAS_MOVIMIENTO_CAJA: Record<TipoCajaMovimiento, string> = {
  INGRESO: 'Ingreso',
  EGRESO: 'Egreso',
};

export const ETIQUETAS_DIFERENCIA_CAJA: Record<TipoDiferenciaCaja, string> = {
  CUADRADO: 'Cuadrada',
  SOBRANTE: 'Sobrante',
  FALTANTE: 'Faltante',
};

export function tipoDiferenciaDe(diferencia: number | null | undefined): TipoDiferenciaCaja | null {
  if (diferencia === null || diferencia === undefined || Number.isNaN(diferencia)) {
    return null;
  }
  if (diferencia > 0) {
    return 'SOBRANTE';
  }
  if (diferencia < 0) {
    return 'FALTANTE';
  }
  return 'CUADRADO';
}

export function round2(valor: number): number {
  return Math.round((valor + Number.EPSILON) * 100) / 100;
}
