import { HttpClient, HttpParams } from '@angular/common/http';
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
  EstadoSubastaDetalle,
  EstadoSubastaTcg,
  MargenSubasta,
  ModoSubastaTcg,
  PujaTcg,
  RegistrarPujaRequest,
  SubastaDetalle,
  SubastaTcg,
  SubastasTcgFiltros,
  calcularMargenSubasta,
  pujaMaxima,
  pujasOrdenadas,
} from '../models/subasta-tcg.model';

/** Rango de fechas opcional para GET /api/subastas-tcg (`fechaDesde` / `fechaHasta`). */
export interface SubastasTcgRangoFechas {
  desde?: string | null;
  hasta?: string | null;
}

interface SubastaDetalleApi {
  id: string;
  productoId: string;
  productoNombre: string;
  tituloPersonalizado?: string | null;
  codigoSku: string;
  tipoProducto: TipoProductoTcg;
  cantidad: number;
  orden: number;
  estado?: EstadoSubastaDetalle | null;
  pujaGanadoraId?: string | null;
  pedidoDigitalId?: string | null;
  pedidoCodigo?: string | null;
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
  modo?: ModoSubastaTcg | null;
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
  pujas: Array<PujaTcg & { subastaDetalleId?: string | null }>;
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

  async refrescar(rango?: SubastasTcgRangoFechas): Promise<SubastaTcg[]> {
    try {
      let params = new HttpParams();
      const desde = rango?.desde?.trim();
      const hasta = rango?.hasta?.trim();
      if (desde) {
        params = params.set('fechaDesde', desde);
      }
      if (hasta) {
        params = params.set('fechaHasta', hasta);
      }
      const items = await firstValueFrom(
        this.http.get<SubastaApi[]>(apiUrl('subastas-tcg'), { params }),
      );
      const subastas = items.map(mapSubasta);
      this.subastasSignal.set(subastas);
      return subastas;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  listar(filtros: SubastasTcgFiltros): SubastaTcg[] {
    const q = filtros.busqueda.trim().toLowerCase();
    const qNorm = q.replace(/^#/, '');
    const desde = filtros.desde ? Date.parse(`${filtros.desde}T00:00:00`) : null;
    const hasta = filtros.hasta ? Date.parse(`${filtros.hasta}T23:59:59.999`) : null;

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
        const fecha = new Date(subasta.fechaInicio).getTime();
        if (desde && fecha < desde) {
          return false;
        }
        if (hasta && fecha > hasta) {
          return false;
        }
        if (q) {
          const haystack = [
            subasta.codigo,
            subasta.titulo,
            subasta.productoNombre,
            subasta.codigoSku,
            ...(subasta.detalles ?? []).flatMap((d) => [
              d.productoNombre,
              d.tituloPersonalizado,
              d.codigoSku,
            ]),
            ...subasta.pujas.map((p) => p.nombrePostor),
          ]
            .filter(Boolean)
            .join(' ')
            .toLowerCase();
          const codigoNorm = subasta.codigo.toLowerCase().replace(/^#/, '');
          if (!haystack.includes(q) && !codigoNorm.includes(qNorm)) {
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
        tituloPersonalizado: d.tituloPersonalizado?.trim() || null,
      })),
      titulo: request.titulo.trim(),
      canal: request.canal,
      modo: request.modo ?? 'COMBO',
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
      subastaDetalleId: request.subastaDetalleId || null,
      monto: request.monto,
    });
  }

  cerrar(id: string): Promise<SubastaTcg> {
    return this.enviar('POST', apiUrl(`subastas-tcg/${id}/cerrar`));
  }

  declararDesierta(id: string, subastaDetalleId?: string | null): Promise<SubastaTcg> {
    return this.enviar('POST', apiUrl(`subastas-tcg/${id}/declarar-desierta`), {
      subastaDetalleId: subastaDetalleId || null,
    });
  }

  eliminarPuja(pujaId: string): Promise<SubastaTcg> {
    return this.enviar('DELETE', apiUrl(`subastas-tcg/pujas/${pujaId}`));
  }

  async adjudicar(id: string, checkout?: AdjudicarSubastaRequest): Promise<SubastaTcg> {
    const subasta = await this.enviar('POST', apiUrl(`subastas-tcg/${id}/adjudicar`), {
      subastaDetalleId: checkout?.subastaDetalleId ?? null,
      nombrePostor: checkout?.nombrePostor ?? null,
      clienteId: checkout?.clienteId ?? null,
      montoAdjudicado: checkout?.montoAdjudicado ?? null,
      metodoEnvio: checkout?.metodoEnvio ?? 'RECOJO_TIENDA',
      origenPagoPreferido: checkout?.origenPagoPreferido ?? 'YAPE',
      destinatarioNombre: checkout?.destinatarioNombre ?? null,
      destinatarioTelefono: checkout?.destinatarioTelefono ?? null,
      entregaDireccion: checkout?.entregaDireccion ?? null,
      entregaDistrito: checkout?.entregaDistrito ?? null,
      entregaProvincia: checkout?.entregaProvincia ?? null,
      entregaDepartamento: checkout?.entregaDepartamento ?? null,
      agencia: checkout?.agencia ?? null,
      puntoEntrega: checkout?.puntoEntrega ?? null,
      canalContacto: checkout?.canalContacto ?? null,
      contactoReferencia: checkout?.contactoReferencia ?? null,
      guardarPuntoEnCliente: checkout?.guardarPuntoEnCliente ?? false,
    });
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

  private async enviar(
    method: 'POST' | 'DELETE',
    url: string,
    body?: unknown,
  ): Promise<SubastaTcg> {
    try {
      const request$ =
        method === 'DELETE'
          ? this.http.delete<SubastaApi>(url)
          : this.http.post<SubastaApi>(url, body ?? {});
      const dto = await firstValueFrom(request$);
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
        tituloPersonalizado: d.tituloPersonalizado?.trim() || null,
        codigoSku: d.codigoSku,
        tipoProducto: d.tipoProducto,
        cantidad: d.cantidad,
        orden: d.orden,
        estado:
          d.estado === 'ADJUDICADO'
            ? 'ADJUDICADO'
            : d.estado === 'DESIERTA'
              ? 'DESIERTA'
              : 'PENDIENTE',
        pujaGanadoraId: d.pujaGanadoraId ?? null,
        pedidoDigitalId: d.pedidoDigitalId ?? null,
        pedidoCodigo:
          d.pedidoCodigo?.trim() || (d.pedidoDigitalId ? codigoPedido(d.pedidoDigitalId) : null),
      }));
  }
  return [
    {
      id: '',
      productoId: dto.productoId,
      productoNombre: dto.productoNombre,
      tituloPersonalizado: null,
      codigoSku: dto.codigoSku ?? '',
      tipoProducto: dto.tipoProducto,
      cantidad: 1,
      orden: 1,
      estado: 'PENDIENTE',
      pujaGanadoraId: null,
      pedidoDigitalId: null,
      pedidoCodigo: null,
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
    modo: dto.modo === 'INDIVIDUALES' ? 'INDIVIDUALES' : 'COMBO',
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
      subastaDetalleId: puja.subastaDetalleId ?? null,
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
