export interface VentasDiaKpi {
  totalRecaudado: number;
  comprobantesEmitidos: number;
  ventasRegistradas: number;
}

export interface SubastasKpi {
  activas: number;
  ganadoresPendientePago: number;
}

export interface PagosIpnKpi {
  pendientesAsociar: number;
  montoPendiente: number;
}

export interface StockBajoKpi {
  cantidad: number;
  umbral: number;
}

export type TipoActividadDashboard = 'PEDIDO' | 'VENTA';

export interface ActividadRecienteItem {
  id: string;
  tipo: TipoActividadDashboard | string;
  clienteNombre: string;
  monto: number;
  estado: string;
  metodoPago: string;
  fecha: string;
}

export interface DashboardResumen {
  fechaLocal: string;
  generadoEn: string;
  ventasHoy: VentasDiaKpi;
  subastas: SubastasKpi;
  pagosNotificados: PagosIpnKpi;
  stockBajo: StockBajoKpi;
  actividadReciente: ActividadRecienteItem[];
}

export function formatearFechaOperativa(isoDate: string): string {
  const [anio, mes, dia] = isoDate.split('-').map(Number);
  if (!anio || !mes || !dia) {
    return isoDate;
  }
  const fecha = new Date(anio, mes - 1, dia);
  const etiqueta = fecha.toLocaleDateString('es-PE', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
  return etiqueta.charAt(0).toUpperCase() + etiqueta.slice(1);
}

export function claseEstadoActividad(estado: string): string {
  const clave = estado.toLowerCase();
  if (clave.includes('entregado') || clave.includes('pagado') || clave.includes('venta')) {
    return 'ok';
  }
  if (clave.includes('cancelado')) {
    return 'danger';
  }
  if (clave.includes('pendiente')) {
    return 'warn';
  }
  return 'info';
}
