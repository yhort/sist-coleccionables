import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { SedesApiService } from '../../../core/data-access/sedes-api.service';
import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { ResultadoPaginado } from '../../../shared/ui/tabla-paginacion/paginacion';
import { ProductosTcgApiService } from '../../productos-tcg/data-access/productos-tcg.service';
import { TipoProductoTcg } from '../../productos-tcg/models/producto-tcg.model';
import { ETIQUETAS_TIPO } from '../../productos-tcg/models/producto-tcg.model';
import {
  AjusteInventarioRequest,
  RegistrarMovimientoRequest,
  SentidoAjuste,
  StockFila,
  StockProductoSede,
  TipoMovimientoInventario,
  cantidadLibreDe,
  requiereSentidoManual,
  sentidoPorDefecto,
} from '../models/inventario.model';
import { InventarioStore } from './inventario-store.service';

interface StockApi {
  id?: string | null;
  sedeId: string;
  sedeNombre: string;
  productoId: string;
  productoNombre: string;
  codigoSku: string;
  tipoProducto: TipoProductoTcg;
  cantidadDisponible: number;
  cantidadReservada: number;
  cantidadLibre: number;
}

interface StockSedePaginadoApi extends ResultadoPaginado<StockApi> {
  resumen: {
    skus: number;
    disponible: number;
    reservado: number;
    libre: number;
  };
}

export interface StockSedePaginado extends ResultadoPaginado<StockFila> {
  resumen: StockSedePaginadoApi['resumen'];
}

@Injectable({ providedIn: 'root' })
export class StockApiService {
  private readonly http = inject(HttpClient);
  private readonly store = inject(InventarioStore);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly filasSignal = signal<StockFila[]>([]);
  private sedeCargada: string | null = null;

  readonly filas = this.filasSignal.asReadonly();
  readonly sedes = this.sedesApi.sedes;

  /** Última sede cargada en `filas` (p.ej. tras alta con stock). */
  sedeActiva(): string | null {
    return this.sedeCargada;
  }

  async obtenerProductoSede(productoId: string, sedeId: string): Promise<StockFila | null> {
    try {
      const dto = await firstValueFrom(
        this.http.get<StockApi>(apiUrl(`inventario/stock/${productoId}`), {
          params: new HttpParams().set('sedeId', sedeId),
        }),
      );
      return mapStockApi(dto);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  /**
   * Tras un alta con stock: lee el stock real del API y lo fusiona en `filas`
   * para que el listado de productos no quede en 0 por un refresh incompleto.
   */
  async confirmarStockProducto(
    productoId: string,
    sedeId: string,
    stockEsperado?: number,
  ): Promise<StockFila> {
    const fila = await this.obtenerProductoSede(productoId, sedeId);
    if (!fila) {
      throw new Error('No se pudo confirmar el stock del producto creado.');
    }
    if (stockEsperado != null && fila.cantidadLibre < stockEsperado) {
      // Reintenta una vez por si el commit aún no es visible.
      await new Promise((r) => setTimeout(r, 150));
      const reintento = await this.obtenerProductoSede(productoId, sedeId);
      if (reintento) {
        this.upsertFila(reintento);
        this.productosApi.aplicarStockLocal(new Map([[productoId, reintento.cantidadLibre]]));
        return reintento;
      }
    }
    this.upsertFila(fila);
    this.productosApi.aplicarStockLocal(new Map([[productoId, fila.cantidadLibre]]));
    return fila;
  }

  upsertFila(fila: StockFila): void {
    this.filasSignal.update((items) => {
      const resto = items.filter(
        (item) => !(item.sedeId === fila.sedeId && item.productoId === fila.productoId),
      );
      return [fila, ...resto];
    });
    this.sedeCargada = fila.sedeId;
  }

  async refrescarSede(sedeId: string, busqueda = ''): Promise<StockFila[]> {
    const pagina = await this.consultarPagina(sedeId, busqueda);
    this.filasSignal.set(pagina.items);
    this.sedeCargada = sedeId;
    this.productosApi.aplicarStockLocal(
      new Map(pagina.items.map((fila) => [fila.productoId, fila.cantidadLibre])),
    );
    return pagina.items;
  }

  async consultarPagina(
    sedeId: string,
    busqueda = '',
    page?: number,
    pageSize?: number,
  ): Promise<StockSedePaginado> {
    try {
      let params = new HttpParams().set('sedeId', sedeId);
      const query = busqueda.trim();
      if (query) {
        params = params.set('q', query);
      }
      if (page != null) {
        params = params.set('page', String(page));
      }
      if (pageSize != null) {
        params = params.set('pageSize', String(pageSize));
      }
      const result = await firstValueFrom(
        this.http.get<StockSedePaginadoApi>(apiUrl('inventario/stock'), { params }),
      );
      const items = result.items.map(mapStockApi);
      if (page != null && pageSize != null) {
        this.filasSignal.set(items);
        this.sedeCargada = sedeId;
        this.productosApi.aplicarStockLocal(
          new Map(items.map((fila) => [fila.productoId, fila.cantidadLibre])),
        );
      }
      return {
        items,
        total: result.total,
        page: result.page,
        pageSize: result.pageSize,
        resumen: result.resumen,
      };
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  listarPorSede(sedeId: string, busqueda = ''): StockFila[] {
    const query = busqueda.trim().toLowerCase();
    const origen =
      this.sedeCargada === sedeId && this.filasSignal().length >= 0
        ? this.filasSignal().filter((item) => item.sedeId === sedeId)
        : this.store
            .stocks()
            .filter((item) => item.sedeId === sedeId)
            .map((item) => this.toFila(item));

    return origen
      .filter((fila) => {
        if (!query) {
          return true;
        }
        return `${fila.productoNombre} ${fila.codigoSku} ${ETIQUETAS_TIPO[fila.tipoProducto]}`
          .toLowerCase()
          .includes(query);
      })
      .sort((a, b) => a.productoNombre.localeCompare(b.productoNombre, 'es'));
  }

  obtener(sedeId: string, productoId: string): StockFila | undefined {
    const api = this.filasSignal().find(
      (item) => item.sedeId === sedeId && item.productoId === productoId,
    );
    if (api) {
      return api;
    }
    const stock = this.store
      .stocks()
      .find((item) => item.sedeId === sedeId && item.productoId === productoId);
    return stock ? this.toFila(stock) : undefined;
  }

  async ajustar(request: AjusteInventarioRequest): Promise<StockFila> {
    try {
      const dto = await firstValueFrom(
        this.http.put<StockApi>(apiUrl('inventario/ajustar'), {
          productoId: request.productoId,
          sedeId: request.sedeId,
          cantidad: request.cantidad,
          sentido: request.sentido,
          motivo: request.motivo,
        }),
      );
      const fila = mapStockApi(dto);
      await this.refrescarSede(request.sedeId);
      return fila;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  registrarMovimientos(requests: readonly RegistrarMovimientoRequest[]): void {
    this.store.transaccionar(() => {
      for (const request of requests) {
        this.registrarMovimiento(request);
      }
    });
  }

  registrarMovimiento(request: RegistrarMovimientoRequest): StockFila {
    const cantidad = Number(request.cantidad);
    if (!Number.isFinite(cantidad) || cantidad <= 0) {
      throw new Error('La cantidad debe ser mayor que cero.');
    }

    const motivo = request.motivo.trim();
    if (motivo.length < 3) {
      throw new Error('Indica un motivo o justificación.');
    }

    const actual =
      this.store
        .stocks()
        .find((item) => item.sedeId === request.sedeId && item.productoId === request.productoId) ??
      this.crearStockVacio(request.sedeId, request.productoId);

    const efectoBase = calcularEfecto(request.tipoMovimiento, request.sentido, cantidad);
    let efecto = request.invertir
      ? {
          deltaDisponible: -efectoBase.deltaDisponible,
          deltaReservada: -efectoBase.deltaReservada,
        }
      : efectoBase;

    // Confirmar reserva: VENTA baja disponible y, si hay unidades reservadas, las libera.
    if (request.tipoMovimiento === 'VENTA' && !request.invertir) {
      const reservaAConfirmar = Math.min(cantidad, actual.cantidadReservada);
      efecto = { deltaDisponible: -cantidad, deltaReservada: -reservaAConfirmar };
    }
    const siguiente: StockProductoSede = {
      ...actual,
      cantidadDisponible: actual.cantidadDisponible + efecto.deltaDisponible,
      cantidadReservada: actual.cantidadReservada + efecto.deltaReservada,
    };

    if (siguiente.cantidadDisponible < 0) {
      throw new Error('El stock disponible no puede quedar negativo.');
    }
    if (siguiente.cantidadReservada < 0) {
      throw new Error('El stock reservado no puede quedar negativo.');
    }
    if (cantidadLibreDe(siguiente) < 0) {
      throw new Error('La reserva no puede superar el stock disponible.');
    }

    this.store.stocks.update((items) => {
      const existe = items.some((item) => item.id === actual.id);
      return existe
        ? items.map((item) => (item.id === actual.id ? siguiente : item))
        : [siguiente, ...items];
    });

    this.store.movimientos.update((items) => [
      {
        id: globalThis.crypto.randomUUID(),
        sedeId: request.sedeId,
        productoId: request.productoId,
        tipoMovimiento: request.tipoMovimiento,
        cantidad,
        stockAnterior: actual.cantidadDisponible,
        stockPosterior: siguiente.cantidadDisponible,
        reservadoAnterior: actual.cantidadReservada,
        reservadoPosterior: siguiente.cantidadReservada,
        referenciaTipo: request.referenciaTipo,
        referenciaId: request.referenciaId,
        motivo,
        usuario: request.usuario?.trim() || 'Equipo Trunqi',
        fechaCreacion: new Date().toISOString(),
      },
      ...items,
    ]);

    return this.toFila(siguiente);
  }

  private crearStockVacio(sedeId: string, productoId: string): StockProductoSede {
    return {
      id: globalThis.crypto.randomUUID(),
      sedeId,
      productoId,
      cantidadDisponible: 0,
      cantidadReservada: 0,
    };
  }

  private toFila(stock: StockProductoSede): StockFila {
    const producto = this.productosApi.obtenerPorId(stock.productoId);

    return {
      ...stock,
      productoNombre: producto?.nombre ?? 'Producto',
      codigoSku: producto?.codigoSku ?? '—',
      tipoProducto: producto?.tipoProducto ?? 'ACCESORIO',
      sedeNombre: this.sedesApi.sedes().find((item) => item.id === stock.sedeId)?.nombre ?? stock.sedeId,
      cantidadLibre: cantidadLibreDe(stock),
    };
  }
}

function mapStockApi(dto: StockApi): StockFila {
  return {
    id: dto.id ?? `${dto.sedeId}-${dto.productoId}`,
    sedeId: dto.sedeId,
    productoId: dto.productoId,
    cantidadDisponible: dto.cantidadDisponible,
    cantidadReservada: dto.cantidadReservada,
    productoNombre: dto.productoNombre,
    codigoSku: dto.codigoSku,
    tipoProducto: dto.tipoProducto,
    sedeNombre: dto.sedeNombre,
    cantidadLibre: dto.cantidadLibre,
  };
}

export function calcularEfecto(
  tipo: TipoMovimientoInventario,
  sentido: SentidoAjuste,
  cantidad: number,
): { deltaDisponible: number; deltaReservada: number } {
  const clasificacion = sentidoPorDefecto(tipo);

  if (clasificacion === 'RESERVA') {
    return { deltaDisponible: 0, deltaReservada: cantidad };
  }
  if (clasificacion === 'LIBERACION') {
    return { deltaDisponible: 0, deltaReservada: -cantidad };
  }

  const direccion = requiereSentidoManual(tipo) ? sentido : clasificacion;
  const signo = direccion === 'ENTRADA' ? 1 : -1;

  if (tipo === 'VENTA') {
    return { deltaDisponible: -cantidad, deltaReservada: 0 };
  }

  return { deltaDisponible: signo * cantidad, deltaReservada: 0 };
}
