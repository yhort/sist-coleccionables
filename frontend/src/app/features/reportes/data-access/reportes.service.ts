import { Injectable, inject } from '@angular/core';

import { AperturasTcgApiService } from '../../aperturas-tcg/data-access/aperturas-tcg.service';
import {
  calcularRendimiento,
  costoSelladoDe,
  totalCartasObtenidas,
  valorEstimadoCartas,
} from '../../aperturas-tcg/models/apertura-tcg.model';
import { InventarioStore } from '../../inventario/data-access/inventario-store.service';
import { SEDES_INVENTARIO } from '../../inventario/models/inventario.model';
import { PagosApiService } from '../../pagos/data-access/pagos.service';
import { Pago, esOrigenDigital } from '../../pagos/models/pago.model';
import { PedidosDigitalesApiService } from '../../pedidos-digitales/data-access/pedidos-digitales.service';
import {
  EstadoPedidoDigital,
  FILTROS_PEDIDOS_VACIOS,
  PedidoDigital,
  origenDeCanal,
} from '../../pedidos-digitales/models/pedido-digital.model';
import { ProductosTcgApiService } from '../../productos-tcg/data-access/productos-tcg.service';
import { precioVigente } from '../../productos-tcg/models/producto-tcg.model';
import {
  ETIQUETAS_CANAL_REPORTE,
  ETIQUETAS_COMPROBANTE,
  ETIQUETAS_FRANQUICIA,
  ETIQUETAS_METODO_ARQUEO,
  EstadoComprobanteReporte,
  FRANQUICIAS_REPORTE,
  FilaArqueoComprobante,
  FilaArqueoMetodo,
  FilaYieldApertura,
  METODOS_ARQUEO,
  ORIGENES_CANAL_REPORTE,
  ReporteConsolidado,
  ReportesPeriodo,
  SerieReporte,
  franquiciaDeJuego,
  round2,
} from '../models/reporte.model';

const ESTADOS_COBRADOS: readonly EstadoPedidoDigital[] = [
  'Pagado',
  'Empaquetado',
  'PendienteEntrega',
  'Entregado',
];

@Injectable({ providedIn: 'root' })
export class ReportesApiService {
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly pagosApi = inject(PagosApiService);
  private readonly aperturasApi = inject(AperturasTcgApiService);
  private readonly inventario = inject(InventarioStore);

  construir(periodo: ReportesPeriodo): ReporteConsolidado {
    const pedidos = this.pedidosCobrados(periodo);
    const pagos = this.pagosConfirmados(periodo);
    const { ventasPorCanal, ventasPorFranquicia, ventasTotales, margenBruto } =
      this.agregarVentas(pedidos);
    const inventario = this.valorarInventario();
    const conciliados = pagos.filter((pago) => esOrigenDigital(pago.origen));

    return {
      periodo: { ...periodo },
      kpis: {
        ventasTotales,
        pedidosCobrados: pedidos.length,
        margenBruto,
        margenPorcentaje: ventasTotales > 0 ? round2((margenBruto / ventasTotales) * 100) : null,
        inventarioSellado: inventario.sellado,
        inventarioCartas: inventario.cartas,
        inventarioActivo: round2(inventario.sellado + inventario.cartas),
        unidadesSellado: inventario.unidadesSellado,
        unidadesCartas: inventario.unidadesCartas,
        totalConciliado: round2(conciliados.reduce((sum, pago) => sum + pago.monto, 0)),
        pagosConciliados: conciliados.length,
      },
      ventasPorCanal,
      ventasPorFranquicia,
      yieldAperturas: this.reporteYield(periodo),
      arqueo: this.reporteArqueo(pagos),
    };
  }

  exportarCsv(reporte: ReporteConsolidado): void {
    const lineas: string[][] = [
      ['Reporte TCG', etiquetaPeriodo(reporte.periodo)],
      [],
      ['KPIs'],
      ['Métrica', 'Valor'],
      ['Ventas totales del periodo', dinero(reporte.kpis.ventasTotales)],
      ['Pedidos cobrados', String(reporte.kpis.pedidosCobrados)],
      ['Margen bruto estimado', dinero(reporte.kpis.margenBruto)],
      [
        'Margen %',
        reporte.kpis.margenPorcentaje === null ? '' : `${reporte.kpis.margenPorcentaje.toFixed(1)}%`,
      ],
      ['Inventario sellado', dinero(reporte.kpis.inventarioSellado)],
      ['Inventario cartas sueltas', dinero(reporte.kpis.inventarioCartas)],
      ['Inventario activo', dinero(reporte.kpis.inventarioActivo)],
      ['Total conciliado Yape/Izipay', dinero(reporte.kpis.totalConciliado)],
      [],
      ['Ventas por canal'],
      ['Canal', 'Pedidos', 'Ingresos', '%'],
      ...reporte.ventasPorCanal.map((serie) => [
        serie.clave,
        String(serie.cantidad),
        dinero(serie.monto),
        `${serie.porcentaje.toFixed(1)}%`,
      ]),
      [],
      ['Ventas por franquicia'],
      ['Franquicia', 'Ítems', 'Ingresos', '%'],
      ...reporte.ventasPorFranquicia.map((serie) => [
        serie.etiqueta,
        String(serie.cantidad),
        dinero(serie.monto),
        `${serie.porcentaje.toFixed(1)}%`,
      ]),
      [],
      ['Rendimiento de aperturas (Yield TCG)'],
      [
        'Apertura',
        'Fecha',
        'Sede',
        'Sellado',
        'Cantidad',
        'Cartas obtenidas',
        'Ingresadas Kardex',
        'Costo compra',
        'Valor comercial',
        'Diferencia',
        'Yield %',
      ],
      ...reporte.yieldAperturas.filas.map((fila) => [
        fila.id,
        fila.fecha.slice(0, 10),
        fila.sedeNombre,
        fila.selladoNombre,
        String(fila.cantidadSellados),
        String(fila.cartasObtenidas),
        String(fila.cartasIngresadasKardex),
        dinero(fila.costoCompra),
        dinero(fila.valorComercial),
        dinero(fila.diferencia),
        fila.yieldPorcentaje === null ? '' : `${fila.yieldPorcentaje.toFixed(1)}%`,
      ]),
      [],
      ['Arqueo por método de pago'],
      ['Método', 'Pagos', 'Monto', 'Emitido SUNAT', 'Pendiente'],
      ...reporte.arqueo.porMetodo.map((fila) => [
        fila.etiqueta,
        String(fila.cantidad),
        dinero(fila.monto),
        dinero(fila.emitidoSunat),
        dinero(fila.pendiente),
      ]),
      [],
      ['Estado de comprobante'],
      ['Estado', 'Pagos', 'Monto'],
      ...reporte.arqueo.porComprobante.map((fila) => [
        fila.etiqueta,
        String(fila.cantidad),
        dinero(fila.monto),
      ]),
    ];

    const csv = `\uFEFF${lineas.map((fila) => fila.map(escaparCsv).join(',')).join('\n')}`;
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = `reportes-tcg-${reporte.periodo.desde || 'inicio'}-${reporte.periodo.hasta || 'hoy'}.csv`;
    enlace.click();
    URL.revokeObjectURL(url);
  }

  private pedidosCobrados(periodo: ReportesPeriodo): PedidoDigital[] {
    this.pedidosApi.pedidos();
    return this.pedidosApi
      .listar({ ...FILTROS_PEDIDOS_VACIOS, desde: periodo.desde, hasta: periodo.hasta })
      .filter((pedido) => ESTADOS_COBRADOS.includes(pedido.estado));
  }

  private pagosConfirmados(periodo: ReportesPeriodo): Pago[] {
    const pagos = this.pagosApi.pagos();
    const desde = periodo.desde ? startOfDay(periodo.desde) : null;
    const hasta = periodo.hasta ? endOfDay(periodo.hasta) : null;

    return pagos.filter((pago) => {
      if (pago.estado !== 'CONFIRMADO') {
        return false;
      }
      const fecha = new Date(pago.fechaConfirmacion ?? pago.fechaNotificacion).getTime();
      if (desde && fecha < desde) {
        return false;
      }
      if (hasta && fecha > hasta) {
        return false;
      }
      return true;
    });
  }

  private agregarVentas(pedidos: PedidoDigital[]): {
    ventasPorCanal: SerieReporte[];
    ventasPorFranquicia: SerieReporte[];
    ventasTotales: number;
    margenBruto: number;
  } {
    const canalMonto = Object.fromEntries(ORIGENES_CANAL_REPORTE.map((origen) => [origen, 0])) as Record<
      string,
      number
    >;
    const canalCantidad = Object.fromEntries(
      ORIGENES_CANAL_REPORTE.map((origen) => [origen, 0]),
    ) as Record<string, number>;
    const franquiciaMonto = Object.fromEntries(FRANQUICIAS_REPORTE.map((juego) => [juego, 0])) as Record<
      string,
      number
    >;
    const franquiciaCantidad = Object.fromEntries(
      FRANQUICIAS_REPORTE.map((juego) => [juego, 0]),
    ) as Record<string, number>;

    let ventasTotales = 0;
    let costoEstimado = 0;

    for (const pedido of pedidos) {
      const origen = origenDeCanal(pedido.canalPedido);
      ventasTotales = round2(ventasTotales + pedido.total);
      canalMonto[origen] = round2(canalMonto[origen] + pedido.total);
      canalCantidad[origen] += 1;

      for (const detalle of pedido.detalles) {
        const producto = this.productosApi.obtenerPorId(detalle.productoId);
        const franquicia = franquiciaDeJuego(producto?.juego ?? '');
        franquiciaMonto[franquicia] = round2(franquiciaMonto[franquicia] + detalle.total);
        franquiciaCantidad[franquicia] += detalle.cantidad;
        costoEstimado = round2(costoEstimado + (producto?.costo ?? 0) * detalle.cantidad);
      }
    }

    return {
      ventasTotales,
      margenBruto: round2(ventasTotales - costoEstimado),
      ventasPorCanal: aSeries(
        ORIGENES_CANAL_REPORTE.map((clave) => ({
          clave,
          etiqueta: ETIQUETAS_CANAL_REPORTE[clave],
          monto: canalMonto[clave],
          cantidad: canalCantidad[clave],
        })),
        ventasTotales,
      ),
      ventasPorFranquicia: aSeries(
        FRANQUICIAS_REPORTE.map((clave) => ({
          clave,
          etiqueta: ETIQUETAS_FRANQUICIA[clave],
          monto: franquiciaMonto[clave],
          cantidad: franquiciaCantidad[clave],
        })),
        ventasTotales,
      ),
    };
  }

  private valorarInventario(): {
    sellado: number;
    cartas: number;
    unidadesSellado: number;
    unidadesCartas: number;
  } {
    this.productosApi.productos();
    const stocks = this.inventario.stocks();
    let sellado = 0;
    let cartas = 0;
    let unidadesSellado = 0;
    let unidadesCartas = 0;

    for (const stock of stocks) {
      if (stock.cantidadDisponible <= 0) {
        continue;
      }
      const producto = this.productosApi.obtenerPorId(stock.productoId);
      if (!producto?.activo) {
        continue;
      }
      const valor = round2(precioVigente(producto) * stock.cantidadDisponible);
      if (producto.tipoProducto === 'SELLADO') {
        sellado = round2(sellado + valor);
        unidadesSellado += stock.cantidadDisponible;
      }
      if (producto.tipoProducto === 'CARTA') {
        cartas = round2(cartas + valor);
        unidadesCartas += stock.cantidadDisponible;
      }
    }

    return { sellado, cartas, unidadesSellado, unidadesCartas };
  }

  private reporteYield(periodo: ReportesPeriodo): ReporteConsolidado['yieldAperturas'] {
    const aperturas = this.aperturasApi.listar({
      sedeId: 'TODAS',
      estado: 'CONFIRMADA',
      desde: periodo.desde,
      hasta: periodo.hasta,
    });
    const movimientos = this.inventario.movimientos();

    const filas: FilaYieldApertura[] = aperturas.map((apertura) => {
      const sellado = this.productosApi.obtenerPorId(apertura.productoSelladoId);
      const rendimiento = calcularRendimiento(
        costoSelladoDe(sellado, apertura.cantidadSellados),
        valorEstimadoCartas(apertura.detalles, (id) => this.productosApi.obtenerPorId(id)),
      );
      const sede = SEDES_INVENTARIO.find((item) => item.id === apertura.sedeId);
      const cartasKardex = movimientos
        .filter(
          (mov) =>
            mov.tipoMovimiento === 'APERTURA_INGRESO_CARTA' && mov.referenciaId === apertura.id,
        )
        .reduce((sum, mov) => sum + mov.cantidad, 0);

      return {
        id: apertura.id,
        fecha: apertura.fechaConfirmacion ?? apertura.fechaCreacion,
        sedeNombre: sede?.nombre ?? apertura.sedeId,
        selladoNombre: sellado?.nombre ?? 'Sellado',
        cantidadSellados: apertura.cantidadSellados,
        cartasObtenidas: totalCartasObtenidas(apertura),
        cartasIngresadasKardex: cartasKardex,
        costoCompra: round2(rendimiento.costoSellado),
        valorComercial: round2(rendimiento.valorEstimadoCartas),
        diferencia: round2(rendimiento.diferencia),
        yieldPorcentaje:
          rendimiento.yieldPorcentaje === null ? null : round2(rendimiento.yieldPorcentaje),
      };
    });

    const costoCompra = round2(filas.reduce((sum, fila) => sum + fila.costoCompra, 0));
    const valorComercial = round2(filas.reduce((sum, fila) => sum + fila.valorComercial, 0));
    const diferencia = round2(valorComercial - costoCompra);

    return {
      filas,
      costoCompra,
      valorComercial,
      diferencia,
      yieldPorcentaje: costoCompra > 0 ? round2((diferencia / costoCompra) * 100) : null,
    };
  }

  private reporteArqueo(pagos: Pago[]): ReporteConsolidado['arqueo'] {
    const porMetodo: FilaArqueoMetodo[] = METODOS_ARQUEO.map((origen) => ({
      origen,
      etiqueta: ETIQUETAS_METODO_ARQUEO[origen],
      monto: 0,
      cantidad: 0,
      emitidoSunat: 0,
      pendiente: 0,
    }));
    const porEstado: Record<EstadoComprobanteReporte, FilaArqueoComprobante> = {
      EMITIDO_SUNAT: {
        estado: 'EMITIDO_SUNAT',
        etiqueta: ETIQUETAS_COMPROBANTE.EMITIDO_SUNAT,
        monto: 0,
        cantidad: 0,
      },
      PENDIENTE: {
        estado: 'PENDIENTE',
        etiqueta: ETIQUETAS_COMPROBANTE.PENDIENTE,
        monto: 0,
        cantidad: 0,
      },
    };

    for (const pago of pagos) {
      const estado = this.estadoComprobanteDe(pago);
      porEstado[estado].monto = round2(porEstado[estado].monto + pago.monto);
      porEstado[estado].cantidad += 1;

      const fila = porMetodo.find((item) => item.origen === pago.origen);
      if (!fila) {
        continue;
      }
      fila.monto = round2(fila.monto + pago.monto);
      fila.cantidad += 1;
      if (estado === 'EMITIDO_SUNAT') {
        fila.emitidoSunat = round2(fila.emitidoSunat + pago.monto);
      } else {
        fila.pendiente = round2(fila.pendiente + pago.monto);
      }
    }

    return {
      porMetodo,
      porComprobante: [porEstado.EMITIDO_SUNAT, porEstado.PENDIENTE],
      totalConfirmado: round2(pagos.reduce((sum, pago) => sum + pago.monto, 0)),
    };
  }

  private estadoComprobanteDe(pago: Pago): EstadoComprobanteReporte {
    if (!pago.pedidoDigitalId) {
      return 'PENDIENTE';
    }
    const pedido = this.pedidosApi.obtener(pago.pedidoDigitalId);
    if (pedido?.estado === 'Entregado' && pedido.ventaId) {
      return 'EMITIDO_SUNAT';
    }
    return 'PENDIENTE';
  }
}

function aSeries(
  items: { clave: string; etiqueta: string; monto: number; cantidad: number }[],
  total: number,
): SerieReporte[] {
  return items.map((item) => ({
    ...item,
    porcentaje: total > 0 ? round2((item.monto / total) * 100) : 0,
  }));
}

function startOfDay(isoDate: string): number {
  return new Date(`${isoDate}T00:00:00`).getTime();
}

function endOfDay(isoDate: string): number {
  return new Date(`${isoDate}T23:59:59.999`).getTime();
}

function dinero(valor: number): string {
  return valor.toFixed(2);
}

function etiquetaPeriodo(periodo: ReportesPeriodo): string {
  if (!periodo.desde && !periodo.hasta) {
    return 'Todo el histórico';
  }
  return `${periodo.desde || 'inicio'} a ${periodo.hasta || 'hoy'}`;
}

function escaparCsv(valor: string): string {
  if (/[",\n]/.test(valor)) {
    return `"${valor.replaceAll('"', '""')}"`;
  }
  return valor;
}
