import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';

export interface CompraDetalleInput {
  productoId: string;
  cantidad: number;
  costoUnitario: number;
}

export interface CompraResponse {
  id: string;
  proveedorId: string;
  proveedorRuc: string;
  proveedorRazonSocial: string;
  sedeId: string;
  sedeNombre: string;
  fecha: string;
  observacion: string | null;
  total: number;
}

@Injectable({ providedIn: 'root' })
export class ComprasApiService {
  private readonly http = inject(HttpClient);

  async crear(request: {
    proveedorId: string;
    sedeId: string;
    observacion?: string | null;
    detalles: CompraDetalleInput[];
  }): Promise<CompraResponse> {
    try {
      return await firstValueFrom(this.http.post<CompraResponse>(apiUrl('inventario/compras'), request));
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }
}
