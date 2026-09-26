import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { ResultadoPaginado } from '../../../shared/ui/tabla-paginacion/paginacion';
import { etiquetaJuego, EliminarProductoTcgResultado, MetadatosWooCommerce, ProductoTcg, ProductosTcgFiltros, estadoStockDe } from '../models/producto-tcg.model';
import { EliminarProductoTcgApi, ProductoTcgApi, mapProductoFromApi, mapProductoToUpsert } from './producto-tcg.mapper';
import { CrearVarianteProductoCartaRequest } from '../../catalogo-tcg/models/catalogo-tcg.model';

@Injectable({ providedIn: 'root' })
export class ProductosTcgApiService {
  private readonly http = inject(HttpClient);
  private readonly productosSignal = signal<ProductoTcg[]>([]);

  readonly productos = this.productosSignal.asReadonly();
  readonly total = computed(() => this.productosSignal().length);

  async refrescar(): Promise<ProductoTcg[]> {
    try {
      const result = await firstValueFrom(
        this.http.get<ResultadoPaginado<ProductoTcgApi>>(apiUrl('productos-tcg')),
      );
      const mapeados = result.items.map((item) => {
        const previo = this.productosSignal().find((p) => p.id === item.id);
        return mapProductoFromApi(item, previo?.stockLocal ?? 0);
      });
      this.productosSignal.set(mapeados);
      return mapeados;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async consultarPagina(
    filtros: ProductosTcgFiltros,
    page: number,
    pageSize: number,
  ): Promise<ResultadoPaginado<ProductoTcg>> {
    try {
      let params = new HttpParams()
        .set('page', String(page))
        .set('pageSize', String(pageSize));
      const busqueda = filtros.busqueda.trim();
      if (busqueda) {
        params = params.set('q', busqueda);
      }
      if (filtros.tipoProducto !== 'TODOS') {
        params = params.set('tipoProducto', filtros.tipoProducto);
      }
      if (filtros.juego !== 'TODOS') {
        params = params.set('juego', etiquetaJuego(filtros.juego) || filtros.juego);
      }
      if (filtros.estadoActivo === 'ACTIVOS') {
        params = params.set('activo', 'true');
      } else if (filtros.estadoActivo === 'INACTIVOS') {
        params = params.set('activo', 'false');
      }

      const result = await firstValueFrom(
        this.http.get<ResultadoPaginado<ProductoTcgApi>>(apiUrl('productos-tcg'), { params }),
      );
      const items = result.items.map((item) => {
        const previo = this.productosSignal().find((p) => p.id === item.id);
        return mapProductoFromApi(item, previo?.stockLocal ?? 0);
      });
      this.productosSignal.update((existentes) => {
        const porId = new Map(existentes.map((item) => [item.id, item]));
        for (const item of items) {
          porId.set(item.id, item);
        }
        return [...porId.values()];
      });
      return {
        items,
        total: result.total,
        page: result.page,
        pageSize: result.pageSize,
      };
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  aplicarStockLocal(porProducto: ReadonlyMap<string, number>): void {
    this.productosSignal.update((items) =>
      items.map((item) => {
        if (!porProducto.has(item.id)) {
          return item;
        }
        return {
          ...item,
          stockLocal: porProducto.get(item.id) ?? item.stockLocal,
        };
      }),
    );
  }

  listar(filtros: ProductosTcgFiltros): ProductoTcg[] {
    const busqueda = filtros.busqueda.trim().toLowerCase();

    return this.productosSignal().filter((producto) => {
      if (filtros.tipoProducto !== 'TODOS' && producto.tipoProducto !== filtros.tipoProducto) {
        return false;
      }
      if (filtros.juego !== 'TODOS' && producto.juego !== filtros.juego) {
        return false;
      }
      if (
        filtros.estadoStock !== 'TODOS' &&
        estadoStockDe(producto.stockLocal) !== filtros.estadoStock
      ) {
        return false;
      }
      if (
        filtros.sincronizacionWoo !== 'TODOS' &&
        producto.woo.estadoSincronizacion !== filtros.sincronizacionWoo
      ) {
        return false;
      }
      if (!busqueda) {
        return true;
      }

      const haystack = [
        producto.nombre,
        producto.codigoSku,
        producto.woo.sku,
        producto.woo.wooCommerceId?.toString() ?? '',
        producto.atributosTcg.setCodigo,
        producto.atributosTcg.numeroCarta,
        producto.woo.categoriasWoo.join(' '),
      ]
        .join(' ')
        .toLowerCase();

      return haystack.includes(busqueda);
    });
  }

  obtenerPorId(id: string): ProductoTcg | undefined {
    return this.productosSignal().find((producto) => producto.id === id);
  }

  skuExiste(sku: string, excluirId?: string): boolean {
    const normalizado = sku.trim().toLowerCase();
    return this.productosSignal().some(
      (producto) =>
        producto.id !== excluirId &&
        (producto.codigoSku.toLowerCase() === normalizado ||
          producto.woo.sku.toLowerCase() === normalizado),
    );
  }

  async crearVariante(request: CrearVarianteProductoCartaRequest): Promise<ProductoTcg> {
    try {
      const dto = await firstValueFrom(
        this.http.post<ProductoTcgApi>(apiUrl('productos-tcg/variantes'), request),
      );
      const persistido = mapProductoFromApi(dto, request.stockInicial);
      this.productosSignal.update((items) => {
        const hay = items.some((item) => item.id === persistido.id);
        return hay
          ? items.map((item) => (item.id === persistido.id ? persistido : item))
          : [persistido, ...items];
      });
      return persistido;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async guardar(
    producto: ProductoTcg,
    opciones?: { sedeId?: string | null; esAlta?: boolean },
  ): Promise<ProductoTcg> {
    const esAlta =
      opciones?.esAlta ?? !this.productosSignal().some((item) => item.id === producto.id);
    const body = mapProductoToUpsert(producto, {
      esAlta,
      sedeId: opciones?.sedeId,
    });
    try {
      const dto = esAlta
        ? await firstValueFrom(this.http.post<ProductoTcgApi>(apiUrl('productos-tcg'), body))
        : await firstValueFrom(
            this.http.put<ProductoTcgApi>(apiUrl(`productos-tcg/${producto.id}`), body),
          );
      const stockFallback = esAlta ? (body.stockInicial ?? producto.stockLocal ?? 0) : producto.stockLocal;
      const persistido = mapProductoFromApi(dto, stockFallback);
      persistido.woo = {
        ...persistido.woo,
        ...producto.woo,
        sku: persistido.codigoSku,
        precioNormal: persistido.woo.precioNormal || persistido.precioVenta,
        imagenes: persistido.woo.imagenes.length > 0 ? persistido.woo.imagenes : producto.woo.imagenes,
        wooCommerceId: persistido.woo.wooCommerceId ?? producto.woo.wooCommerceId,
        estadoSincronizacion:
          persistido.woo.estadoSincronizacion !== 'NO_MAPEADO'
            ? persistido.woo.estadoSincronizacion
            : producto.woo.estadoSincronizacion,
      };
      persistido.wooVinculado = persistido.woo.wooCommerceId != null;
      persistido.tieneDependencias = persistido.tieneDependencias || producto.tieneDependencias;
      this.productosSignal.update((items) => {
        const hay = items.some((item) => item.id === persistido.id);
        return hay
          ? items.map((item) => (item.id === persistido.id ? persistido : item))
          : [persistido, ...items];
      });
      return persistido;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  aplicarSyncWoo(productoId: string, patch: Partial<MetadatosWooCommerce>): ProductoTcg {
    const actual = this.obtenerPorId(productoId);
    if (!actual) {
      throw new Error('No se encontró el producto a sincronizar con WooCommerce.');
    }
    const persistido: ProductoTcg = {
      ...actual,
      woo: {
        ...actual.woo,
        ...patch,
      },
      wooVinculado: patch.wooCommerceId != null ? true : actual.wooVinculado,
    };
    this.productosSignal.update((items) =>
      items.map((item) => (item.id === persistido.id ? persistido : item)),
    );
    return persistido;
  }

  async desvincularWooCommerce(id: string): Promise<ProductoTcg> {
    try {
      const dto = await firstValueFrom(
        this.http.post<ProductoTcgApi>(apiUrl(`productos-tcg/${id}/desvincular-woocommerce`), {}),
      );
      const previo = this.obtenerPorId(id);
      const persistido = mapProductoFromApi(dto, previo?.stockLocal ?? 0);
      this.productosSignal.update((items) =>
        items.map((item) => (item.id === persistido.id ? persistido : item)),
      );
      return persistido;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async eliminarODesactivar(id: string): Promise<EliminarProductoTcgResultado> {
    try {
      const resultado = await firstValueFrom(
        this.http.delete<EliminarProductoTcgApi>(apiUrl(`productos-tcg/${id}`)),
      );
      if (resultado.accion === 'ELIMINADA') {
        this.productosSignal.update((items) => items.filter((item) => item.id !== id));
      } else if (resultado.producto) {
        const previo = this.obtenerPorId(id);
        const mapped = mapProductoFromApi(resultado.producto, previo?.stockLocal ?? 0);
        this.productosSignal.update((items) =>
          items.map((item) => (item.id === mapped.id ? mapped : item)),
        );
        return {
          id: resultado.id,
          accion: resultado.accion,
          motivo: resultado.motivo ?? null,
          producto: mapped,
        };
      } else {
        this.productosSignal.update((items) =>
          items.map((item) => (item.id === id ? { ...item, activo: false } : item)),
        );
      }
      return {
        id: resultado.id,
        accion: resultado.accion,
        motivo: resultado.motivo ?? null,
        producto: resultado.producto
          ? mapProductoFromApi(resultado.producto)
          : this.obtenerPorId(id) ?? null,
      };
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async activar(id: string): Promise<ProductoTcg> {
    try {
      const dto = await firstValueFrom(
        this.http.patch<ProductoTcgApi>(apiUrl(`productos-tcg/${id}/activar`), {}),
      );
      const previo = this.obtenerPorId(id);
      const persistido = mapProductoFromApi(dto, previo?.stockLocal ?? 0);
      this.productosSignal.update((items) =>
        items.map((item) => (item.id === persistido.id ? persistido : item)),
      );
      return persistido;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async desactivar(id: string): Promise<ProductoTcg> {
    try {
      const dto = await firstValueFrom(
        this.http.patch<ProductoTcgApi>(apiUrl(`productos-tcg/${id}/desactivar`), {}),
      );
      const previo = this.obtenerPorId(id);
      const persistido = mapProductoFromApi(dto, previo?.stockLocal ?? 0);
      this.productosSignal.update((items) =>
        items.map((item) => (item.id === persistido.id ? persistido : item)),
      );
      return persistido;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }
}
