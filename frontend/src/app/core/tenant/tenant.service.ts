import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../http/api-url';
import {
  TENANT_SETTINGS_TRUNQI_DEFAULT,
  TenantFeatureName,
  TenantSettings,
  featureEnabled,
} from './tenant-settings.model';

@Injectable({ providedIn: 'root' })
export class TenantService {
  private readonly http = inject(HttpClient);
  private readonly settingsSignal = signal<TenantSettings | null>(null);

  readonly settings = this.settingsSignal.asReadonly();
  readonly nombreComercial = computed(
    () => this.settingsSignal()?.nombreComercial ?? TENANT_SETTINGS_TRUNQI_DEFAULT.nombreComercial,
  );

  setSettings(settings: TenantSettings | null): void {
    this.settingsSignal.set(settings);
  }

  clear(): void {
    this.settingsSignal.set(null);
  }

  hasFeature(feature: TenantFeatureName): boolean {
    return featureEnabled(this.settingsSignal(), feature);
  }

  async refreshFromApi(): Promise<TenantSettings> {
    const settings = await firstValueFrom(
      this.http.get<TenantSettings>(apiUrl('auth/tenant-settings')),
    );
    this.settingsSignal.set(settings);
    return settings;
  }
}
