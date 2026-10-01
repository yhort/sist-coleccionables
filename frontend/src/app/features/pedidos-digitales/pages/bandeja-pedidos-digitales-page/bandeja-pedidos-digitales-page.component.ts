import { AfterViewInit, Component, ElementRef, HostListener, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { readApiError } from '../../../../core/http/api-error';
import { CajaTurnoBannerComponent } from '../../../caja/components/caja-turno-banner/caja-turno-banner.component';
import { CajaApiService } from '../../../caja/data-access/caja.service';
import { PackingSlipDialogComponent } from '../../../entregas/components/packing-slip-dialog/packing-slip-dialog.component';
import { EntregasApiService } from '../../../entregas/data-access/entregas.service';
import { EntregaFila } from '../../../entregas/models/entrega.model';
import { StockApiService } from '../../../inventario/data-access/stock.service';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import {
  CrearPedidoDigitalDialogComponent,
  CrearPedidoSavedEvent,
} from '../../components/crear-pedido-digital-dialog/crear-pedido-digital-dialog.component';
import { PedidoDetallePanelComponent } from '../../components/pedido-detalle-panel/pedido-detalle-panel.component';
import { PedidoKanbanItem } from '../../components/pedido-kanban-card/pedido-kanban-card.component';
import { PedidosKanbanComponent } from '../../components/pedidos-kanban/pedidos-kanban.component';
import { PedidosTablaComponent } from '../../components/pedidos-tabla/pedidos-tabla.component';
import { EmitirCpeLoteDialogComponent } from '../../components/emitir-cpe-lote-dialog/emitir-cpe-lote-dialog.component';
import { EmpaqueConsolidadoDialogComponent } from '../../components/empaque-consolidado-dialog/empaque-consolidado-dialog.component';
import { PedidoAccionPreparadaDialogComponent } from '../../components/pedido-accion-preparada-dialog/pedido-accion-preparada-dialog.component';
import { PedidoEtiquetaDialogComponent } from '../../components/pedido-etiqueta-dialog/pedido-etiqueta-dialog.component';
import { RegistrarPagoLoteDialogComponent } from '../../components/registrar-pago-lote-dialog/registrar-pago-lote-dialog.component';
import { PedidosDigitalesApiService } from '../../data-access/pedidos-digitales.service';
import {
  ETIQUETAS_ESTADO_PEDIDO,
  ETIQUETAS_ORIGEN_PEDIDO,
  FILTROS_PEDIDOS_VACIOS,
  ORIGENES_PEDIDO,
  PedidoDigital,
  PedidosDigitalesFiltros,
  EstadoPedidoDigital,
  crearFiltrosPedidosDiaActual,
  etiquetaCantidadPedidos,
  origenDeCanal,
  pedidoPuedeNotaCredito,
} from '../../models/pedido-digital.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

const VISTA_STORAGE_KEY = 'pedidos-digitales.vista';

export type PedidosVistaMode = 'kanban' | 'tabla';

function leerVistaPreferida(): PedidosVistaMode {
  try {
    const valor = localStorage.getItem(VISTA_STORAGE_KEY);
    return valor === 'tabla' ? 'tabla' : 'kanban';
  } catch {
    return 'kanban';
  }
}

@Component({
  selector: 'app-bandeja-pedidos-digitales-page',
  imports: [
    SolesPipe,
    FormsModule,
    CajaTurnoBannerComponent,
    CrearPedidoDigitalDialogComponent,
    PedidoDetallePanelComponent,
    PedidosKanbanComponent,
    PedidosTablaComponent,
    PackingSlipDialogComponent,
    RegistrarPagoLoteDialogComponent,
    EmitirCpeLoteDialogComponent,
    EmpaqueConsolidadoDialogComponent,
    PedidoAccionPreparadaDialogComponent,
    PedidoEtiquetaDialogComponent,
  ],
  templateUrl: './bandeja-pedidos-digitales-page.component.html',
  styleUrl: './bandeja-pedidos-digitales-page.component.scss',
})
export class BandejaPedidosDigitalesPageComponent implements AfterViewInit {
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly stockApi = inject(StockApiService);
  private readonly cajaApi = inject(CajaApiService);
  private readonly entregasApi = inject(EntregasApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly busquedaInput = viewChild<ElementRef<HTMLInputElement>>('busquedaInput');

  readonly origenes = ORIGENES_PEDIDO;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PEDIDO;
  readonly filtros = signal<PedidosDigitalesFiltros>(crearFiltrosPedidosDiaActual());
  readonly vista = signal<PedidosVistaMode>(leerVistaPreferida());
  readonly seleccionadoId = signal<string | null>(null);
  readonly formAbierta = signal(false);
  readonly ticketFila = signal<EntregaFila | null>(null);
  readonly pagoLote = signal<PedidoDigital[] | null>(null);
  readonly cpeLote = signal<PedidoDigital[] | null>(null);
  readonly notaCreditoLote = signal<PedidoDigital | null>(null);
  readonly etiquetasLote = signal<PedidoDigital[] | null>(null);
  readonly consolidadoEmpaque = signal<PedidoDigital[] | null>(null);
  readonly error = signal('');
  readonly aviso = signal('');

  readonly sedeCajaId = computed(() => this.sedesApi.sedes()[0]?.id ?? '');

  constructor() {
    void this.cargar().then(() => this.aplicarQueryParams());
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => this.focusBusqueda());
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (
      this.formAbierta() ||
      this.seleccionadoId() ||
      this.ticketFila() ||
      this.pagoLote() ||
      this.cpeLote() ||
      this.notaCreditoLote() ||
      this.etiquetasLote() ||
      this.consolidadoEmpaque()
    ) {
      return;
    }
    const target = event.target as HTMLElement | null;
    const enCampo =
      target?.tagName === 'INPUT' ||
      target?.tagName === 'TEXTAREA' ||
      target?.tagName === 'SELECT' ||
      target?.isContentEditable;
    if (event.key === '/' && !enCampo) {
      event.preventDefault();
      this.focusBusqueda();
    }
    if ((event.key === 'n' || event.key === 'N') && !enCampo && (event.altKey || event.metaKey)) {
      event.preventDefault();
      this.formAbierta.set(true);
    }
  }

  setVista(mode: PedidosVistaMode): void {
    this.vista.set(mode);
    try {
      localStorage.setItem(VISTA_STORAGE_KEY, mode);
    } catch {
      /* ignore quota / private mode */
    }
  }

  focusBusqueda(): void {
    this.busquedaInput()?.nativeElement.focus();
    this.busquedaInput()?.nativeElement.select();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    try {
      await Promise.all([
        this.productosApi.refrescar(),
        this.sedesApi.refrescar(),
        this.refrescarPedidos(),
      ]);
      const sedeId = this.sedesApi.sedes()[0]?.id;
      if (sedeId) {
        await Promise.all([
          this.stockApi.refrescarSede(sedeId),
          this.cajaApi.refrescarEstados(this.sedesApi.sedes().map((sede) => sede.id)),
        ]);
      }
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  readonly items = computed<PedidoKanbanItem[]>(() => {
    this.pedidosApi.pedidos();
    return this.pedidosApi.listar(this.filtros()).map((pedido) => ({
      pedido,
      sedeNombre:
        pedido.sedeNombre ||
        this.pedidosApi.sedes().find((sede) => sede.id === pedido.sedeId)?.nombre ||
        pedido.sedeId,
      tiposProducto: pedido.detalles.map((detalle) => {
        const producto = this.pedidosApi.productos().find((item) => item.id === detalle.productoId);
        return producto?.tipoProducto ?? 'ACCESORIO';
      }),
    }));
  });

  readonly resumen = computed(() => {
    const items = this.items();
    const pedidos = items.map((item) => item.pedido);
    return {
      total: pedidos.length,
      pendientePago: pedidos.filter((pedido) => pedido.estado === 'PendientePago').length,
      enCurso: pedidos.filter(
        (pedido) =>
          pedido.estado === 'Pagado' ||
          pedido.estado === 'Empaquetado' ||
          pedido.estado === 'PendienteEntrega',
      ).length,
      entregados: pedidos.filter((pedido) => pedido.estado === 'Entregado').length,
      monto: pedidos
        .filter(
          (pedido) =>
            pedido.estado !== 'Cancelado' &&
            pedido.estado !== 'Anulado' &&
            pedido.estado !== 'Devuelto',
        )
        .reduce((sum, pedido) => sum + pedido.total, 0),
    };
  });

  readonly conteoOrigen = computed(() => {
    const pedidos = this.items().map((item) => item.pedido);
    return Object.fromEntries(
      this.origenes.map((origen) => [
        origen,
        pedidos.filter((pedido) => origenDeCanal(pedido.canalPedido) === origen).length,
      ]),
    ) as Record<(typeof ORIGENES_PEDIDO)[number], number>;
  });

  readonly opcionesSubasta = computed(() => {
    this.pedidosApi.pedidos();
    const mapa = new Map<string, string>();
    for (const pedido of this.pedidosApi.pedidos()) {
      if (!pedido.subastaTcgId) {
        continue;
      }
      const label =
        pedido.tituloSubasta?.trim() ||
        pedido.codigoSubasta?.trim() ||
        pedido.subastaTcgId;
      if (!mapa.has(pedido.subastaTcgId)) {
        mapa.set(pedido.subastaTcgId, label);
      }
    }
    return [...mapa.entries()]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label, 'es'));
  });

  actualizarFiltro<K extends keyof PedidosDigitalesFiltros>(
    clave: K,
    valor: PedidosDigitalesFiltros[K],
  ): void {
    this.filtros.update((actual) => ({ ...actual, [clave]: valor }));
    if (clave === 'desde' || clave === 'hasta') {
      void this.refrescarPedidos().catch((err) => this.error.set(readApiError(err)));
    }
  }

  actualizarMonto(clave: 'montoMin' | 'montoMax', valor: string | number | null): void {
    const numero = valor === '' || valor === null ? null : Number(valor);
    this.actualizarFiltro(clave, numero !== null && Number.isFinite(numero) ? numero : null);
  }

  limpiarFiltros(): void {
    this.filtros.set({ ...FILTROS_PEDIDOS_VACIOS });
    void this.refrescarPedidos()
      .then(() => this.focusBusqueda())
      .catch((err) => this.error.set(readApiError(err)));
  }

  abrirDetalle(pedido: PedidoDigital): void {
    this.error.set('');
    this.aviso.set('');
    this.seleccionadoId.set(pedido.id);
  }

  cerrarDetalle(): void {
    this.seleccionadoId.set(null);
  }

  onPedidoCreado(evento: CrearPedidoSavedEvent): void {
    this.formAbierta.set(false);
    this.error.set('');
    this.aviso.set('');
    if (evento.imprimirTicket) {
      this.ticketFila.set(this.entregasApi.filaDesdePedido(evento.pedido));
      return;
    }
    this.focusBusqueda();
  }

  cerrarTicket(): void {
    this.ticketFila.set(null);
    this.focusBusqueda();
  }

  async transicionar(evento: { pedido: PedidoDigital; estado: EstadoPedidoDigital }): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    if (evento.estado === 'Entregado' && !(await this.asegurarCaja(evento.pedido.sedeId))) {
      return;
    }
    try {
      await this.pedidosApi.cambiarEstado(evento.pedido.id, evento.estado);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo actualizar el pedido.');
    }
  }

  async transicionarLote(evento: {
    pedidos: PedidoDigital[];
    estado: EstadoPedidoDigital;
  }): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    if (evento.pedidos.length === 0) {
      return;
    }
    if (evento.estado === 'Entregado') {
      const sedes = [...new Set(evento.pedidos.map((pedido) => pedido.sedeId))];
      for (const sedeId of sedes) {
        if (!(await this.asegurarCaja(sedeId))) {
          return;
        }
      }
    }
    try {
      await this.pedidosApi.cambiarEstadoLote(
        evento.pedidos.map((pedido) => pedido.id),
        evento.estado,
      );
      this.aviso.set(
        `${etiquetaCantidadPedidos(evento.pedidos.length)} pasaron a ${ETIQUETAS_ESTADO_PEDIDO[evento.estado]}.`,
      );
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo actualizar el lote.');
    }
  }

  abrirPagoLote(pedidos: PedidoDigital[]): void {
    this.error.set('');
    this.aviso.set('');
    this.pagoLote.set(pedidos);
  }

  onPagoLoteGuardado(): void {
    const cuantos = this.pagoLote()?.length ?? 0;
    this.pagoLote.set(null);
    this.aviso.set(
      cuantos <= 1
        ? 'Pago registrado. El pedido pasó a Pagado.'
        : `Pago registrado. ${cuantos} pedidos pasaron a Pagado.`,
    );
  }

  abrirCpeLote(pedidos: PedidoDigital[]): void {
    this.error.set('');
    this.aviso.set('');
    this.cpeLote.set(pedidos);
  }

  abrirNotaCreditoLote(pedidos: PedidoDigital[]): void {
    this.error.set('');
    this.aviso.set('');
    const representante =
      pedidos.find((pedido) => pedidoPuedeNotaCredito(pedido)) ??
      pedidos.find((pedido) => !!pedido.notaCredito) ??
      pedidos[0];
    if (!representante?.ventaId) {
      this.error.set(
        'No se encontró una venta con boleta/factura para emitir la nota de crédito del lote.',
      );
      return;
    }
    this.notaCreditoLote.set(representante);
  }

  cerrarNotaCreditoLote(): void {
    const previo = this.notaCreditoLote();
    const estadoPrevio = previo?.estado;
    this.notaCreditoLote.set(null);
    void this.refrescarPedidos().then(() => {
      const actualizado = previo
        ? this.pedidosApi.pedidos().find((item) => item.id === previo.id)
        : null;
      if (
        actualizado &&
        (actualizado.estado === 'Anulado' || actualizado.estado === 'Devuelto') &&
        estadoPrevio !== actualizado.estado
      ) {
        this.aviso.set(
          `Nota de crédito emitida. Los pedidos del comprobante quedaron ${
            actualizado.estado === 'Devuelto' ? 'Devueltos' : 'Anulados'
          }.`,
        );
      }
    });
  }

  onCpeLoteGuardado(): void {
    const cuantos = this.cpeLote()?.length ?? 0;
    this.aviso.set(
      cuantos <= 1
        ? 'Comprobante consolidado emitido.'
        : `Comprobante consolidado emitido para ${cuantos} pedidos.`,
    );
  }

  abrirEtiquetas(pedidos: PedidoDigital[]): void {
    this.error.set('');
    this.aviso.set('');
    if (pedidos.length === 0) {
      return;
    }
    this.etiquetasLote.set(pedidos);
  }

  cerrarEtiquetas(): void {
    this.etiquetasLote.set(null);
  }

  abrirConsolidadoEmpaque(pedidos: PedidoDigital[]): void {
    this.error.set('');
    this.aviso.set('');
    if (pedidos.length < 2) {
      return;
    }
    this.consolidadoEmpaque.set(pedidos);
  }

  cerrarConsolidadoEmpaque(): void {
    this.consolidadoEmpaque.set(null);
  }

  async anular(pedido: PedidoDigital): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    const ok = globalThis.confirm(
      `¿Anular el pedido ${pedido.codigo} de ${pedido.clienteNombre}? Se cancelará y se liberará la reserva de inventario.`,
    );
    if (!ok) {
      return;
    }
    try {
      await this.pedidosApi.cancelar(pedido.id, 'Anulado desde Pedidos Digitales.');
      this.aviso.set(`Pedido ${pedido.codigo} anulado. Reserva liberada.`);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo anular el pedido.');
    }
  }

  async onMarcarNotificados(pedidos: PedidoDigital[]): Promise<void> {
    if (pedidos.length === 0) {
      return;
    }
    this.error.set('');
    try {
      await this.pedidosApi.marcarNotificados(pedidos.map((p) => p.id));
      this.aviso.set(
        pedidos.length === 1
          ? 'Resumen de WhatsApp copiado · marcado como notificado.'
          : `Resumen de WhatsApp copiado · ${pedidos.length} pedidos marcados como notificados.`,
      );
    } catch (err) {
      this.error.set(
        readApiError(err) ||
          'El resumen se copió, pero no se pudo marcar como notificado. Revisa la conexión e inténtalo de nuevo.',
      );
    }
  }

  async onToggleNotificacion(pedido: PedidoDigital): Promise<void> {
    this.error.set('');
    try {
      const actualizado = await this.pedidosApi.actualizarNotificacion(pedido.id, !pedido.notificado);
      this.aviso.set(
        actualizado.notificado
          ? `${actualizado.codigo} marcado como notificado.`
          : `${actualizado.codigo} marcado como sin notificar.`,
      );
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  onAviso(mensaje: string): void {
    this.error.set('');
    this.aviso.set(mensaje);
  }

  onErrorAccion(mensaje: string): void {
    this.aviso.set('');
    this.error.set(mensaje);
  }

  onCajaChanged(): void {
    void this.cajaApi.refrescarEstados(this.sedesApi.sedes().map((sede) => sede.id));
  }

  private async asegurarCaja(sedeId: string): Promise<boolean> {
    try {
      await this.cajaApi.refrescarEstado(sedeId);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo consultar la caja.');
      return false;
    }
    if (this.cajaApi.estaAbierta(sedeId)) {
      return true;
    }
    this.error.set(this.cajaApi.mensajeCerrada(sedeId));
    return false;
  }

  private async refrescarPedidos(): Promise<void> {
    const { desde, hasta } = this.filtros();
    await this.pedidosApi.refrescar({
      desde: desde || null,
      hasta: hasta || null,
    });
  }

  private aplicarQueryParams(): void {
    const params = this.route.snapshot.queryParamMap;
    const nueva = params.get('nueva') === '1';
    const pedidoId = params.get('pedidoId');
    if (nueva) {
      this.formAbierta.set(true);
    }
    if (pedidoId) {
      this.seleccionadoId.set(pedidoId);
    }
    if (nueva || pedidoId) {
      void this.router.navigate([], { queryParams: {}, replaceUrl: true });
    }
  }
}
