import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { SedeInventario } from '../../features/inventario/models/inventario.model';
import { apiUrl } from '../http/api-url';

interface SedeApi {
  id: string;
  nombre: string;
  tipo: 'TIENDA' | 'ALMACEN';
  activa: boolean;
}

@Injectable({ providedIn: 'root' })
export class SedesApiService {
  private readonly http = inject(HttpClient);
  private readonly sedesSignal = signal<SedeInventario[]>([]);

  readonly sedes = this.sedesSignal.asReadonly();

  async refrescar(): Promise<SedeInventario[]> {
    // El API ya filtra activas por defecto; reforzamos el filtro en cliente.
    const items = await firstValueFrom(
      this.http.get<SedeApi[]>(apiUrl('sedes'), { params: { activa: 'true' } }),
    );
    const sedes = items
      .filter((item) => item.activa)
      .map((item) => ({
        id: item.id,
        nombre: item.nombre,
        tipo: item.tipo,
      }));
    this.sedesSignal.set(sedes);
    return sedes;
  }
}
