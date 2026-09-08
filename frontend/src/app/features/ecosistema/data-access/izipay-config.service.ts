import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import {
  GuardarIntegracionIzipay,
  IntegracionIzipay,
} from '../models/izipay-config.model';

@Injectable({ providedIn: 'root' })
export class IzipayConfigApiService {
  private readonly http = inject(HttpClient);
  private readonly configSignal = signal<IntegracionIzipay | null>(null);

  readonly config = this.configSignal.asReadonly();

  async cargar(): Promise<IntegracionIzipay | null> {
    try {
      const dto = await firstValueFrom(
        this.http.get<IntegracionIzipay>(apiUrl('pagos/izipay/config')),
      );
      this.configSignal.set(dto);
      return dto;
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) {
        this.configSignal.set(null);
        return null;
      }
      throw new Error(readApiError(err));
    }
  }

  async guardar(request: GuardarIntegracionIzipay): Promise<IntegracionIzipay> {
    try {
      const dto = await firstValueFrom(
        this.http.put<IntegracionIzipay>(apiUrl('pagos/izipay/config'), request),
      );
      this.configSignal.set(dto);
      return dto;
    } catch (err) {
      throw new Error(readApiError(err));
    }
  }
}
