import { CurrencyPipe, DatePipe, NgClass } from '@angular/common';
import {
  Component,
  DestroyRef,
  HostListener,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { readApiError } from '../../../../core/http/api-error';

import { ETIQUETAS_MOVIMIENTO } from '../../../inventario/models/inventario.model';
import { PedidosDigitalesApiService } from '../../../pedidos-digitales/data-access/pedidos-digitales.service';
import { ETIQUETAS_ESTADO_PEDIDO } from '../../../pedidos-digitales/models/pedido-digital.model';
import { ETIQUETAS_TIPO } from '../../../productos-tcg/models/producto-tcg.model';
import { AdjudicarCheckoutDialogComponent } from '../adjudicar-checkout-dialog/adjudicar-checkout-dialog.component';
import { SubastasTcgApiService } from '../../data-access/subastas-tcg.service';
import {
  ETIQUETAS_CANAL_SUBASTA,
  ETIQUETAS_ESTADO_SUBASTA,
  calcularMargenSubasta,
  cuentaRegresiva,
  etiquetaLoteSubasta,
  montoMinimoSiguiente,
  pujaGanadoraActual,
  pujasOrdenadas,
  subastaVencida,
  unidadesLote,
} from '../../models/subasta-tcg.model';
import { SubastaMargenCardComponent } from '../subasta-margen-card/subasta-margen-card.component';

@Component({
  selector: 'app-subasta-detalle-panel',
  imports: [
    CurrencyPipe,
    DatePipe,
    NgClass,
    FormsModule,
    RouterLink,
    SubastaMargenCardComponent,
    AdjudicarCheckoutDialogComponent,
  ],
  templateUrl: './subasta-detalle-panel.component.html',
  styleUrl: './subasta-detalle-panel.component.scss',
  host: {
    '[class.is-panel]': 'modo() === "panel"',
  },
})
export class SubastaDetallePanelComponent {
  private readonly subastasApi = inject(SubastasTcgApiService);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly subastaId = input.required<string>();
  readonly modo = input<'panel' | 'pagina'>('panel');
  readonly closed = output<void>();
  readonly changed = output<void>();

  readonly error = signal('');
  readonly nombrePostor = signal('');
  readonly monto = signal(0);
  readonly ahora = signal(Date.now());
  readonly checkoutAbierto = signal(false);

  readonly etiquetasEstado = ETIQUETAS_ESTADO_SUBASTA;
  readonly etiquetasCanal = ETIQUETAS_CANAL_SUBASTA;
  readonly etiquetasTipo = ETIQUETAS_TIPO;
  readonly etiquetasPedido = ETIQUETAS_ESTADO_PEDIDO;
  readonly movimientoReserva = ETIQUETAS_MOVIMIENTO.PUJA_GANADORA_RESERVA;

  readonly subasta = computed(() => {
    this.subastasApi.subastas();
    return this.subastasApi.obtener(this.subastaId()) ?? null;
  });

  readonly sedeNombre = computed(() => {
    const subasta = this.subasta();
    return this.subastasApi.sedes().find((sede) => sede.id === subasta?.sedeId)?.nombre
      ?? subasta?.sedeNombre
      ?? '';
  });

  readonly detalles = computed(() => this.subasta()?.detalles ?? []);

  readonly etiquetaLote = computed(() => {
    const subasta = this.subasta();
    return subasta ? etiquetaLoteSubasta(subasta) : '';
  });

  readonly unidades = computed(() => {
    const subasta = this.subasta();
    return subasta ? unidadesLote(subasta) : 0;
  });

  readonly stockLibreLote = computed(() => {
    const subasta = this.subasta();
    return subasta ? this.subastasApi.stockLibreLote(subasta) : 0;
  });

  readonly pujas = computed(() => {
    const subasta = this.subasta();
    return subasta ? [...pujasOrdenadas(subasta.pujas)].reverse() : [];
  });

  readonly lider = computed(() => {
    const subasta = this.subasta();
    return subasta ? pujaGanadoraActual(subasta) : null;
  });

  readonly margen = computed(() => {
    const subasta = this.subasta();
    if (!subasta) {
      return calcularMargenSubasta(0, null);
    }
    return this.subastasApi.margenDe(subasta);
  });

  readonly minimoSiguiente = computed(() => {
    const subasta = this.subasta();
    return subasta ? montoMinimoSiguiente(subasta) : 0;
  });

  readonly cronometro = computed(() => {
    const subasta = this.subasta();
    if (!subasta) {
      return { totalMs: 0, vencida: true, texto: '00:00:00' };
    }
    return cuentaRegresiva(subasta.fechaCierre, this.ahora());
  });

  readonly vencida = computed(() => {
    const subasta = this.subasta();
    return subasta ? subastaVencida(subasta, this.ahora()) : false;
  });

  readonly pendienteCierre = computed(
    () => this.subasta()?.estado === 'ACTIVA' && this.vencida(),
  );

  readonly aceptaPujas = computed(() => this.subasta()?.estado === 'ACTIVA' && !this.vencida());

  readonly puedeAdjudicar = computed(() => {
    const estado = this.subasta()?.estado;
    return estado === 'ACTIVA' || estado === 'CERRADA';
  });

  readonly pedido = computed(() => {
    this.pedidosApi.pedidos();
    const id = this.subasta()?.pedidoDigitalId;
    return id ? this.pedidosApi.obtener(id) : undefined;
  });

  readonly etiquetaOferta = computed(() => {
    const estado = this.subasta()?.estado;
    return estado === 'ADJUDICADA' || estado === 'CERRADA'
      ? 'Oferta ganadora'
      : 'Oferta líder';
  });

  constructor() {
    const tick = globalThis.setInterval(() => this.ahora.set(Date.now()), 1_000);
    this.destroyRef.onDestroy(() => globalThis.clearInterval(tick));

    effect(() => {
      const minimo = this.minimoSiguiente();
      if (this.monto() < minimo) {
        this.monto.set(minimo);
      }
    });

    effect((onCleanup) => {
      const subasta = this.subasta();
      if (!subasta || subasta.estado !== 'ACTIVA' || !subastaVencida(subasta)) {
        return;
      }
      const id = globalThis.setInterval(() => {
        void this.subastasApi.refrescar().catch(() => undefined);
      }, 20_000);
      onCleanup(() => globalThis.clearInterval(id));
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.checkoutAbierto()) {
      return;
    }
    if (this.modo() === 'panel') {
      this.closed.emit();
    }
  }

  pujarRapido(pasos = 1): void {
    const subasta = this.subasta();
    if (!subasta) {
      return;
    }
    this.monto.set(this.minimoSiguiente() + subasta.incrementoMinimo * Math.max(pasos - 1, 0));
  }

  async registrarPuja(): Promise<void> {
    const subasta = this.subasta();
    if (!subasta) {
      return;
    }
    if (!this.aceptaPujas()) {
      this.error.set('La subasta ha finalizado y no acepta más pujas.');
      return;
    }
    this.error.set('');
    try {
      await this.subastasApi.registrarPuja(subasta.id, {
        nombrePostor: this.nombrePostor(),
        monto: Number(this.monto()),
      });
      this.nombrePostor.set('');
      this.monto.set(montoMinimoSiguiente(this.subasta() ?? subasta));
      this.changed.emit();
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  activar(): void {
    this.ejecutar((id) => this.subastasApi.activar(id));
  }

  cerrar(): void {
    this.ejecutar((id) => this.subastasApi.cerrar(id));
  }

  abrirCheckout(): void {
    this.error.set('');
    this.checkoutAbierto.set(true);
  }

  onCheckoutConfirmado(): void {
    this.checkoutAbierto.set(false);
    this.changed.emit();
  }

  cancelar(): void {
    if (!globalThis.confirm('¿Cancelar esta subasta?')) {
      return;
    }
    this.ejecutar((id) => this.subastasApi.cancelar(id));
  }

  private ejecutar(accion: (id: string) => unknown): void {
    const subasta = this.subasta();
    if (!subasta) {
      return;
    }
    this.error.set('');
    try {
      const resultado = accion(subasta.id);
      if (resultado instanceof Promise) {
        void resultado
          .then(() => this.changed.emit())
          .catch((err) => {
            this.error.set(readApiError(err));
          });
        return;
      }
      this.changed.emit();
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }
}
