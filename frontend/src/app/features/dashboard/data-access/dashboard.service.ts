import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { DashboardResumen } from '../models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly http = inject(HttpClient);
  private readonly resumenSignal = signal<DashboardResumen | null>(null);

  readonly resumen = this.resumenSignal.asReadonly();

  async refrescar(): Promise<DashboardResumen> {
    try {
      const resumen = await firstValueFrom(
        this.http.get<DashboardResumen>(apiUrl('dashboard/resumen')),
      );
      this.resumenSignal.set(resumen);
      return resumen;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }
}
