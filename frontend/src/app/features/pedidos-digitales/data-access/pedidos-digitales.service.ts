import { HttpClient } from '@angular/common/http';
import { Injectable, Injector, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { SedesApiService } from '../../../core/data-access/sedes-api.service';
import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { codigoItem, codigoPedido, codigoSubasta, codigoVenta } from '../../../core/ui/codigo-amigable';
import { StockApiService } from '../../inventario/data-access/stock.service';
import { ProductosTcgApiService } from '../../productos-tcg/data-access/productos-tcg.service';
import {
  CanalPedidoDigital,
  CrearPedidoDesdeSubastaRequest,
  CrearPedidoDigitalRequest,
  EstadoPedidoDigital,
  IndicadorReservaPedido,
  PedidoDigital,
  PedidoDigitalDetalle,
  PedidoDigitalEntrega,
  PedidoDigitalHistorialEstado,
  PedidosDigitalesFiltros,
  origenDeCanal,
} from '../models/pedido-digital.model';

interface PedidoApi {
  id: string;
  codigo?: string | null;
  numeroPedido?: string | null;
  correlativo?: number | string | null;
  clienteId?: string | null;
  clienteNombre: string;
  clienteTelefono?: string | null;
  clienteTipoDocumento?: 'DNI' | 'RUC' | 'CE' | 'PASAPORTE' | 'SIN_DOCUMENTO' | null;
  clienteNumeroDocumento?: string | null;
  sedeId: string;
  sedeNombre: string;
  canalPedido: CanalPedidoDigital;
  estado: EstadoPedidoDigital;
  indicadorReserva: IndicadorReservaPedido;
  fechaPedido: string;
  subtotal: number;
  igv: number;
  total: number;
  referenciaExterna?: string | null;
  observacion?: string | null;
  subastaTcgId?: string | null;
  codigoSubasta?: string | null;
  tituloSubasta?: string | null;
  notificado?: boolean;
  fechaNotificacion?: string | null;
  ventaId?: string | null;
  codigoVenta?: string | null;
  entregaId?: string | null;
  entrega: PedidoDigitalEntrega;
  detalles: Array<PedidoDigitalDetalle & { codigoSku?: string | null; codigo?: string | null }>;
  historialEstados: PedidoDigitalHistorialEstado[];
}

export interface ComprobanteConsolidadoApi {
  ventaId: string;
  comprobante: {
    id: string;
    ventaId: string;
    tipo: 'BOLETA' | 'FACTURA' | 'NOTA_VENTA';
    serie: string;
    correlativo: number;
    estado: string;
    mensaje?: string | null;
    fechaEmision: string;
    clienteNombre?: string | null;
    total?: number | null;
    hashFirma?: string | null;
    tieneXml?: boolean;
    tieneCdr?: boolean;
    tienePdf?: boolean;
  };
  pedidos: PedidoApi[];
}

@Injectable({ providedIn: 'root' })
export class PedidosDigitalesApiService {
  private readonly http = inject(HttpClient);
  private readonly stockApi = inject(StockApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly injector = inject(Injector);
  private readonly pedidosSignal = signal<PedidoDigital[]>([]);

  readonly pedidos = this.pedidosSignal.asReadonly();
  readonly sedes = this.sedesApi.sedes;
  readonly productos = this.productosApi.productos;

  async refrescar(): Promise<PedidoDigital[]> {
    try {
      const items = await firstValueFrom(this.http.get<PedidoApi[]>(apiUrl('pedidos-digitales')));
      const pedidos = items.map(mapPedido);
      this.pedidosSignal.set(pedidos);
      return pedidos;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  listar(filtros: PedidosDigitalesFiltros): PedidoDigital[] {
    const busqueda = filtros.busqueda.trim().toLowerCase();
    const clienteQ = filtros.cliente.trim().toLowerCase();
    const desde = filtros.desde ? Date.parse(`${filtros.desde}T00:00:00`) : null;
    const hasta = filtros.hasta ? Date.parse(`${filtros.hasta}T23:59:59.999`) : null;

    return this.pedidosSignal()
      .filter((pedido) => {
        if (filtros.origen !== 'TODOS' && origenDeCanal(pedido.canalPedido) !== filtros.origen) {
          return false;
        }
        if (filtros.subastaTcgId !== 'TODAS') {
          if ((pedido.subastaTcgId ?? '') !== filtros.subastaTcgId) {
            return false;
          }
        }
        if (clienteQ) {
          const clienteHaystack = [
            pedido.clienteNombre,
            pedido.clienteTelefono ?? '',
            pedido.entrega.destinatarioNombre ?? '',
            pedido.entrega.contactoReferencia ?? '',
          ]
            .join(' ')
            .toLowerCase();
          if (!clienteHaystack.includes(clienteQ)) {
            return false;
          }
        }
        if (filtros.notificacion === 'NOTIFICADOS' && !pedido.notificado) {
          return false;
        }
        if (filtros.notificacion === 'SIN_NOTIFICAR' && pedido.notificado) {
          return false;
        }
        if (busqueda) {
          const haystack = [
            pedido.codigo,
            pedido.id,
            pedido.clienteNombre,
            pedido.clienteTelefono ?? '',
            pedido.referenciaExterna ?? '',
            pedido.entrega.numeroTracking ?? '',
            pedido.entrega.destinatarioNombre ?? '',
            pedido.codigoSubasta ?? '',
            pedido.tituloSubasta ?? '',
            pedido.codigoVenta ?? '',
            ...pedido.detalles.map((detalle) => detalle.descripcion),
          ]
            .join(' ')
            .toLowerCase();
          if (!haystack.includes(busqueda)) {
            return false;
          }
        }
        const fecha = new Date(pedido.fechaPedido).getTime();
        if (desde && fecha < desde) {
          return false;
        }
        if (hasta && fecha > hasta) {
          return false;
        }
        if (filtros.montoMin !== null && pedido.total < filtros.montoMin) {
          return false;
        }
        if (filtros.montoMax !== null && pedido.total > filtros.montoMax) {
          return false;
        }
        return true;
      })
      .map(clonarPedido)
      .sort((a, b) => new Date(b.fechaPedido).getTime() - new Date(a.fechaPedido).getTime());
  }

  obtener(id: string): PedidoDigital | undefined {
    const encontrada = this.pedidosSignal().find((item) => item.id === id);
    return encontrada ? clonarPedido(encontrada) : undefined;
  }

  listarPendientePago(): PedidoDigital[] {
    return this.pedidosSignal()
      .filter((pedido) => pedido.estado === 'PendientePago')
      .map(clonarPedido);
  }

  stockLibre(sedeId: string, productoId: string): number {
    return this.stockApi.obtener(sedeId, productoId)?.cantidadLibre ?? 0;
  }

  async crear(request: CrearPedidoDigitalRequest): Promise<PedidoDigital> {
    try {
      const creado = await firstValueFrom(
        this.http.post<PedidoApi>(apiUrl('pedidos-digitales'), {
          clienteId: request.clienteId ?? null,
          clienteNombre: request.clienteNombre,
          clienteTelefono: request.clienteTelefono ?? null,
          tipoDocumento: request.tipoDocumento ?? null,
          numeroDocumento: request.numeroDocumento ?? null,
          esClienteVarios: request.esClienteVarios ?? false,
          sedeId: request.sedeId,
          canalPedido: request.canalPedido,
          observacion: request.observacion ?? null,
          detalles: request.detalles,
          entrega: request.entrega,
          guardarPuntoEnCliente: request.guardarPuntoEnCliente ?? false,
          cobroInmediato: request.cobroInmediato
            ? {
                origen: request.cobroInmediato.origen,
                codigoOperacion: request.cobroInmediato.codigoOperacion ?? null,
                referenciaExterna: request.cobroInmediato.referenciaExterna ?? null,
                montoRecibido: request.cobroInmediato.montoRecibido ?? null,
              }
            : null,
        }),
      );
      const pedido = mapPedido(creado);
      this.pedidosSignal.update((items) => [pedido, ...items.filter((item) => item.id !== pedido.id)]);
      await this.stockApi.refrescarSede(pedido.sedeId).catch(() => undefined);
      return clonarPedido(pedido);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async crearDesdeSubasta(request: CrearPedidoDesdeSubastaRequest): Promise<PedidoDigital> {
    return this.crear({
      clienteId: request.clienteId,
      clienteNombre: request.clienteNombre,
      sedeId: request.sedeId,
      canalPedido: request.canalPedido,
      observacion: request.observacion ?? null,
      detalles: [
        {
          productoId: request.productoId,
          cantidad: 1,
          precioUnitario: request.total,
        },
      ],
      entrega: {
        destinatarioNombre: request.clienteNombre,
        destinatarioTelefono: null,
        direccion: null,
        distrito: null,
        provincia: null,
        departamento: null,
        courier: 'Recojo en tienda',
        esRecojoTienda: true,
      },
    });
  }

  async importarDesdeWooCommerce(request: {
    referenciaExterna: string;
    clienteNombre: string;
    clienteTelefono?: string | null;
    sedeId: string;
    pagado: boolean;
    observacion?: string | null;
    detalles: CrearPedidoDigitalRequest['detalles'];
    entrega: PedidoDigitalEntrega;
  }): Promise<PedidoDigital> {
    const pedido = await this.crear({
      clienteNombre: request.clienteNombre,
      clienteTelefono: request.clienteTelefono,
      sedeId: request.sedeId,
      canalPedido: 'WOOCOMMERCE',
      observacion: request.observacion ?? `Importado desde WooCommerce (${request.referenciaExterna}).`,
      detalles: request.detalles,
      entrega: request.entrega,
    });
    if (request.pagado && pedido.estado === 'PendientePago') {
      return this.cambiarEstado(pedido.id, 'Pagado', 'Pago confirmado en la pasarela de WooCommerce.');
    }
    return pedido;
  }

  async cambiarEstado(
    id: string,
    estadoNuevo: EstadoPedidoDigital,
    observacion?: string,
  ): Promise<PedidoDigital> {
    if (estadoNuevo === 'Cancelado') {
      return this.cancelar(id, observacion);
    }

    try {
      const nota = observacion?.trim() || etiquetaTransicion(estadoNuevo);
      const actualizado = await firstValueFrom(
        this.http.put<PedidoApi>(apiUrl(`pedidos-digitales/${id}/estado`), {
          estado: estadoNuevo,
          observacion: nota,
        }),
      );
      const pedido = mapPedido(actualizado);
      this.reemplazar(pedido);
      if (estadoNuevo === 'Entregado') {
        await this.stockApi.refrescarSede(pedido.sedeId).catch(() => undefined);
      }
      return clonarPedido(pedido);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async cambiarEstadoLote(
    ids: readonly string[],
    estadoNuevo: EstadoPedidoDigital,
    observacion?: string,
  ): Promise<PedidoDigital[]> {
    if (ids.length === 0) {
      throw new Error('Selecciona al menos un pedido.');
    }
    try {
      const actualizados = await firstValueFrom(
        this.http.post<PedidoApi[]>(apiUrl('pedidos-digitales/estado-lote'), {
          estado: estadoNuevo,
          observacion: observacion?.trim() || null,
          pedidoDigitalIds: [...ids],
        }),
      );
      const pedidos = actualizados.map(mapPedido);
      for (const pedido of pedidos) {
        this.reemplazar(pedido);
      }
      if (estadoNuevo === 'Entregado') {
        const sedes = [...new Set(pedidos.map((pedido) => pedido.sedeId))];
        await Promise.all(sedes.map((sedeId) => this.stockApi.refrescarSede(sedeId).catch(() => undefined)));
      }
      return pedidos.map(clonarPedido);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async actualizarNotificacion(id: string, notificado: boolean): Promise<PedidoDigital> {
    try {
      const actualizado = await firstValueFrom(
        this.http.put<PedidoApi>(apiUrl(`pedidos-digitales/${id}/notificacion`), { notificado }),
      );
      const pedido = mapPedido(actualizado);
      this.reemplazar(pedido);
      return clonarPedido(pedido);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async marcarNotificados(ids: readonly string[]): Promise<PedidoDigital[]> {
    if (ids.length === 0) {
      return [];
    }
    try {
      const actualizados = await firstValueFrom(
        this.http.post<PedidoApi[]>(apiUrl('pedidos-digitales/notificacion-lote'), {
          notificado: true,
          pedidoDigitalIds: [...ids],
        }),
      );
      const pedidos = actualizados.map(mapPedido);
      for (const pedido of pedidos) {
        this.reemplazar(pedido);
      }
      return pedidos.map(clonarPedido);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async emitirComprobanteConsolidado(
    ids: readonly string[],
    tipo: 'BOLETA' | 'FACTURA' | 'NOTA_VENTA',
  ): Promise<ComprobanteConsolidadoApi> {
    if (ids.length === 0) {
      throw new Error('Selecciona al menos un pedido entregado.');
    }
    try {
      const resultado = await firstValueFrom(
        this.http.post<ComprobanteConsolidadoApi>(apiUrl('pedidos-digitales/comprobante-consolidado'), {
          tipoComprobante: tipo,
          pedidoDigitalIds: [...ids],
        }),
      );
      for (const pedido of resultado.pedidos.map(mapPedido)) {
        this.reemplazar(pedido);
      }
      return resultado;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async cancelar(id: string, observacion?: string): Promise<PedidoDigital> {
    try {
      const cancelado = await firstValueFrom(
        this.http.post<PedidoApi>(apiUrl(`pedidos-digitales/${id}/cancelar`), {
          observacion: observacion ?? null,
        }),
      );
      const pedido = mapPedido(cancelado);
      this.reemplazar(pedido);
      await this.stockApi.refrescarSede(pedido.sedeId).catch(() => undefined);
      if (pedido.subastaTcgId) {
        const { SubastasTcgApiService } = await import('../../subastas-tcg/data-access/subastas-tcg.service');
        await this.injector.get(SubastasTcgApiService).refrescar().catch(() => undefined);
      }
      return clonarPedido(pedido);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  actualizarSnapshotEntrega(id: string, patch: Partial<PedidoDigitalEntrega>): PedidoDigital {
    const actual = this.requiere(id);
    const siguiente: PedidoDigital = {
      ...actual,
      entrega: { ...actual.entrega, ...patch },
    };
    this.reemplazar(siguiente);
    return clonarPedido(siguiente);
  }

  listarParaDespacho(): PedidoDigital[] {
    return this.pedidosSignal()
      .filter(
        (pedido) =>
          pedido.estado === 'Pagado' ||
          pedido.estado === 'Empaquetado' ||
          pedido.estado === 'PendienteEntrega',
      )
      .map(clonarPedido)
      .sort((a, b) => new Date(b.fechaPedido).getTime() - new Date(a.fechaPedido).getTime());
  }

  async convertirVenta(id: string, observacion?: string): Promise<PedidoDigital> {
    return this.cambiarEstado(id, 'Entregado', observacion);
  }

  private requiere(id: string): PedidoDigital {
    const encontrada = this.pedidosSignal().find((item) => item.id === id);
    if (!encontrada) {
      throw new Error('No se encontró el pedido digital.');
    }
    return clonarPedido(encontrada);
  }

  private reemplazar(pedido: PedidoDigital): void {
    this.pedidosSignal.update((items) =>
      items.map((item) => (item.id === pedido.id ? pedido : item)),
    );
  }
}

function mapPedido(dto: PedidoApi): PedidoDigital {
  return {
    id: dto.id,
    codigo: codigoPedido({
      id: dto.id,
      codigo: dto.codigo,
      numeroPedido: dto.numeroPedido,
      correlativo: dto.correlativo,
    }),
    clienteId: dto.clienteId ?? null,
    clienteNombre: dto.clienteNombre,
    clienteTelefono: dto.clienteTelefono ?? null,
    clienteTipoDocumento: dto.clienteTipoDocumento ?? null,
    clienteNumeroDocumento: dto.clienteNumeroDocumento ?? null,
    sedeId: dto.sedeId,
    sedeNombre: dto.sedeNombre,
    canalPedido: dto.canalPedido,
    estado: dto.estado,
    indicadorReserva: dto.indicadorReserva,
    fechaPedido: dto.fechaPedido,
    subtotal: dto.subtotal,
    igv: dto.igv,
    total: dto.total,
    referenciaExterna: dto.referenciaExterna ?? null,
    observacion: dto.observacion ?? null,
    subastaTcgId: dto.subastaTcgId ?? null,
    codigoSubasta: dto.codigoSubasta?.trim() || (dto.subastaTcgId ? codigoSubasta(dto.subastaTcgId) : null),
    tituloSubasta: dto.tituloSubasta?.trim() || null,
    notificado: !!dto.notificado,
    fechaNotificacion: dto.fechaNotificacion ?? null,
    ventaId: dto.ventaId ?? null,
    codigoVenta: dto.codigoVenta?.trim() || (dto.ventaId ? codigoVenta(dto.ventaId) : null),
    detalles: dto.detalles.map((detalle) => ({
      id: detalle.id,
      codigo: detalle.codigo?.trim() || codigoItem(detalle.id),
      productoId: detalle.productoId,
      descripcion: detalle.descripcion,
      cantidad: detalle.cantidad,
      precioUnitario: detalle.precioUnitario,
      total: detalle.total,
    })),
    historialEstados: dto.historialEstados.map((evento) => ({ ...evento })),
    entrega: { ...dto.entrega },
  };
}

function clonarPedido(pedido: PedidoDigital): PedidoDigital {
  return {
    ...pedido,
    detalles: pedido.detalles.map((detalle) => ({ ...detalle })),
    historialEstados: pedido.historialEstados.map((evento) => ({ ...evento })),
    entrega: { ...pedido.entrega },
  };
}

function etiquetaTransicion(estado: EstadoPedidoDigital): string {
  switch (estado) {
    case 'Pagado':
      return 'Pago registrado. El pedido pasa a empaque.';
    case 'Empaquetado':
      return 'Pedido empaquetado.';
    case 'PendienteEntrega':
      return 'Listo para despacho o recojo.';
    case 'Entregado':
      return 'Entrega confirmada. Se convierte a venta.';
    default:
      return `Estado actualizado a ${estado}.`;
  }
}
