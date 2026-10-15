import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { PedidoDigital } from '../../pedidos-digitales/models/pedido-digital.model';
import { CobrarYEmitirPosRequest, CobrarYEmitirPosResponse } from '../models/pos.model';

@Injectable({ providedIn: 'root' })
export class PosApiService {
  private readonly http = inject(HttpClient);

  async pedidosPendientes(q?: string, sedeId?: string): Promise<PedidoDigital[]> {
    try {
      let params = new HttpParams();
      const termino = q?.trim();
      if (termino) {
        params = params.set('q', termino);
      }
      if (sedeId) {
        params = params.set('sedeId', sedeId);
      }
      return await firstValueFrom(
        this.http.get<PedidoDigital[]>(apiUrl('pos/pedidos-pendientes'), { params }),
      );
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async cobrarYEmitir(request: CobrarYEmitirPosRequest): Promise<CobrarYEmitirPosResponse> {
    try {
      return await firstValueFrom(
        this.http.post<CobrarYEmitirPosResponse>(apiUrl('pos/cobrar-y-emitir'), request),
      );
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }
}
