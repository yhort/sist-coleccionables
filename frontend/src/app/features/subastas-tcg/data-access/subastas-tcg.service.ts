import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { SedesApiService } from '../../../core/data-access/sedes-api.service';
import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { codigoPedido, codigoSubasta } from '../../../core/ui/codigo-amigable';
import { StockApiService } from '../../inventario/data-access/stock.service';
import { cantidadLibreDe } from '../../inventario/models/inventario.model';
import { PedidosDigitalesApiService } from '../../pedidos-digitales/data-access/pedidos-digitales.service';
import { ProductosTcgApiService } from '../../productos-tcg/data-access/productos-tcg.service';
import { TipoProductoTcg } from '../../productos-tcg/models/producto-tcg.model';
import {
  AdjudicarSubastaRequest,
  CanalSubastaTcg,
  CrearSubastaRequest,
  EstadoSubastaTcg,
  MargenSubasta,
  PujaTcg,
  RegistrarPujaRequest,
  SubastaDetalle,
  SubastaTcg,
  SubastasTcgFiltros,
  calcularMargenSubasta,
  pujaMaxima,
  pujasOrdenadas,
} from '../models/subasta-tcg.model';

interface SubastaDetalleApi {
  id: string;
  productoId: string;
  productoNombre: string;
  codigoSku: string;
  tipoProducto: TipoProductoTcg;
  cantidad: number;
  orden: number;
}

interface SubastaApi {
  id: string;
  codigo?: string | null;
  sedeId: string;
  sedeNombre: string;
  productoId: string;
  productoNombre: string;
  codigoSku?: string;
  tipoProducto: TipoProductoTcg;
  detalles?: SubastaDetalleApi[] | null;
  titulo: string;
  canal: CanalSubastaTcg;
  precioBase: number;
  incrementoMinimo: number;
  precioReserva?: number | null;
  fechaInicio: string;
  fechaCierre: string;
  fechaCierreReal?: string | null;
  estado: EstadoSubastaTcg;
  pujaGanadoraId?: string | null;
  pedidoDigitalId?: string | null;
  pedidoCodigo?: string | null;
  observacion?: string | null;
  pujas: PujaTcg[];
  margen: MargenSubasta;
}

@Injectable({ providedIn: 'root' })
export class SubastasTcgApiService {
  private readonly http = inject(HttpClient);
  private readonly subastasSignal = signal<SubastaTcg[]>([]);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly stockApi = inject(StockApiService);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly sedesApi = inject(SedesApiService);

  readonly subastas = this.subastasSignal.asReadonly();
  readonly sedes = this.sedesApi.sedes;
  readonly productos = this.productosApi.productos;

  async refrescar(): Promise<SubastaTcg[]> {
    try {
      const items = await firstValueFrom(this.http.get<SubastaApi[]>(apiUrl('subastas-tcg')));
      const subastas = items.map(mapSubasta);
      this.subastasSignal.set(subastas);
      return subastas;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  listar(filtros: SubastasTcgFiltros): SubastaTcg[] {
    return this.subastasSignal()
      .filter((subasta) => {
        if (filtros.canal !== 'TODOS' && subasta.canal !== filtros.canal) {
          return false;
        }
        if (filtros.sedeId !== 'TODAS' && subasta.sedeId !== filtros.sedeId) {
          return false;
        }
        if (filtros.tipoProducto !== 'TODOS') {
          const tipos = new Set(
            (subasta.detalles?.length
              ? subasta.detalles.map((d) => d.tipoProducto)
              : [subasta.tipoProducto ?? this.productosApi.obtenerPorId(subasta.productoId)?.tipoProducto]
            ).filter(Boolean),
          );
          if (!tipos.has(filtros.tipoProducto)) {
            return false;
          }
        }
        return true;
      })
      .map(clonar)
      .sort(
        (a, b) => new Date(b.fechaInicio).getTime() - new Date(a.fechaInicio).getTime(),
      );
  }

  obtener(id: string): SubastaTcg | undefined {
    const encontrada = this.subastasSignal().find((item) => item.id === id);
    return encontrada ? clonar(encontrada) : undefined;
  }

  stockLibre(sedeId: string, productoId: string): number {
    const stock = this.stockApi.obtener(sedeId, productoId);
    return stock ? cantidadLibreDe(stock) : 0;
  }

  stockLibreLote(subasta: SubastaTcg): number {
    const lineas = subasta.detalles?.length
      ? subasta.detalles
      : [{ productoId: subasta.productoId, cantidad: 1 }];
    if (lineas.length === 0) {
      return 0;
    }
    return Math.min(
      ...lineas.map((linea) =>
        Math.floor(this.stockLibre(subasta.sedeId, linea.productoId) / Math.max(linea.cantidad, 1)),
      ),
    );
  }

  margenDe(subasta: SubastaTcg) {
    const ganadora = subasta.pujas.find((puja) => puja.id === subasta.pujaGanadoraId) ?? pujaMaxima(subasta);
    return calcularMargenSubasta(subasta.precioBase, ganadora?.monto ?? null);
  }

  crear(request: CrearSubastaRequest): Promise<SubastaTcg> {
    return this.enviar('POST', apiUrl('subastas-tcg'), {
      sedeId: request.sedeId,
      productoId: request.productoId ?? request.detalles[0]?.productoId,
      detalles: request.detalles.map((d) => ({
        productoId: d.productoId,
        cantidad: d.cantidad,
      })),
      titulo: request.titulo.trim(),
      canal: request.canal,
      precioBase: request.precioBase,
      incrementoMinimo: request.incrementoMinimo,
      precioReserva: request.precioReserva,
      fechaInicio: request.fechaInicio,
      fechaCierre: request.fechaCierre,
      observacion: request.observacion,
    });
  }

  activar(id: string): Promise<SubastaTcg> {
    return this.enviar('POST', apiUrl(`subastas-tcg/${id}/activar`));
  }

  registrarPuja(id: string, request: RegistrarPujaRequest): Promise<SubastaTcg> {
    return this.enviar('POST', apiUrl(`subastas-tcg/${id}/pujas`), {
      nombrePostor: request.nombrePostor,
      clienteId: request.clienteId || null,
      monto: request.monto,
    });
  }

  cerrar(id: string): Promise<SubastaTcg> {
    return this.enviar('POST', apiUrl(`subastas-tcg/${id}/cerrar`));
  }

  async adjudicar(id: string, checkout?: AdjudicarSubastaRequest): Promise<SubastaTcg> {
    const subasta = await this.enviar(
      'POST',
      apiUrl(`subastas-tcg/${id}/adjudicar`),
      checkout ?? {},
    );
    await Promise.all([
      this.stockApi.refrescarSede(subasta.sedeId).catch(() => undefined),
      this.pedidosApi.refrescar().catch(() => undefined),
    ]);
    return subasta;
  }

  async cancelar(id: string): Promise<SubastaTcg> {
    const subasta = await this.enviar('POST', apiUrl(`subastas-tcg/${id}/cancelar`));
    await Promise.all([
      this.stockApi.refrescarSede(subasta.sedeId).catch(() => undefined),
      this.pedidosApi.refrescar().catch(() => undefined),
    ]);
    return subasta;
  }

  private async enviar(method: 'POST', url: string, body?: unknown): Promise<SubastaTcg> {
    try {
      const dto = await firstValueFrom(this.http.post<SubastaApi>(url, body ?? {}));
      const subasta = mapSubasta(dto);
      this.upsert(subasta);
      return clonar(subasta);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private upsert(subasta: SubastaTcg): void {
    this.subastasSignal.update((items) => {
      const hay = items.some((item) => item.id === subasta.id);
      return hay
        ? items.map((item) => (item.id === subasta.id ? subasta : item))
        : [subasta, ...items];
    });
  }
}

function mapDetalles(dto: SubastaApi): SubastaDetalle[] {
  const raw = dto.detalles ?? [];
  if (raw.length > 0) {
    return [...raw]
      .sort((a, b) => a.orden - b.orden)
      .map((d) => ({
        id: d.id,
        productoId: d.productoId,
        productoNombre: d.productoNombre,
        codigoSku: d.codigoSku,
        tipoProducto: d.tipoProducto,
        cantidad: d.cantidad,
        orden: d.orden,
      }));
  }
  return [
    {
      id: '',
      productoId: dto.productoId,
      productoNombre: dto.productoNombre,
      codigoSku: dto.codigoSku ?? '',
      tipoProducto: dto.tipoProducto,
      cantidad: 1,
      orden: 1,
    },
  ];
}

function mapSubasta(dto: SubastaApi): SubastaTcg {
  const detalles = mapDetalles(dto);
  return {
    id: dto.id,
    codigo: dto.codigo?.trim() || codigoSubasta(dto.id),
    sedeId: dto.sedeId,
    sedeNombre: dto.sedeNombre,
    productoId: dto.productoId,
    productoNombre: dto.productoNombre,
    codigoSku: dto.codigoSku ?? detalles[0]?.codigoSku,
    tipoProducto: dto.tipoProducto,
    detalles,
    titulo: dto.titulo,
    canal: dto.canal,
    precioBase: dto.precioBase,
    incrementoMinimo: dto.incrementoMinimo,
    precioReserva: dto.precioReserva ?? null,
    fechaInicio: dto.fechaInicio,
    fechaCierre: dto.fechaCierre,
    fechaCierreReal: dto.fechaCierreReal ?? null,
    estado: dto.estado,
    pujaGanadoraId: dto.pujaGanadoraId ?? null,
    pedidoDigitalId: dto.pedidoDigitalId ?? null,
    pedidoCodigo: dto.pedidoCodigo?.trim() || (dto.pedidoDigitalId ? codigoPedido(dto.pedidoDigitalId) : null),
    observacion: dto.observacion ?? null,
    pujas: (dto.pujas ?? []).map((puja) => ({
      id: puja.id,
      subastaTcgId: puja.subastaTcgId,
      clienteId: puja.clienteId ?? null,
      nombrePostor: puja.nombrePostor,
      monto: puja.monto,
      fecha: puja.fecha,
      esGanadora: puja.esGanadora,
    })),
  };
}

function clonar(subasta: SubastaTcg): SubastaTcg {
  return {
    ...subasta,
    detalles: subasta.detalles.map((d) => ({ ...d })),
    pujas: pujasOrdenadas(subasta.pujas).map((puja) => ({ ...puja })),
  };
}
