import { Component, computed, inject, signal } from '@angular/core';

import { readApiError } from '../../../../core/http/api-error';
import { CajaTurnoBannerComponent } from '../../../caja/components/caja-turno-banner/caja-turno-banner.component';
import { CajaApiService } from '../../../caja/data-access/caja.service';
import { EntregasFiltersComponent } from '../../components/entregas-filters/entregas-filters.component';
import { EntregasKpisComponent } from '../../components/entregas-kpis/entregas-kpis.component';
import { EntregasTableComponent } from '../../components/entregas-table/entregas-table.component';
import { PackingSlipDialogComponent } from '../../components/packing-slip-dialog/packing-slip-dialog.component';
import { ProgramarEntregaDialogComponent } from '../../components/programar-entrega-dialog/programar-entrega-dialog.component';
import { EntregasApiService } from '../../data-access/entregas.service';
import {
  EntregaFila,
  EntregasFiltros,
  FILTROS_ENTREGAS_VACIOS,
  crearFiltrosEntregasDiaActual,
} from '../../models/entrega.model';

@Component({
  selector: 'app-entregas-page',
  imports: [
    EntregasFiltersComponent,
    EntregasKpisComponent,
    EntregasTableComponent,
    PackingSlipDialogComponent,
    ProgramarEntregaDialogComponent,
    CajaTurnoBannerComponent,
  ],
  templateUrl: './entregas-page.component.html',
  styleUrl: './entregas-page.component.scss',
})
export class EntregasPageComponent {
  private readonly entregasApi = inject(EntregasApiService);
  private readonly cajaApi = inject(CajaApiService);

  readonly sedes = this.entregasApi.sedes;
  readonly filtros = signal<EntregasFiltros>(crearFiltrosEntregasDiaActual());
  readonly error = signal('');
  readonly dialogModo = signal<'empaque' | 'despacho' | null>(null);
  readonly filaActiva = signal<EntregaFila | null>(null);
  readonly etiquetaAbierta = signal(false);
  readonly sedeCajaId = computed(() => this.sedes()[0]?.id ?? '');

  readonly filas = computed(() => {
    this.entregasApi.entregas();
    this.entregasApi.sedes();
    return this.entregasApi.listar(this.filtros());
  });

  readonly kpis = computed(() => {
    this.entregasApi.entregas();
    this.entregasApi.sedes();
    return this.entregasApi.kpis(this.filas());
  });

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    try {
      await this.refrescarEntregas();
      await this.cajaApi.refrescarEstados(this.sedes().map((sede) => sede.id));
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  actualizarFiltros(filtros: EntregasFiltros): void {
    const prev = this.filtros();
    this.filtros.set(filtros);
    if (prev.desde !== filtros.desde || prev.hasta !== filtros.hasta) {
      void this.refrescarEntregas().catch((err) => this.error.set(readApiError(err)));
    }
  }

  limpiarFiltros(): void {
    this.filtros.set({ ...FILTROS_ENTREGAS_VACIOS });
    void this.refrescarEntregas().catch((err) => this.error.set(readApiError(err)));
  }

  abrirEmpaque(fila: EntregaFila): void {
    this.error.set('');
    this.etiquetaAbierta.set(false);
    this.filaActiva.set(fila);
    this.dialogModo.set('empaque');
  }

  abrirDespacho(fila: EntregaFila): void {
    this.error.set('');
    this.etiquetaAbierta.set(false);
    this.filaActiva.set(fila);
    this.dialogModo.set('despacho');
  }

  abrirEtiqueta(fila: EntregaFila): void {
    this.error.set('');
    this.filaActiva.set(fila);
    this.etiquetaAbierta.set(true);
  }

  cerrarDialog(): void {
    this.dialogModo.set(null);
    this.filaActiva.set(null);
    this.etiquetaAbierta.set(false);
  }

  onGuardado(): void {
    this.cerrarDialog();
  }

  async confirmar(fila: EntregaFila): Promise<void> {
    this.error.set('');
    try {
      await this.cajaApi.refrescarEstado(fila.pedido.sedeId);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo consultar la caja.');
      return;
    }
    if (!this.cajaApi.estaAbierta(fila.pedido.sedeId)) {
      this.error.set(this.cajaApi.mensajeCerrada(fila.pedido.sedeId));
      return;
    }
    const recojo = fila.entrega.metodoEnvio === 'RECOJO_TIENDA';
    const mensaje = recojo
      ? '¿Confirmar el recojo en tienda? Se crea la venta y el kardex VENTA.'
      : '¿Confirmar la entrega? El pedido pasa a Entregado y se confirma la reserva en inventario.';
    if (!globalThis.confirm(mensaje)) {
      return;
    }
    try {
      await this.entregasApi.confirmar(fila.pedido.id);
      await this.cajaApi.refrescarEstado(fila.pedido.sedeId).catch(() => undefined);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo confirmar la entrega.');
    }
  }

  onCajaChanged(): void {
    void this.cajaApi.refrescarEstados(this.sedes().map((sede) => sede.id));
  }

  private async refrescarEntregas(): Promise<void> {
    const { desde, hasta } = this.filtros();
    await this.entregasApi.refrescar({
      desde: desde || null,
      hasta: hasta || null,
    });
  }
}
