import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

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
    if (pedidoId) {
      this.abrirRegistro(pedidoId);
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
  }

  confirmar(pago: Pago): void {
    this.error.set('');
    try {
      this.pagosApi.confirmar(pago.id);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo confirmar el pago.');
    }
  }

  rechazar(pago: Pago): void {
    this.error.set('');
    if (!globalThis.confirm('¿Rechazar este pago? No sumará al pedido.')) {
      return;
    }
    try {
      this.pagosApi.rechazar(pago.id, 'Rechazado desde la bandeja.');
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo rechazar el pago.');
    }
  }
}
