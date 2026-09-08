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

  async refrescar(q = ''): Promise<Cliente[]> {
    try {
      const items = await firstValueFrom(
        this.http.get<Cliente[]>(apiUrl('clientes'), { params: q.trim() ? { q: q.trim() } : {} }),
      );
      this.clientesSignal.set(items);
      return items;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async buscar(q: string): Promise<Cliente[]> {
    try {
      return await firstValueFrom(
        this.http.get<Cliente[]>(apiUrl('clientes'), { params: q.trim() ? { q: q.trim() } : {} }),
      );
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
        return [cliente, ...resto];
      });
      return cliente;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }
}
