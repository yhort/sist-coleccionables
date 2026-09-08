import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { TipoProductoTcg } from '../../productos-tcg/models/producto-tcg.model';
import {
  KardexFila,
  KardexFiltros,
  MovimientoInventario,
  ReferenciaMovimiento,
  TipoMovimientoInventario,
} from '../models/inventario.model';
import { InventarioStore } from './inventario-store.service';

interface KardexApi {
  id: string;
  sedeId: string;
  sedeNombre: string;
  productoId: string;
  productoNombre: string;
  codigoSku: string;
  tipoProducto: TipoProductoTcg;
  tipoMovimiento: TipoMovimientoInventario;
  cantidad: number;
  stockAnterior: number;
  stockPosterior: number;
  reservadoAnterior: number;
  reservadoPosterior: number;
  referenciaTipo?: string | null;
  referenciaId?: string | null;
  motivo?: string | null;
  usuarioNombre?: string | null;
  fechaCreacion: string;
}

@Injectable({ providedIn: 'root' })
export class KardexApiService {
  private readonly http = inject(HttpClient);
  private readonly store = inject(InventarioStore);
  private readonly filasSignal = signal<KardexFila[]>([]);

  readonly filas = this.filasSignal.asReadonly();

  async refrescar(filtros: KardexFiltros): Promise<KardexFila[]> {
    let params = new HttpParams();
    if (filtros.sedeId !== 'TODAS') {
      params = params.set('sedeId', filtros.sedeId);
    }
    if (filtros.productoId !== 'TODOS') {
      params = params.set('productoId', filtros.productoId);
    }
    if (filtros.tipoMovimiento !== 'TODOS') {
      params = params.set('tipoMovimiento', filtros.tipoMovimiento);
    }
    if (filtros.desde) {
      params = params.set('desde', `${filtros.desde}T00:00:00-05:00`);
    }
    if (filtros.hasta) {
      params = params.set('hasta', `${filtros.hasta}T23:59:59-05:00`);
    }

    try {
      const items = await firstValueFrom(
        this.http.get<KardexApi[]>(apiUrl('inventario/kardex'), { params }),
      );
      const filas = items.map(mapKardexApi).filter((fila) => coincideBusqueda(fila, filtros.busqueda));
      this.filasSignal.set(filas);
      return filas;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  listar(filtros: KardexFiltros): KardexFila[] {
    const api = this.filasSignal();
    if (api.length > 0 || filtros.sedeId !== 'TODAS' || filtros.productoId !== 'TODOS') {
      return api.filter((fila) => coincideBusqueda(fila, filtros.busqueda));
    }

    const query = filtros.busqueda.trim().toLowerCase();
    const desde = filtros.desde ? Date.parse(`${filtros.desde}T00:00:00`) : null;
    const hasta = filtros.hasta ? Date.parse(`${filtros.hasta}T23:59:59.999`) : null;

    return this.store
      .movimientos()
      .map((movimiento) => this.toFilaLocal(movimiento))
      .filter((fila) => {
        if (filtros.sedeId !== 'TODAS' && fila.sedeId !== filtros.sedeId) {
          return false;
        }
        if (filtros.productoId !== 'TODOS' && fila.productoId !== filtros.productoId) {
          return false;
        }
        if (filtros.tipoMovimiento !== 'TODOS' && fila.tipoMovimiento !== filtros.tipoMovimiento) {
          return false;
        }
        const fecha = new Date(fila.fechaCreacion).getTime();
        if (desde && fecha < desde) {
          return false;
        }
        if (hasta && fecha > hasta) {
          return false;
        }
        if (!query) {
          return true;
        }
        return `${fila.productoNombre} ${fila.codigoSku} ${fila.motivo} ${fila.usuario}`
          .toLowerCase()
          .includes(query);
      });
  }

  private toFilaLocal(movimiento: MovimientoInventario): KardexFila {
    return {
      ...movimiento,
      productoNombre: 'Producto',
      codigoSku: '—',
      tipoProducto: 'ACCESORIO',
      sedeNombre: movimiento.sedeId,
    };
  }
}

function mapKardexApi(dto: KardexApi): KardexFila {
  return {
    id: dto.id,
    sedeId: dto.sedeId,
    productoId: dto.productoId,
    tipoMovimiento: dto.tipoMovimiento,
    cantidad: dto.cantidad,
    stockAnterior: dto.stockAnterior,
    stockPosterior: dto.stockPosterior,
    reservadoAnterior: dto.reservadoAnterior,
    reservadoPosterior: dto.reservadoPosterior,
    referenciaTipo: (dto.referenciaTipo as ReferenciaMovimiento) ?? 'AJUSTE_MANUAL',
    referenciaId: dto.referenciaId ?? null,
    motivo: dto.motivo ?? '',
    usuario: dto.usuarioNombre ?? '',
    fechaCreacion: dto.fechaCreacion,
    productoNombre: dto.productoNombre,
    codigoSku: dto.codigoSku,
    tipoProducto: dto.tipoProducto,
    sedeNombre: dto.sedeNombre,
  };
}

function coincideBusqueda(fila: KardexFila, busqueda: string): boolean {
  const query = busqueda.trim().toLowerCase();
  if (!query) {
    return true;
  }
  return `${fila.productoNombre} ${fila.codigoSku} ${fila.motivo} ${fila.usuario}`
    .toLowerCase()
    .includes(query);
}
