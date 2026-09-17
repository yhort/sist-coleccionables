import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { CajaAperturaDialogComponent } from '../../components/caja-apertura-dialog/caja-apertura-dialog.component';
import { CajaArqueoDialogComponent } from '../../components/caja-arqueo-dialog/caja-arqueo-dialog.component';
import { CajaMovimientoDialogComponent } from '../../components/caja-movimiento-dialog/caja-movimiento-dialog.component';
import { CajaTicketPrintComponent } from '../../components/caja-ticket-print/caja-ticket-print.component';
import { CajaApiService } from '../../data-access/caja.service';
import {
  CajaResumen,
  CajaTicketReporte,
  ETIQUETAS_DIFERENCIA_CAJA,
  ETIQUETAS_ESTADO_CAJA,
  ETIQUETAS_MOVIMIENTO_CAJA,
} from '../../models/caja.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-caja-page',
  imports: [
    SolesPipe,
    DatePipe,
    FormsModule,
    CajaAperturaDialogComponent,
    CajaArqueoDialogComponent,
    CajaMovimientoDialogComponent,
    CajaTicketPrintComponent,
  ],
  templateUrl: './caja-page.component.html',
  styleUrl: './caja-page.component.scss',
})
export class CajaPageComponent {
  private readonly cajaApi = inject(CajaApiService);

  readonly sedeId = signal('');
  readonly error = signal('');
  readonly dialog = signal<'apertura' | 'arqueo' | 'movimiento' | null>(null);
  readonly ticket = signal<CajaTicketReporte | null>(null);
  readonly resumen = signal<CajaResumen | null>(null);

  readonly sedes = this.cajaApi.sedes;
  readonly sesiones = this.cajaApi.sesiones;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_CAJA;
  readonly etiquetasMovimiento = ETIQUETAS_MOVIMIENTO_CAJA;
  readonly etiquetasDiferencia = ETIQUETAS_DIFERENCIA_CAJA;

  readonly estado = computed(() => {
    this.cajaApi.estados();
    const id = this.sedeId();
    return id ? this.cajaApi.estadoDe(id) : null;
  });

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    try {
      await this.cajaApi.refrescarSedes();
      const actual = this.sedeId() || this.sedes()[0]?.id || '';
      if (actual && actual !== this.sedeId()) {
        this.sedeId.set(actual);
      }
      if (!this.sedeId()) {
        return;
      }
      await Promise.all([this.cajaApi.refrescarEstado(this.sedeId()), this.cajaApi.listar(this.sedeId())]);
      await this.cargarResumen();
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  async onSede(sedeId: string): Promise<void> {
    this.sedeId.set(sedeId);
    this.resumen.set(null);
    this.error.set('');
    try {
      await Promise.all([this.cajaApi.refrescarEstado(sedeId), this.cajaApi.listar(sedeId)]);
      await this.cargarResumen();
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  async cargarResumen(): Promise<void> {
    const estado = this.cajaApi.estadoDe(this.sedeId());
    if (!estado?.abierta) {
      this.resumen.set(null);
      return;
    }
    try {
      this.resumen.set(await this.cajaApi.resumenActual(this.sedeId()));
    } catch {
      this.resumen.set(estado.resumen);
    }
  }

  onGuardado(): void {
    this.dialog.set(null);
    void this.cargar();
  }

  onArqueoGuardado(): void {
    void this.cargar();
  }

  async imprimir(sesionId: string): Promise<void> {
    this.error.set('');
    try {
      this.ticket.set(await this.cajaApi.reporteTicket(sesionId));
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }
}
