import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { SedesApiService } from '../../../core/data-access/sedes-api.service';
import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { StockApiService } from '../../inventario/data-access/stock.service';
import { cantidadLibreDe } from '../../inventario/models/inventario.model';
import { ProductosTcgApiService } from '../../productos-tcg/data-access/productos-tcg.service';
import { FILTROS_PRODUCTOS_VACIOS, ProductoTcg } from '../../productos-tcg/models/producto-tcg.model';
import {
  AperturaDetalleInput,
  AperturaTcg,
  AperturaTcgDetalle,
  AperturaTcgFiltros,
  CrearAperturaRequest,
  EstadoAperturaTcg,
  EstadoCartaObtenida,
  RendimientoApertura,
  calcularRendimiento,
  costoSelladoDe,
  valorEstimadoCartas,
} from '../models/apertura-tcg.model';

interface AperturaApi {
  id: string;
  sedeId: string;
  sedeNombre: string;
  productoSelladoId: string;
  productoSelladoNombre: string;
  cantidadSellados: number;
  estado: EstadoAperturaTcg;
  usuarioNombre: string;
  observacion?: string | null;
  fechaCreacion: string;
  fechaConfirmacion?: string | null;
  detalles: Array<{
    id: string;
    productoCartaId: string;
    cantidad: number;
    costoUnitarioAsignado?: number | null;
    estado: EstadoCartaObtenida;
    esFoil: boolean;
  }>;
  rendimiento: RendimientoApertura;
}

@Injectable({ providedIn: 'root' })
export class AperturasTcgApiService {
  private readonly http = inject(HttpClient);
  private readonly aperturasSignal = signal<AperturaTcg[]>([]);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly stockApi = inject(StockApiService);
  private readonly sedesApi = inject(SedesApiService);

  readonly aperturas = this.aperturasSignal.asReadonly();
  readonly sedes = this.sedesApi.sedes;

  async refrescar(): Promise<AperturaTcg[]> {
    try {
      const items = await firstValueFrom(this.http.get<AperturaApi[]>(apiUrl('aperturas-tcg')));
      const aperturas = items.map(mapApertura);
      this.aperturasSignal.set(aperturas);
      return aperturas;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  listar(filtros: AperturaTcgFiltros): AperturaTcg[] {
    const desde = filtros.desde ? startOfDay(filtros.desde) : null;
    const hasta = filtros.hasta ? endOfDay(filtros.hasta) : null;

    return this.aperturasSignal()
      .filter((apertura) => {
        if (filtros.sedeId !== 'TODAS' && apertura.sedeId !== filtros.sedeId) {
          return false;
        }
        if (filtros.estado !== 'TODOS' && apertura.estado !== filtros.estado) {
          return false;
        }
        const fecha = new Date(apertura.fechaCreacion).getTime();
        if (desde && fecha < desde) {
          return false;
        }
        if (hasta && fecha > hasta) {
          return false;
        }
        return true;
      })
      .map(clonar)
      .sort(
        (a, b) => new Date(b.fechaCreacion).getTime() - new Date(a.fechaCreacion).getTime(),
      );
  }

  obtener(id: string): AperturaTcg | undefined {
    const encontrada = this.aperturasSignal().find((item) => item.id === id);
    return encontrada ? clonar(encontrada) : undefined;
  }

  selladosParaApertura(): ProductoTcg[] {
    return this.productosApi
      .listar({ ...FILTROS_PRODUCTOS_VACIOS, tipoProducto: 'SELLADO' })
      .filter((producto) => producto.activo && producto.atributosTcg.permiteApertura);
  }

  cartasCatalogo(): ProductoTcg[] {
    return this.productosApi.listar({ ...FILTROS_PRODUCTOS_VACIOS, tipoProducto: 'CARTA' });
  }

  stockLibre(sedeId: string, productoId: string): number {
    const stock = this.stockApi.obtener(sedeId, productoId);
    return stock ? cantidadLibreDe(stock) : 0;
  }

  rendimientoDe(apertura: AperturaTcg): RendimientoApertura {
    if (apertura.rendimiento) {
      return { ...apertura.rendimiento };
    }
    const sellado = this.productosApi.obtenerPorId(apertura.productoSelladoId);
    return calcularRendimiento(
      costoSelladoDe(sellado, apertura.cantidadSellados),
      valorEstimadoCartas(apertura.detalles, (id) => this.productosApi.obtenerPorId(id)),
    );
  }

  crear(request: CrearAperturaRequest): Promise<AperturaTcg> {
    return this.enviar('POST', apiUrl('aperturas-tcg'), {
      sedeId: request.sedeId,
      productoSelladoId: request.productoSelladoId,
      cantidadSellados: request.cantidadSellados,
      observacion: request.observacion,
    });
  }

  actualizarBorrador(id: string, request: CrearAperturaRequest): Promise<AperturaTcg> {
    return this.enviar('PUT', apiUrl(`aperturas-tcg/${id}`), {
      sedeId: request.sedeId,
      productoSelladoId: request.productoSelladoId,
      cantidadSellados: request.cantidadSellados,
      observacion: request.observacion,
    });
  }

  reemplazarDetalles(id: string, inputs: readonly AperturaDetalleInput[]): Promise<AperturaTcg> {
    return this.enviar(
      'PUT',
      apiUrl(`aperturas-tcg/${id}/detalles`),
      inputs.map((input) => ({
        productoCartaId: input.productoCartaId,
        cantidad: input.cantidad,
        estado: input.estado,
        esFoil: input.esFoil,
      })),
    );
  }

  async confirmar(id: string): Promise<AperturaTcg> {
    const apertura = await this.enviar('POST', apiUrl(`aperturas-tcg/${id}/confirmar`));
    await this.stockApi.refrescarSede(apertura.sedeId).catch(() => undefined);
    return apertura;
  }

  async anular(id: string): Promise<AperturaTcg> {
    const apertura = await this.enviar('POST', apiUrl(`aperturas-tcg/${id}/anular`));
    await this.stockApi.refrescarSede(apertura.sedeId).catch(() => undefined);
    return apertura;
  }

  private async enviar(
    method: 'POST' | 'PUT',
    url: string,
    body?: unknown,
  ): Promise<AperturaTcg> {
    try {
      const dto =
        method === 'POST'
          ? await firstValueFrom(this.http.post<AperturaApi>(url, body ?? {}))
          : await firstValueFrom(this.http.put<AperturaApi>(url, body ?? {}));
      const apertura = mapApertura(dto);
      this.upsert(apertura);
      return clonar(apertura);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private upsert(apertura: AperturaTcg): void {
    this.aperturasSignal.update((items) => {
      const hay = items.some((item) => item.id === apertura.id);
      return hay
        ? items.map((item) => (item.id === apertura.id ? apertura : item))
        : [apertura, ...items];
    });
  }
}

function mapApertura(dto: AperturaApi): AperturaTcg {
  return {
    id: dto.id,
    sedeId: dto.sedeId,
    sedeNombre: dto.sedeNombre,
    productoSelladoId: dto.productoSelladoId,
    productoSelladoNombre: dto.productoSelladoNombre,
    cantidadSellados: dto.cantidadSellados,
    estado: dto.estado,
    usuarioNombre: dto.usuarioNombre,
    observacion: dto.observacion ?? null,
    fechaCreacion: dto.fechaCreacion,
    fechaConfirmacion: dto.fechaConfirmacion ?? null,
    detalles: dto.detalles.map(
      (detalle): AperturaTcgDetalle => ({
        id: detalle.id,
        productoCartaId: detalle.productoCartaId,
        cantidad: detalle.cantidad,
        costoUnitarioAsignado: detalle.costoUnitarioAsignado ?? null,
        estado: detalle.estado,
        esFoil: detalle.esFoil,
      }),
    ),
    rendimiento: dto.rendimiento,
  };
}

function clonar(apertura: AperturaTcg): AperturaTcg {
  return {
    ...apertura,
    rendimiento: apertura.rendimiento ? { ...apertura.rendimiento } : undefined,
    detalles: apertura.detalles.map((detalle) => ({ ...detalle })),
  };
}

function startOfDay(isoDate: string): number {
  return new Date(`${isoDate}T00:00:00`).getTime();
}

function endOfDay(isoDate: string): number {
  return new Date(`${isoDate}T23:59:59.999`).getTime();
}
