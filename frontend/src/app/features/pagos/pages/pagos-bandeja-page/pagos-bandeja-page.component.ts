import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { readApiError } from '../../../../core/http/api-error';
import { AsociarPagoDialogComponent } from '../../components/asociar-pago-dialog/asociar-pago-dialog.component';
import { PagosFiltersComponent } from '../../components/pagos-filters/pagos-filters.component';
import { PagosKpisComponent } from '../../components/pagos-kpis/pagos-kpis.component';
import { PagoFila, PagosTableComponent } from '../../components/pagos-table/pagos-table.component';
import { PagosApiService } from '../../data-access/pagos.service';
import {
  ESTADOS_PAGO,
  EstadoPago,
  FILTROS_PAGOS_VACIOS,
  Pago,
  PagosFiltros,
} from '../../models/pago.model';

@Component({
  selector: 'app-pagos-bandeja-page',
  imports: [
    AsociarPagoDialogComponent,
    PagosFiltersComponent,
    PagosKpisComponent,
    PagosTableComponent,
  ],
  templateUrl: './pagos-bandeja-page.component.html',
  styleUrl: './pagos-bandeja-page.component.scss',
})
export class PagosBandejaPageComponent implements OnInit {
  private readonly pagosApi = inject(PagosApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly filtros = signal<PagosFiltros>({ ...FILTROS_PAGOS_VACIOS });
  readonly dialogAbierto = signal(false);
  readonly pagoEnCurso = signal<Pago | null>(null);
  readonly pedidoIdInicial = signal<string | null>(null);
  readonly error = signal('');
  readonly cargando = signal(false);
  readonly accionId = signal<string | null>(null);

  readonly kpis = computed(() => {
    this.pagosApi.pagos();
    return this.pagosApi.kpis();
  });

  readonly filas = computed<PagoFila[]>(() => {
    this.pagosApi.pagos();
    return this.pagosApi.listar(this.filtros()).map((pago) => {
      const pedido = this.pagosApi.pedidoDe(pago);
      return {
        pago,
        clienteNombre: this.pagosApi.clienteDe(pago),
        pedidoEstado: pedido?.estado ?? null,
      };
    });
  });

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const pedidoId = params.get('pedidoId');
    const estado = params.get('estado');
    if (estado && ESTADOS_PAGO.includes(estado as EstadoPago)) {
      this.filtros.update((actual) => ({ ...actual, estado: estado as EstadoPago }));
    }
    void this.cargar().then(() => {
      if (pedidoId) {
        this.abrirRegistro(pedidoId);
      }
    });
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set('');
    try {
      await this.pagosApi.refrescar();
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.cargando.set(false);
    }
  }

  actualizarFiltros(filtros: PagosFiltros): void {
    this.filtros.set(filtros);
  }

  limpiarFiltros(): void {
    this.filtros.set({ ...FILTROS_PAGOS_VACIOS });
  }

  abrirRegistro(pedidoId: string | null = null): void {
    this.error.set('');
    this.pagoEnCurso.set(null);
    this.pedidoIdInicial.set(pedidoId);
    this.dialogAbierto.set(true);
  }

  abrirAsociar(pago: Pago): void {
    this.error.set('');
    this.pagoEnCurso.set(pago);
    this.pedidoIdInicial.set(null);
    this.dialogAbierto.set(true);
  }

  cerrarDialog(): void {
    this.dialogAbierto.set(false);
    this.pagoEnCurso.set(null);
    this.pedidoIdInicial.set(null);
    if (this.route.snapshot.queryParamMap.get('pedidoId')) {
      void this.router.navigate([], { queryParams: {}, replaceUrl: true });
    }
  }

  onGuardado(): void {
    this.cerrarDialog();
    void this.cargar();
  }

  async confirmar(pago: Pago): Promise<void> {
    this.error.set('');
    this.accionId.set(pago.id);
    try {
      await this.pagosApi.confirmar(pago.id);
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.accionId.set(null);
    }
  }

  async rechazar(pago: Pago): Promise<void> {
    this.error.set('');
    if (!globalThis.confirm('¿Rechazar este pago? No sumará al pedido.')) {
      return;
    }
    this.accionId.set(pago.id);
    try {
      await this.pagosApi.rechazar(pago.id, 'Rechazado desde la bandeja.');
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.accionId.set(null);
    }
  }

  async anular(pago: Pago): Promise<void> {
    this.error.set('');
    const ok = globalThis.confirm(
      '¿Anular este pago?\n\n' +
        'La acción es definitiva para efectos contables: el registro se conserva como ANULADO ' +
        '(no se elimina) y dejará de contar en caja, cobertura del pedido e ingresos. ' +
        'Si el pedido solo estaba Pagado, volverá a Pendiente de pago.',
    );
    if (!ok) {
      return;
    }
    this.accionId.set(pago.id);
    try {
      await this.pagosApi.anular(pago.id, 'Anulado desde la bandeja de pagos.');
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.accionId.set(null);
    }
  }
}
