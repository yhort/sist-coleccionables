import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { Cliente, UpsertClienteRequest } from '../models/cliente.model';

@Injectable({ providedIn: 'root' })
export class ClientesApiService {
  private readonly http = inject(HttpClient);
  private readonly clientesSignal = signal<Cliente[]>([]);

  readonly clientes = this.clientesSignal.asReadonly();

  async refrescar(q = '', soloActivos = true): Promise<Cliente[]> {
    try {
      const params: Record<string, string> = {};
      if (q.trim()) {
        params['q'] = q.trim();
      }
      if (soloActivos) {
        params['activo'] = 'true';
      }
      const items = await firstValueFrom(this.http.get<Cliente[]>(apiUrl('clientes'), { params }));
      this.clientesSignal.set(items);
      return items;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async buscar(q: string, soloActivos = true): Promise<Cliente[]> {
    try {
      const params: Record<string, string> = {};
      if (q.trim()) {
        params['q'] = q.trim();
      }
      if (soloActivos) {
        params['activo'] = 'true';
      }
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
      this.clientesSignal.update((items) => {
        const resto = items.filter((item) => item.id !== cliente.id);
        return cliente.activo ? [cliente, ...resto] : resto;
      });
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
      this.clientesSignal.update((items) => items.filter((item) => item.id !== id));
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
      this.clientesSignal.update((items) => {
        const resto = items.filter((item) => item.id !== cliente.id);
        return [cliente, ...resto];
      });
      return cliente;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }
}
