import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { SedesApiService } from '../../../core/data-access/sedes-api.service';
import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import {
  AbrirCajaRequest,
  CajaEstadoActual,
  CajaMovimiento,
  CajaResumen,
  CajaSesion,
  CajaTicketReporte,
  CerrarCajaRequest,
  RegistrarCajaMovimientoRequest,
} from '../models/caja.model';

@Injectable({ providedIn: 'root' })
export class CajaApiService {
  private readonly http = inject(HttpClient);
  private readonly sedesApi = inject(SedesApiService);
  private readonly estadoPorSede = signal<Record<string, CajaEstadoActual>>({});
  private readonly sesionesSignal = signal<CajaSesion[]>([]);

  readonly sedes = this.sedesApi.sedes;
  readonly sesiones = this.sesionesSignal.asReadonly();
  readonly estados = this.estadoPorSede.asReadonly();

  readonly hayTurnoAbierto = computed(() =>
    Object.values(this.estadoPorSede()).some((estado) => estado.abierta),
  );

  estadoDe(sedeId: string): CajaEstadoActual | null {
    return this.estadoPorSede()[sedeId] ?? null;
  }

  estaAbierta(sedeId: string): boolean {
    return this.estadoDe(sedeId)?.abierta === true;
  }

  mensajeCerrada(sedeId: string): string {
    return (
      this.estadoDe(sedeId)?.mensaje ??
      'La caja de esta sede está cerrada. Abre el turno antes de confirmar ventas.'
    );
  }

  async refrescarSedes(): Promise<void> {
    await this.sedesApi.refrescar();
  }

  async refrescarEstado(sedeId: string): Promise<CajaEstadoActual> {
    try {
      const estado = await firstValueFrom(
        this.http.get<CajaEstadoActual>(apiUrl('caja/sesion-actual'), { params: { sedeId } }),
      );
      this.estadoPorSede.update((actual) => ({ ...actual, [sedeId]: estado }));
      return estado;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async refrescarEstados(sedeIds?: readonly string[]): Promise<void> {
    const ids = sedeIds?.length ? sedeIds : this.sedesApi.sedes().map((sede) => sede.id);
    await Promise.all(ids.map((id) => this.refrescarEstado(id).catch(() => undefined)));
  }

  async listar(sedeId?: string): Promise<CajaSesion[]> {
    try {
      const params = sedeId ? { sedeId } : undefined;
      const items = await firstValueFrom(
        this.http.get<CajaSesion[]>(apiUrl('caja'), { params }),
      );
      this.sesionesSignal.set(items);
      return items;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async resumenActual(sedeId: string): Promise<CajaResumen> {
    try {
      return await firstValueFrom(
        this.http.get<CajaResumen>(apiUrl('caja/resumen-actual'), { params: { sedeId } }),
      );
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async abrir(request: AbrirCajaRequest): Promise<CajaSesion> {
    try {
      const sesion = await firstValueFrom(
        this.http.post<CajaSesion>(apiUrl('caja/apertura'), request),
      );
      await this.refrescarEstado(request.sedeId);
      await this.listar(request.sedeId).catch(() => undefined);
      return sesion;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async cerrar(request: CerrarCajaRequest): Promise<CajaResumen> {
    try {
      const resumen = await firstValueFrom(
        this.http.post<CajaResumen>(apiUrl('caja/cierre'), request),
      );
      const sedeId = request.sedeId ?? resumen.sedeId;
      if (sedeId) {
        await this.refrescarEstado(sedeId);
        await this.listar(sedeId).catch(() => undefined);
      }
      return resumen;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async registrarMovimiento(request: RegistrarCajaMovimientoRequest): Promise<CajaMovimiento> {
    try {
      const movimiento = await firstValueFrom(
        this.http.post<CajaMovimiento>(apiUrl('caja/movimientos'), request),
      );
      const sedeId = request.sedeId;
      if (sedeId) {
        await this.refrescarEstado(sedeId).catch(() => undefined);
      }
      return movimiento;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async reporteTicket(id: string): Promise<CajaTicketReporte> {
    try {
      return await firstValueFrom(
        this.http.get<CajaTicketReporte>(apiUrl(`caja/${id}/reporte-ticket`)),
      );
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }
}
