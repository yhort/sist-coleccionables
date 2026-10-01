import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { FiltroActivoMaestro, Proveedor, UpsertProveedorRequest } from '../models/proveedor.model';

@Injectable({ providedIn: 'root' })
export class ProveedoresApiService {
  private readonly http = inject(HttpClient);
  private readonly proveedoresSignal = signal<Proveedor[]>([]);

  readonly proveedores = this.proveedoresSignal.asReadonly();

  async refrescar(q = '', filtro: FiltroActivoMaestro = 'activos'): Promise<Proveedor[]> {
    try {
      const params = paramsDeFiltro(q, filtro);
      const items = await firstValueFrom(
        this.http.get<Proveedor[]>(apiUrl('proveedores'), { params }),
      );
      this.proveedoresSignal.set(items);
      return items;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async guardar(request: UpsertProveedorRequest, id?: string | null): Promise<Proveedor> {
    try {
      const proveedor = id
        ? await firstValueFrom(this.http.put<Proveedor>(apiUrl(`proveedores/${id}`), request))
        : await firstValueFrom(this.http.post<Proveedor>(apiUrl('proveedores'), request));
      this.upsert(proveedor);
      return proveedor;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async desactivar(id: string): Promise<Proveedor> {
    try {
      const proveedor = await firstValueFrom(
        this.http.post<Proveedor>(apiUrl(`proveedores/${id}/desactivar`), {}),
      );
      this.upsert(proveedor);
      return proveedor;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async reactivar(id: string): Promise<Proveedor> {
    try {
      const proveedor = await firstValueFrom(
        this.http.post<Proveedor>(apiUrl(`proveedores/${id}/reactivar`), {}),
      );
      this.upsert(proveedor);
      return proveedor;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private upsert(proveedor: Proveedor): void {
    this.proveedoresSignal.update((items) => {
      const resto = items.filter((item) => item.id !== proveedor.id);
      return [proveedor, ...resto].sort((a, b) => a.razonSocial.localeCompare(b.razonSocial, 'es'));
    });
  }
}

function paramsDeFiltro(q: string, filtro: FiltroActivoMaestro): Record<string, string> {
  const params: Record<string, string> = {};
  if (q.trim()) {
    params['q'] = q.trim();
  }
  if (filtro === 'activos') {
    params['activo'] = 'true';
  } else if (filtro === 'inactivos') {
    params['activo'] = 'false';
  }
  return params;
}
