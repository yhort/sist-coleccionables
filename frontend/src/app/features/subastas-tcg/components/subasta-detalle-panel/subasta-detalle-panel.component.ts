import { DatePipe, NgClass } from '@angular/common';
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
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';
import { ClientesApiService } from '../../../clientes/data-access/clientes.service';
import { Cliente } from '../../../clientes/models/cliente.model';
import { ETIQUETAS_MOVIMIENTO } from '../../../inventario/models/inventario.model';
import { PedidosDigitalesApiService } from '../../../pedidos-digitales/data-access/pedidos-digitales.service';
import { ETIQUETAS_ESTADO_PEDIDO } from '../../../pedidos-digitales/models/pedido-digital.model';
import { ETIQUETAS_TIPO } from '../../../productos-tcg/models/producto-tcg.model';
import { SubastasTcgApiService } from '../../data-access/subastas-tcg.service';
import {
  ETIQUETAS_CANAL_SUBASTA,
  ETIQUETAS_ESTADO_DETALLE_SUBASTA,
  ETIQUETAS_ESTADO_SUBASTA,
  ETIQUETAS_MODO_SUBASTA,
  SubastaDetalle,
  calcularMargenSubasta,
  cuentaRegresiva,
  esEventoIndividuales,
  etiquetaLoteSubasta,
  montoMinimoSiguiente,
  nombreVisibleLinea,
  pujaGanadoraActual,
  pujasDeDetalle,
  subastaVencida,
  unidadesLote,
} from '../../models/subasta-tcg.model';
import { AdjudicarCheckoutDialogComponent } from '../adjudicar-checkout-dialog/adjudicar-checkout-dialog.component';
import { SubastaMargenCardComponent } from '../subasta-margen-card/subasta-margen-card.component';

@Component({
  selector: 'app-subasta-detalle-panel',
  imports: [
    SolesPipe,
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
  private readonly clientesApi = inject(ClientesApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly subastaId = input.required<string>();
  readonly modo = input<'panel' | 'pagina'>('panel');
  readonly closed = output<void>();
  readonly changed = output<void>();

  readonly error = signal('');
  readonly nombrePostor = signal('');
  readonly clienteId = signal<string | null>(null);
  readonly sugerenciasCliente = signal<Cliente[]>([]);
  readonly productoPujaId = signal<string | null>(null);
  readonly detalleSeleccionadoId = signal<string | null>(null);
  readonly monto = signal(0);
  readonly ahora = signal(Date.now());
  readonly checkoutAbierto = signal(false);

  readonly etiquetasEstado = ETIQUETAS_ESTADO_SUBASTA;
  readonly etiquetasCanal = ETIQUETAS_CANAL_SUBASTA;
  readonly etiquetasModo = ETIQUETAS_MODO_SUBASTA;
  readonly etiquetasDetalle = ETIQUETAS_ESTADO_DETALLE_SUBASTA;
  readonly etiquetasTipo = ETIQUETAS_TIPO;
  readonly etiquetasPedido = ETIQUETAS_ESTADO_PEDIDO;
  readonly movimientoReserva = ETIQUETAS_MOVIMIENTO.PUJA_GANADORA_RESERVA;
  readonly nombreLinea = nombreVisibleLinea;

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
    if (!subasta) {
      return [];
    }
    const detalleId = esEventoIndividuales(subasta) ? this.detalleSeleccionadoId() : null;
    return [...pujasDeDetalle(subasta, detalleId)].reverse();
  });

  readonly lider = computed(() => {
    const subasta = this.subasta();
    if (!subasta) {
      return null;
    }
    const detalleId = esEventoIndividuales(subasta) ? this.detalleSeleccionadoId() : null;
    return pujaGanadoraActual(subasta, detalleId);
  });

  readonly margen = computed(() => {
    const subasta = this.subasta();
    if (!subasta) {
      return calcularMargenSubasta(0, null);
    }
    const lider = this.lider();
    return calcularMargenSubasta(subasta.precioBase, lider?.monto ?? null);
  });

  readonly minimoSiguiente = computed(() => {
    const subasta = this.subasta();
    if (!subasta) {
      return 0;
    }
    const detalleId = esEventoIndividuales(subasta) ? this.detalleSeleccionadoId() : null;
    return montoMinimoSiguiente(subasta, detalleId);
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

  readonly aceptaPujas = computed(() => {
    const subasta = this.subasta();
    if (!subasta || subasta.estado !== 'ACTIVA' || this.vencida()) {
      return false;
    }
    if (!esEventoIndividuales(subasta)) {
      return true;
    }
    const linea = this.productoPujaSeleccionado();
    return !!linea && linea.estado === 'PENDIENTE';
  });

  readonly puedeAdjudicar = computed(() => {
    const subasta = this.subasta();
    if (!subasta) {
      return false;
    }
    if (esEventoIndividuales(subasta)) {
      const linea = this.productoPujaSeleccionado();
      return (
        (subasta.estado === 'ACTIVA' || subasta.estado === 'CERRADA') &&
        !!linea &&
        linea.estado === 'PENDIENTE'
      );
    }
    return subasta.estado === 'ACTIVA' || subasta.estado === 'CERRADA';
  });

  readonly puedeDeclararDesierta = computed(() => {
    const subasta = this.subasta();
    if (!subasta) {
      return false;
    }
    if (esEventoIndividuales(subasta)) {
      const linea = this.productoPujaSeleccionado();
      return (
        (subasta.estado === 'ACTIVA' || subasta.estado === 'CERRADA') &&
        !!linea &&
        linea.estado === 'PENDIENTE'
      );
    }
    return subasta.estado === 'ACTIVA';
  });

  readonly puedeAnularPuja = computed(() => {
    const subasta = this.subasta();
    if (!subasta) {
      return false;
    }
    if (subasta.estado === 'ADJUDICADA' || subasta.estado === 'CANCELADA') {
      return false;
    }
    if (esEventoIndividuales(subasta)) {
      return this.productoPujaSeleccionado()?.estado === 'PENDIENTE';
    }
    return !subasta.pedidoDigitalId;
  });

  readonly pedido = computed(() => {
    this.pedidosApi.pedidos();
    const subasta = this.subasta();
    if (!subasta) {
      return undefined;
    }
    if (esEventoIndividuales(subasta)) {
      const linea = this.productoPujaSeleccionado();
      const id = linea?.pedidoDigitalId;
      return id ? this.pedidosApi.obtener(id) : undefined;
    }
    const id = subasta.pedidoDigitalId;
    return id ? this.pedidosApi.obtener(id) : undefined;
  });

  readonly etiquetaOferta = computed(() => {
    const estado = this.subasta()?.estado;
    const linea = this.productoPujaSeleccionado();
    if (linea?.estado === 'ADJUDICADO' || estado === 'ADJUDICADA' || estado === 'CERRADA') {
      return 'Oferta ganadora';
    }
    return 'Oferta líder';
  });

  readonly esEvento = computed(() => {
    const subasta = this.subasta();
    return subasta ? esEventoIndividuales(subasta) : false;
  });

  readonly esMultiItem = computed(
    () => this.esEvento() || this.detalles().length > 1,
  );

  readonly productoPujaSeleccionado = computed(() => {
    const id = this.detalleSeleccionadoId();
    if (!id) {
      return null;
    }
    return this.detalles().find((l) => l.id === id) ?? null;
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

    effect(() => {
      const lineas = this.detalles();
      const actual = this.detalleSeleccionadoId();
      if (lineas.length === 0) {
        this.detalleSeleccionadoId.set(null);
        this.productoPujaId.set(null);
        return;
      }
      if (actual && lineas.some((l) => l.id === actual)) {
        return;
      }
      const preferida =
        lineas.find((l) => l.estado === 'PENDIENTE') ?? lineas[0];
      this.detalleSeleccionadoId.set(preferida.id);
      this.productoPujaId.set(preferida.productoId);
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

  seleccionarProductoPuja(linea: SubastaDetalle): void {
    if (!this.esMultiItem() && !this.esEvento()) {
      return;
    }
    this.detalleSeleccionadoId.set(linea.id);
    this.productoPujaId.set(linea.productoId);
    this.sugerenciasCliente.set([]);
  }

  async buscarCliente(texto: string): Promise<void> {
    this.clienteId.set(null);
    this.nombrePostor.set(texto);
    const query = texto.trim();
    if (query.length < 2) {
      this.sugerenciasCliente.set([]);
      return;
    }
    try {
      const hits = await this.clientesApi.buscar(query);
      const q = query.toLowerCase();
      this.sugerenciasCliente.set(
        hits.filter(
          (c) =>
            c.nombre.toLowerCase().includes(q) ||
            (c.telefono ?? '').includes(query) ||
            (c.contactoReferencia ?? '').toLowerCase().includes(q),
        ),
      );
    } catch {
      this.sugerenciasCliente.set([]);
    }
  }

  elegirCliente(cliente: Cliente): void {
    this.clienteId.set(cliente.id);
    this.nombrePostor.set(cliente.nombre);
    this.sugerenciasCliente.set([]);
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
      this.error.set(
        this.esEvento()
          ? 'Selecciona una carta pendiente para registrar la puja.'
          : 'La subasta ha finalizado y no acepta más pujas.',
      );
      return;
    }
    const detalleId = this.esEvento() ? this.detalleSeleccionadoId() : null;
    if (this.esEvento() && !detalleId) {
      this.error.set('Selecciona la carta a la que aplica la puja.');
      return;
    }
    this.error.set('');
    try {
      await this.subastasApi.registrarPuja(subasta.id, {
        nombrePostor: this.nombrePostor(),
        clienteId: this.clienteId(),
        subastaDetalleId: detalleId,
        monto: Number(this.monto()),
      });
      this.nombrePostor.set('');
      this.clienteId.set(null);
      this.sugerenciasCliente.set([]);
      this.monto.set(
        montoMinimoSiguiente(this.subasta() ?? subasta, this.esEvento() ? detalleId : null),
      );
      this.changed.emit();
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  activar(): void {
    this.ejecutar((id) => this.subastasApi.activar(id));
  }

  declararDesierta(): void {
    const subasta = this.subasta();
    if (!subasta) {
      return;
    }
    const detalleId = this.esEvento() ? this.detalleSeleccionadoId() : null;
    if (this.esEvento() && !detalleId) {
      this.error.set('Selecciona la carta a declarar desierta.');
      return;
    }
    const etiqueta = this.esEvento()
      ? this.productoPujaSeleccionado()
        ? nombreVisibleLinea(this.productoPujaSeleccionado()!)
        : 'esta carta'
      : 'esta subasta';
    if (!globalThis.confirm(`¿Declarar desierta ${etiqueta}? No se creará pedido.`)) {
      return;
    }
    this.ejecutar((id) => this.subastasApi.declararDesierta(id, detalleId));
  }

  eliminarPuja(pujaId: string): void {
    if (!globalThis.confirm('¿Anular esta puja del historial?')) {
      return;
    }
    this.error.set('');
    void this.subastasApi
      .eliminarPuja(pujaId)
      .then(() => this.changed.emit())
      .catch((err) => this.error.set(readApiError(err)));
  }

  abrirCheckout(): void {
    this.error.set('');
    if (this.esEvento() && !this.detalleSeleccionadoId()) {
      this.error.set('Selecciona la carta a adjudicar.');
      return;
    }
    if (this.esEvento() && this.productoPujaSeleccionado()?.estado === 'ADJUDICADO') {
      this.error.set('Esa carta ya fue adjudicada; elige otra pendiente.');
      return;
    }
    this.checkoutAbierto.set(true);
  }

  onCheckoutConfirmado(): void {
    this.checkoutAbierto.set(false);
    this.changed.emit();
  }

  cancelar(): void {
    if (!globalThis.confirm('¿Anular esta subasta por completo?')) {
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
