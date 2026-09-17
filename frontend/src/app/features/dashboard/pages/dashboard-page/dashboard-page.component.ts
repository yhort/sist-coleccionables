import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthSessionService } from '../../../../core/auth/auth-session.service';
import { readApiError } from '../../../../core/http/api-error';
import { DashboardApiService } from '../../data-access/dashboard.service';
import {
  claseEstadoActividad,
  formatearFechaOperativa,
} from '../../models/dashboard.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-dashboard-page',
  imports: [SolesPipe, DatePipe, DecimalPipe, RouterLink],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
})
export class DashboardPageComponent {
  private readonly api = inject(DashboardApiService);
  private readonly auth = inject(AuthSessionService);

  readonly cargando = signal(true);
  readonly error = signal('');
  readonly resumen = this.api.resumen;
  readonly claseEstado = claseEstadoActividad;

  readonly saludo = computed(() => {
    const nombre = this.auth.usuario()?.nombre?.trim() || 'equipo';
    const primero = nombre.split(/\s+/)[0];
    return `Hola, ${primero}`;
  });

  readonly fechaOperativa = computed(() => {
    const fecha = this.resumen()?.fechaLocal;
    return fecha ? formatearFechaOperativa(fecha) : '';
  });

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set('');
    try {
      await this.api.refrescar();
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.cargando.set(false);
    }
  }
}
