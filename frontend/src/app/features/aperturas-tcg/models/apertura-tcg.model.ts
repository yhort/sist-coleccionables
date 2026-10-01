import {
  CondicionTcg,
  ProductoTcg,
  precioVigente,
} from '../../productos-tcg/models/producto-tcg.model';

export type EstadoAperturaTcg = 'BORRADOR' | 'CONFIRMADA' | 'ANULADA';

/** Grading simplificado del wizard de apertura (NM / Excellent / Good). */
export type EstadoCartaObtenida = 'NM' | 'EX' | 'GD';

export interface AperturaTcgDetalle {
  id: string;
  productoCartaId: string;
  cantidad: number;
  costoUnitarioAsignado: number | null;
  estado: EstadoCartaObtenida;
  esFoil: boolean;
}

export interface AperturaTcg {
  id: string;
  sedeId: string;
  sedeNombre?: string;
  productoSelladoId: string;
  productoSelladoNombre?: string;
  cantidadSellados: number;
  estado: EstadoAperturaTcg;
  usuarioNombre: string;
  observacion: string | null;
  fechaCreacion: string;
  fechaConfirmacion: string | null;
  detalles: AperturaTcgDetalle[];
  rendimiento?: RendimientoApertura;
}

export interface AperturaTcgFiltros {
  sedeId: string | 'TODAS';
  estado: EstadoAperturaTcg | 'TODOS';
  desde: string;
  hasta: string;
}

export interface CrearAperturaRequest {
  sedeId: string;
  productoSelladoId: string;
  cantidadSellados: number;
  observacion?: string | null;
}

export interface AperturaDetalleInput {
  id?: string;
  productoCartaId: string;
  cantidad: number;
  estado: EstadoCartaObtenida;
  esFoil: boolean;
}

export interface RendimientoApertura {
  costoSellado: number;
  valorEstimadoCartas: number;
  diferencia: number;
  yieldPorcentaje: number | null;
}

export const FILTROS_APERTURAS_VACIOS: AperturaTcgFiltros = {
  sedeId: 'TODAS',
  estado: 'TODOS',
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

/** Filtros iniciales del historial: operación del día en curso (UTC-5 Perú vía zona del navegador). */
export function crearFiltrosAperturasDiaActual(): AperturaTcgFiltros {
  const hoy = fechaLocalHoy();
  return {
    ...FILTROS_APERTURAS_VACIOS,
    desde: hoy,
    hasta: hoy,
  };
}

export const ESTADOS_CARTA_OBTENIDA: readonly EstadoCartaObtenida[] = ['NM', 'EX', 'GD'];

export const ETIQUETAS_ESTADO_APERTURA: Record<EstadoAperturaTcg, string> = {
  BORRADOR: 'Borrador',
  CONFIRMADA: 'Confirmada',
  ANULADA: 'Anulada',
};

export const ETIQUETAS_ESTADO_CARTA: Record<EstadoCartaObtenida, string> = {
  NM: 'Near Mint',
  EX: 'Excellent',
  GD: 'Good',
};

export function totalCartasObtenidas(apertura: AperturaTcg): number {
  return apertura.detalles.reduce((sum, detalle) => sum + detalle.cantidad, 0);
}

export function estadoAperturaDesdeCondicion(
  condicion: CondicionTcg | '',
): EstadoCartaObtenida {
  switch (condicion) {
    case 'LP':
      return 'EX';
    case 'MP':
    case 'HP':
    case 'DMG':
      return 'GD';
    default:
      return 'NM';
  }
}

export function condicionCatalogoDesdeEstado(estado: EstadoCartaObtenida): CondicionTcg {
  switch (estado) {
    case 'EX':
      return 'LP';
    case 'GD':
      return 'MP';
    default:
      return 'NM';
  }
}

export function normalizarCodigoCarta(valor: string): string {
  return valor.trim().toUpperCase().replace(/\s+/g, '');
}

export function coincideNumeroCarta(catalogo: string, query: string): boolean {
  const a = normalizarCodigoCarta(catalogo);
  const b = normalizarCodigoCarta(query);
  if (!b) {
    return true;
  }
  if (a === b || a.startsWith(b) || b.startsWith(a)) {
    return true;
  }
  return a.split('/')[0] === b.split('/')[0];
}

export function buscarCartasPorSetNumero(
  cartas: readonly ProductoTcg[],
  setCodigo: string,
  numeroCarta: string,
): ProductoTcg[] {
  const set = normalizarCodigoCarta(setCodigo);
  const numero = normalizarCodigoCarta(numeroCarta);
  if (!set && !numero) {
    return [];
  }

  return cartas.filter((producto) => {
    if (producto.tipoProducto !== 'CARTA' || !producto.activo) {
      return false;
    }
    const setOk = !set || normalizarCodigoCarta(producto.atributosTcg.setCodigo) === set;
    const numeroOk = !numero || coincideNumeroCarta(producto.atributosTcg.numeroCarta, numero);
    return setOk && numeroOk;
  });
}

export function resolverCartaCatalogo(
  cartas: readonly ProductoTcg[],
  referencia: Pick<ProductoTcg, 'atributosTcg'> | ProductoTcg,
  estado: EstadoCartaObtenida,
  esFoil: boolean,
): ProductoTcg | undefined {
  const set = referencia.atributosTcg.setCodigo;
  const numero = referencia.atributosTcg.numeroCarta;
  const candidatas = buscarCartasPorSetNumero(cartas, set, numero);
  if (candidatas.length === 0) {
    return undefined;
  }

  const condicion = condicionCatalogoDesdeEstado(estado);
  const exacta = candidatas.find(
    (carta) => carta.atributosTcg.esFoil === esFoil && carta.atributosTcg.condicion === condicion,
  );
  if (exacta) {
    return exacta;
  }

  const porFoil = candidatas.find((carta) => carta.atributosTcg.esFoil === esFoil);
  return porFoil ?? candidatas[0];
}

export function costoSelladoDe(producto: ProductoTcg | undefined, cantidad: number): number {
  if (!producto) {
    return 0;
  }
  const unitario = producto.costo ?? producto.precioVenta;
  return unitario * cantidad;
}

export function valorEstimadoCartas(
  detalles: readonly { productoCartaId: string; cantidad: number }[],
  cartasPorId: (id: string) => ProductoTcg | undefined,
): number {
  return detalles.reduce((sum, detalle) => {
    const carta = cartasPorId(detalle.productoCartaId);
    if (!carta) {
      return sum;
    }
    return sum + precioVigente(carta) * detalle.cantidad;
  }, 0);
}

export function calcularRendimiento(
  costoSellado: number,
  valorCartas: number,
): RendimientoApertura {
  const diferencia = valorCartas - costoSellado;
  return {
    costoSellado,
    valorEstimadoCartas: valorCartas,
    diferencia,
    yieldPorcentaje: costoSellado > 0 ? (diferencia / costoSellado) * 100 : null,
  };
}

export function prorratearCostoSellado(
  detalles: readonly AperturaDetalleInput[],
  costoSellado: number,
  cartasPorId: (id: string) => ProductoTcg | undefined,
): number[] {
  const valores = detalles.map((detalle) => {
    const carta = cartasPorId(detalle.productoCartaId);
    return carta ? precioVigente(carta) * detalle.cantidad : 0;
  });
  const totalValor = valores.reduce((sum, valor) => sum + valor, 0);
  const totalUnidades = detalles.reduce((sum, detalle) => sum + detalle.cantidad, 0);

  return detalles.map((detalle, index) => {
    if (detalle.cantidad <= 0) {
      return 0;
    }
    if (totalValor > 0) {
      return (valores[index] / totalValor) * costoSellado / detalle.cantidad;
    }
    if (totalUnidades > 0) {
      return costoSellado / totalUnidades;
    }
    return 0;
  });
}
