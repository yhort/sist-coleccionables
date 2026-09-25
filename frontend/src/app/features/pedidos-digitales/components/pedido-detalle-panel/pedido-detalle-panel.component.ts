import { DatePipe, NgClass } from '@angular/common';
import { Component, HostListener, computed, effect, inject, input, output, signal } from '@angular/core';

import { EcosistemaApiService } from '../../../ecosistema/data-access/ecosistema.service';
import { codigoItem, pareceUuid } from '../../../../core/ui/codigo-amigable';
import {
  ETIQUETAS_ESTADO_EMISION,
  EmisionSimulada,
  numeroComprobante,
  puedeDescargarCdr,
  puedeDescargarXml,
  puedeAbrirPdf,
} from '../../../ecosistema/models/ecosistema.model';
import { CajaApiService } from '../../../caja/data-access/caja.service';
import { ETIQUETAS_MOVIMIENTO } from '../../../inventario/models/inventario.model';
import { PagosApiService } from '../../../pagos/data-access/pagos.service';
import { Pago, round2 } from '../../../pagos/models/pago.model';
import { ETIQUETAS_TIPO } from '../../../productos-tcg/models/producto-tcg.model';
import { PedidosDigitalesApiService } from '../../data-access/pedidos-digitales.service';
import {
  ETIQUETAS_CANAL_PEDIDO,
  ETIQUETAS_ESTADO_PEDIDO,
  ETIQUETAS_ORIGEN_PEDIDO,
  EstadoPedidoDigital,
  cpeEstadoEmitido,
  etiquetaAccionEstado,
  etiquetaNumeroCpe,
  indicadorReservaDe,
  origenDeCanal,
  pedidoBloqueadoPorNc,
  pedidoPuedeNotaCredito,
  pedidoTieneCpeEmitido,
  transicionesPermitidas,
} from '../../models/pedido-digital.model';
import { etiquetaCanalContacto } from '../../../../shared/models/contacto-entrega.model';
import { PedidoAccionPreparadaDialogComponent } from '../pedido-accion-preparada-dialog/pedido-accion-preparada-dialog.component';
import { RegistrarPagoLoteDialogComponent } from '../registrar-pago-lote-dialog/registrar-pago-lote-dialog.component';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

export type PedidoDetalleAccion = 'pago-consulta' | 'cpe' | 'nota-credito';

@Component({
  selector: 'app-pedido-detalle-panel',
  imports: [SolesPipe, DatePipe, NgClass, PedidoAccionPreparadaDialogComponent, RegistrarPagoLoteDialogComponent],
  templateUrl: './pedido-detalle-panel.component.html',
  styleUrl: './pedido-detalle-panel.component.scss',
})
export class PedidoDetallePanelComponent {
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly ecosistemaApi = inject(EcosistemaApiService);
  private readonly cajaApi = inject(CajaApiService);
  private readonly pagosApi = inject(PagosApiService);

  readonly pedidoId = input.required<string>();
  readonly closed = output<void>();

  readonly error = signal('');
  readonly accion = signal<PedidoDetalleAccion | null>(null);
  readonly cobrando = signal(false);
  readonly comprobante = signal<EmisionSimulada | null>(null);
  readonly pagosVinculados = signal<Pago[]>([]);
  readonly cargandoPagos = signal(false);

  readonly etiquetasEstado = ETIQUETAS_ESTADO_PEDIDO;
  readonly etiquetasCanal = ETIQUETAS_CANAL_PEDIDO;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PEDIDO;
  readonly etiquetasTipo = ETIQUETAS_TIPO;
  readonly etiquetasCpe = ETIQUETAS_ESTADO_EMISION;
  readonly etiquetaCanal = etiquetaCanalContacto;
  readonly movimientoReserva = ETIQUETAS_MOVIMIENTO.RESERVA;
  readonly movimientoLiberacion = ETIQUETAS_MOVIMIENTO.LIBERACION_RESERVA;
  readonly movimientoVenta = ETIQUETAS_MOVIMIENTO.VENTA;
  readonly numeroDe = numeroComprobante;
  readonly puedeXml = puedeDescargarXml;
  readonly puedeCdr = puedeDescargarCdr;
  readonly puedePdf = puedeAbrirPdf;

  readonly pedido = computed(() => {
    this.pedidosApi.pedidos();
    return this.pedidosApi.obtener(this.pedidoId()) ?? null;
  });

  readonly sedeNombre = computed(() => {
    const pedido = this.pedido();
    return (
      pedido?.sedeNombre ||
      this.pedidosApi.sedes().find((sede) => sede.id === pedido?.sedeId)?.nombre ||
      ''
    );
  });

  readonly itemsEnriquecidos = computed(() => {
    const pedido = this.pedido();
    if (!pedido) {
      return [];
    }
    return pedido.detalles.map((detalle) => {
      const producto = this.pedidosApi.productos().find((item) => item.id === detalle.productoId);
      const skuRaw = producto?.codigoSku?.trim() ?? '';
      const sku =
        skuRaw && !pareceUuid(skuRaw) ? skuRaw : (detalle.codigo || codigoItem(detalle.id));
      return {
        detalle,
        tipoProducto: producto?.tipoProducto ?? 'ACCESORIO',
        sku,
      };
    });
  });

  readonly historial = computed(() => {
    const pedido = this.pedido();
    return pedido ? [...pedido.historialEstados].reverse() : [];
  });

  readonly montoCubierto = computed(() =>
    round2(
      this.pagosVinculados()
        .filter((pago) => pago.estado === 'ASOCIADO' || pago.estado === 'CONFIRMADO')
        .reduce((sum, pago) => sum + pago.monto, 0),
    ),
  );

  readonly saldoPendiente = computed(() => {
    const pedido = this.pedido();
    if (!pedido) {
      return 0;
    }
    return round2(Math.max(0, pedido.total - this.montoCubierto()));
  });

  private readonly estadosYaPagados: readonly EstadoPedidoDigital[] = [
    'Pagado',
    'Empaquetado',
    'PendienteEntrega',
    'Entregado',
  ];

  /** Solo conciliar si aún está pendiente de pago y falta saldo. */
  readonly puedeVincularPago = computed(() => {
    const pedido = this.pedido();
    if (!pedido || pedido.estado !== 'PendientePago') {
      return false;
    }
    // Si aún no cargaron pagos, asumir que falta cobro (es PendientePago).
    if (this.cargandoPagos() && this.pagosVinculados().length === 0) {
      return true;
    }
    return this.saldoPendiente() > 0.009;
  });

  /** Consulta de cobro: depende del estado operativo, no de la lista en memoria. */
  readonly puedeVerPagoVinculado = computed(() => {
    const pedido = this.pedido();
    if (
      !pedido ||
      pedido.estado === 'Cancelado' ||
      pedido.estado === 'Anulado' ||
      pedido.estado === 'Devuelto'
    ) {
      return false;
    }
    if (this.estadosYaPagados.includes(pedido.estado)) {
      return true;
    }
    // PendientePago con saldo cubierto (pago parcial/total aún no reflejado en estado).
    return pedido.estado === 'PendientePago' && this.saldoPendiente() <= 0.009;
  });

  /** Emisión CPE disponible desde Pagado en adelante (sin exigir Entregado). */
  readonly puedeEmitirCpe = computed(() => {
    const pedido = this.pedido();
    if (!pedido || pedidoBloqueadoPorNc(pedido)) {
      return false;
    }
    return this.estadosYaPagados.includes(pedido.estado);
  });

  readonly cpeYaEmitido = computed(() => {
    const pedido = this.pedido();
    if (pedido && pedidoTieneCpeEmitido(pedido)) {
      return true;
    }
    return cpeEstadoEmitido(this.comprobante()?.estado);
  });

  readonly etiquetaCpeEmitido = computed(() => {
    const pedido = this.pedido();
    if (pedido?.comprobante) {
      return etiquetaNumeroCpe(pedido.comprobante);
    }
    const cpe = this.comprobante();
    return cpe ? numeroComprobante(cpe) : '';
  });

  readonly puedeNotaCredito = computed(() => {
    const pedido = this.pedido();
    return pedido ? pedidoPuedeNotaCredito(pedido) : false;
  });

  origenDe = origenDeCanal;
  reservaDe = indicadorReservaDe;
  transicionesDe = transicionesPermitidas;
  etiquetaAccion = etiquetaAccionEstado;
  etiquetaNumeroCpe = etiquetaNumeroCpe;

  constructor() {
    effect(() => {
      const ventaId = this.pedido()?.ventaId;
      if (!ventaId) {
        this.comprobante.set(null);
        return;
      }
      void this.ecosistemaApi
        .obtenerPorVenta(ventaId)
        .then((emision) => this.comprobante.set(emision))
        .catch(() => this.comprobante.set(null));
    });

    effect(() => {
      const id = this.pedidoId();
      void this.cargarPagos(id);
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.cobrando()) {
      this.cobrando.set(false);
      return;
    }
    if (this.accion()) {
      this.cerrarAccion();
      return;
    }
    this.closed.emit();
  }

  async transicionar(estado: EstadoPedidoDigital): Promise<void> {
    const pedido = this.pedido();
    if (!pedido) {
      return;
    }
    this.error.set('');
    if (estado === 'Entregado') {
      try {
        await this.cajaApi.refrescarEstado(pedido.sedeId);
      } catch (err) {
        this.error.set(err instanceof Error ? err.message : 'No se pudo consultar la caja.');
        return;
      }
      if (!this.cajaApi.estaAbierta(pedido.sedeId)) {
        this.error.set(this.cajaApi.mensajeCerrada(pedido.sedeId));
        return;
      }
    }
    try {
      await this.pedidosApi.cambiarEstado(pedido.id, estado);
      await this.cargarPagos(pedido.id);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo actualizar el estado.');
    }
  }

  abrirPago(): void {
    if (!this.puedeVincularPago()) {
      return;
    }
    this.cobrando.set(true);
  }

  onPagoRegistrado(): void {
    this.cobrando.set(false);
    const pedido = this.pedido();
    if (pedido) {
      void this.cargarPagos(pedido.id);
    }
  }

  async abrirPagoConsulta(): Promise<void> {
    const pedido = this.pedido();
    if (!pedido) {
      return;
    }
    if (this.pagosVinculados().length === 0) {
      await this.cargarPagos(pedido.id);
    }
    this.accion.set('pago-consulta');
  }

  abrirCpe(): void {
    this.accion.set('cpe');
  }

  abrirNotaCredito(): void {
    const pedido = this.pedido();
    if (!pedido) {
      return;
    }
    if (!this.puedeNotaCredito() && !pedido.notaCredito) {
      return;
    }
    this.accion.set('nota-credito');
  }

  cerrarAccion(): void {
    this.accion.set(null);
    const pedido = this.pedido();
    if (pedido) {
      void this.cargarPagos(pedido.id);
    }
    const ventaId = pedido?.ventaId;
    if (!ventaId) {
      return;
    }
    void this.ecosistemaApi
      .obtenerPorVenta(ventaId)
      .then((emision) => this.comprobante.set(emision))
      .catch(() => undefined);
  }

  async descargarXml(): Promise<void> {
    const emision = this.comprobante();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.descargarXml(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el XML.');
    }
  }

  async descargarCdr(): Promise<void> {
    const emision = this.comprobante();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.descargarCdr(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el CDR.');
    }
  }

  async abrirPdf(formato: 'a4' | 'ticket' = 'a4'): Promise<void> {
    const emision = this.comprobante();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.abrirPdf(emision, formato);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo abrir el PDF.');
    }
  }

  private async cargarPagos(pedidoId: string): Promise<void> {
    this.cargandoPagos.set(true);
    try {
      const pagos = await this.pagosApi.listarPorPedido(pedidoId);
      this.pagosVinculados.set(pagos.filter((p) => p.estado !== 'RECHAZADO'));
    } catch {
      this.pagosVinculados.set([]);
    } finally {
      this.cargandoPagos.set(false);
    }
  }
}
