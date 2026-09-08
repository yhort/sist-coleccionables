import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { AuthSessionService } from '../../../../core/auth/auth-session.service';
import { environment } from '../../../../../environments/environment';
import { IzipayConfigApiService } from '../../data-access/izipay-config.service';
import { ModoIzipay, urlNotificacionIpnIzipay } from '../../models/izipay-config.model';

@Component({
  selector: 'app-izipay-config-panel',
  imports: [ReactiveFormsModule],
  templateUrl: './izipay-config-panel.component.html',
  styleUrl: './izipay-config-panel.component.scss',
})
export class IzipayConfigPanelComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthSessionService);
  readonly api = inject(IzipayConfigApiService);

  readonly aviso = signal('');
  readonly error = signal('');
  readonly copiado = signal(false);
  readonly cargando = signal(true);
  readonly modos: { valor: ModoIzipay; etiqueta: string }[] = [
    { valor: 'TEST', etiqueta: 'TEST' },
    { valor: 'PROD', etiqueta: 'PROD' },
  ];

  readonly form = this.fb.nonNullable.group({
    shopId: ['', [Validators.required, Validators.maxLength(32)]],
    hmacSha256Clave: [''],
    modo: ['TEST' as ModoIzipay, Validators.required],
    activa: [true],
  });

  readonly urlIpn = computed(() => {
    const empresaId = this.auth.empresaId();
    if (!empresaId) {
      return '';
    }
    const origin = typeof globalThis.location === 'undefined' ? '' : globalThis.location.origin;
    return urlNotificacionIpnIzipay(empresaId, environment.apiBaseUrl, origin);
  });

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    this.cargando.set(true);
    try {
      const config = await this.api.cargar();
      if (config) {
        this.form.patchValue({
          shopId: config.shopId,
          hmacSha256Clave: '',
          modo: config.modo,
          activa: config.activa,
        });
      }
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo cargar Izipay.');
    } finally {
      this.cargando.set(false);
    }
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    this.aviso.set('');
    if (this.form.invalid) {
      return;
    }

    const raw = this.form.getRawValue();
    const clave = raw.hmacSha256Clave.trim();
    if (!this.api.config()?.tieneHmac && !clave) {
      this.error.set('Indica la clave HMAC-SHA-256 del Back Office Izipay.');
      return;
    }

    try {
      await this.api.guardar({
        shopId: raw.shopId.trim(),
        hmacSha256Clave: clave || undefined,
        modo: raw.modo,
        activa: raw.activa,
      });
      this.form.patchValue({ hmacSha256Clave: '' });
      this.aviso.set('Configuración Izipay guardada. Los IPN válidos llegarán a la bandeja de pagos.');
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar Izipay.');
    }
  }

  async copiarUrl(): Promise<void> {
    const url = this.urlIpn();
    if (!url) {
      this.error.set('No hay empresa activa para armar la URL de IPN.');
      return;
    }
    this.error.set('');
    try {
      const ok = await this.escribirPortapapeles(url);
      if (!ok) {
        throw new Error('clipboard');
      }
      this.aviso.set('URL de notificación IPN copiada.');
      this.copiado.set(true);
      globalThis.setTimeout(() => this.copiado.set(false), 2500);
    } catch {
      this.error.set('No se pudo copiar la URL. Selecciónala y copia manualmente.');
    }
  }

  private async escribirPortapapeles(texto: string): Promise<boolean> {
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(texto);
        return true;
      }
    } catch {
      // Fallback para contextos sin permiso Clipboard API.
    }
    const area = document.createElement('textarea');
    area.value = texto;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.left = '-9999px';
    document.body.appendChild(area);
    area.select();
    const ok = document.execCommand('copy');
    area.remove();
    return ok;
  }
}
