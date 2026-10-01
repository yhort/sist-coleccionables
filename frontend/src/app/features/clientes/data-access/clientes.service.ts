import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { Cliente, FiltroActivoMaestro, UpsertClienteRequest } from '../models/cliente.model';

@Injectable({ providedIn: 'root' })
export class ClientesApiService {
  private readonly http = inject(HttpClient);
  private readonly clientesSignal = signal<Cliente[]>([]);

  readonly clientes = this.clientesSignal.asReadonly();

  async refrescar(q = '', filtro: FiltroActivoMaestro = 'activos'): Promise<Cliente[]> {
    try {
      const params = paramsDeFiltro(q, filtro);
      const items = await firstValueFrom(this.http.get<Cliente[]>(apiUrl('clientes'), { params }));
      this.clientesSignal.set(items);
      return items;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  /** Búsqueda operativa (pedidos/subastas): solo activos. */
  async buscar(q: string, soloActivos = true): Promise<Cliente[]> {
    try {
      const params = paramsDeFiltro(q, soloActivos ? 'activos' : 'todos');
      return await firstValueFrom(this.http.get<Cliente[]>(apiUrl('clientes'), { params }));
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async obtener(id: string): Promise<Cliente | null> {
    try {
      return await firstValueFrom(this.http.get<Cliente>(apiUrl(`clientes/${id}`)));
    } catch {
      return null;
    }
  }

  async guardar(request: UpsertClienteRequest, id?: string | null): Promise<Cliente> {
    try {
      const cliente = id
        ? await firstValueFrom(this.http.put<Cliente>(apiUrl(`clientes/${id}`), request))
        : await firstValueFrom(this.http.post<Cliente>(apiUrl('clientes'), request));
      this.upsert(cliente);
      return cliente;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async desactivar(id: string): Promise<Cliente> {
    try {
      const cliente = await firstValueFrom(
        this.http.post<Cliente>(apiUrl(`clientes/${id}/desactivar`), {}),
      );
      this.upsert(cliente);
      return cliente;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async reactivar(id: string): Promise<Cliente> {
    try {
      const cliente = await firstValueFrom(
        this.http.post<Cliente>(apiUrl(`clientes/${id}/reactivar`), {}),
      );
      this.upsert(cliente);
      return cliente;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private upsert(cliente: Cliente): void {
    this.clientesSignal.update((items) => {
      const resto = items.filter((item) => item.id !== cliente.id);
      return [cliente, ...resto];
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
