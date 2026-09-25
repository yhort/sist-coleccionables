import { DatePipe } from '@angular/common';
import { Component, HostListener, computed, effect, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { EcosistemaApiService } from '../../../ecosistema/data-access/ecosistema.service';
import {
  ETIQUETAS_COMPROBANTE,
  ETIQUETAS_ESTADO_EMISION,
  EmisionSimulada,
  EstadoEmisionSunat,
  TIPOS_COMPROBANTE_POS,
  TipoComprobanteSunat,
  numeroComprobante,
  puedeDescargarCdr,
  puedeDescargarXml,
  puedeAbrirPdf,
} from '../../../ecosistema/models/ecosistema.model';
import {
  ETIQUETAS_ESTADO_PAGO,
  ETIQUETAS_ORIGEN_PAGO,
  Pago,
} from '../../../pagos/models/pago.model';
import {
  ComprobanteConsolidadoApi,
  PedidosDigitalesApiService,
} from '../../data-access/pedidos-digitales.service';
import {
  PedidoDigital,
  PedidoDigitalDetalle,
  MOTIVOS_NOTA_CREDITO_SUNAT,
  cpeEstadoEmitido,
  descripcionMotivoNotaCredito,
  etiquetaNumeroCpe,
  pedidoPuedeNotaCredito,
  pedidoTieneCpeEmitido,
  round2,
} from '../../models/pedido-digital.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

export interface NcLineaUi {
  key: string;
  productoId: string;
  descripcion: string;
  cantidadMax: number;
  precioUnitario: number;
  totalMax: number;
  seleccionado: boolean;
  cantidad: number;
}

@Component({
  selector: 'app-pedido-accion-preparada-dialog',
  imports: [RouterLink, SolesPipe, DatePipe],
  templateUrl: './pedido-accion-preparada-dialog.component.html',
  styleUrl: './pedido-accion-preparada-dialog.component.scss',
})
export class PedidoAccionPreparadaDialogComponent {
  private readonly ecosistemaApi = inject(EcosistemaApiService);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);

  readonly tipo = input.required<'pago' | 'pago-consulta' | 'cpe' | 'nota-credito'>();
  readonly pedido = input<PedidoDigital | null>(null);
  readonly pagos = input<Pago[]>([]);
  readonly closed = output<void>();

  readonly error = signal('');
  readonly enviando = signal(false);
  readonly emision = signal<EmisionSimulada | null>(null);
  readonly notaCredito = signal<EmisionSimulada | null>(null);
  readonly codigoMotivo = signal('01');
  readonly descripcionMotivo = signal('Anulación de la operación');
  readonly lineasNc = signal<NcLineaUi[]>([]);
  readonly motivosSunat = MOTIVOS_NOTA_CREDITO_SUNAT;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_EMISION;
  readonly etiquetasOrigenPago = ETIQUETAS_ORIGEN_PAGO;
  readonly etiquetasEstadoPago = ETIQUETAS_ESTADO_PAGO;
  readonly tipos = TIPOS_COMPROBANTE_POS;
  readonly etiquetas = ETIQUETAS_COMPROBANTE;
  readonly tipoSeleccionado = signal<TipoComprobanteSunat>('BOLETA');
  readonly numeroDe = numeroComprobante;
  readonly puedeXml = puedeDescargarXml;
  readonly puedeCdr = puedeDescargarCdr;
  readonly puedePdf = puedeAbrirPdf;
  readonly etiquetaNumero = etiquetaNumeroCpe;

  readonly cpeSoloConsulta = computed(() => {
    const pedido = this.pedido();
    if (!pedido) {
      return false;
    }
    return pedidoTieneCpeEmitido(pedido) || cpeEstadoEmitido(this.emision()?.estado);
  });

  readonly puedeEmitirNc = computed(() => {
    const pedido = this.pedido();
    return pedido ? pedidoPuedeNotaCredito(pedido) : false;
  });

  readonly esMotivoTotal = computed(() => {
    const codigo = this.codigoMotivo();
    return codigo === '01' || codigo === '02' || codigo === '06';
  });

  readonly esMotivoParcial = computed(() => this.codigoMotivo() === '07');

  readonly totalNc = computed(() =>
    round2(
      this.lineasNc()
        .filter((linea) => linea.seleccionado && linea.cantidad > 0)
        .reduce((sum, linea) => sum + round2(linea.cantidad * linea.precioUnitario), 0),
    ),
  );

  readonly totalOrigen = computed(() =>
    round2(this.lineasNc().reduce((sum, linea) => sum + linea.totalMax, 0)),
  );

  constructor() {
    effect(() => {
      const tipo = this.tipo();
      const pedido = this.pedido();
      if (!pedido) {
        return;
      }

      if (tipo === 'cpe') {
        if (pedido.comprobante && pedidoTieneCpeEmitido(pedido)) {
          const c = pedido.comprobante;
          this.emision.set({
            id: c.id,
            tipo: c.tipo,
            serie: c.serie,
            correlativo: c.correlativo,
            estado: c.estado as EstadoEmisionSunat,
            pedidoId: pedido.id,
            ventaId: pedido.ventaId,
            clienteNombre: pedido.clienteNombre,
            total: pedido.total,
            mensaje: `CPE emitido: ${etiquetaNumeroCpe(c)}`,
            fecha: new Date().toISOString(),
            hashFirma: null,
            tieneXml: true,
            tieneCdr: c.estado === 'ACEPTADO',
            tienePdf: true,
          });
          return;
        }
        const ventaId = pedido.ventaId;
        if (!ventaId) {
          return;
        }
        void this.ecosistemaApi
          .obtenerPorVenta(ventaId)
          .then((emision) => {
            if (emision) {
              this.emision.set(emision);
            }
          })
          .catch(() => undefined);
        return;
      }

      if (tipo === 'nota-credito') {
        this.inicializarLineasDesdePedido(pedido);
        if (pedido.notaCredito) {
          const nc = pedido.notaCredito;
          this.notaCredito.set({
            id: nc.id,
            tipo: 'NOTA_CREDITO',
            serie: nc.serie,
            correlativo: nc.correlativo,
            estado: nc.estado as EstadoEmisionSunat,
            pedidoId: pedido.id,
            ventaId: pedido.ventaId,
            clienteNombre: pedido.clienteNombre,
            total: pedido.total,
            mensaje: nc.descripcionMotivo || 'Nota de crédito emitida',
            fecha: new Date().toISOString(),
            hashFirma: null,
            tieneXml: true,
            tieneCdr: nc.estado === 'ACEPTADO',
            tienePdf: true,
            codigoMotivo: nc.codigoMotivo,
            descripcionMotivo: nc.descripcionMotivo,
            documentoReferencia: nc.documentoReferencia,
          });
        }
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }

  puedeEmitirAhora(): boolean {
    const pedido = this.pedido();
    if (!pedido || this.enviando() || this.cpeSoloConsulta()) {
      return false;
    }
    if (pedido.estado === 'Anulado' || pedido.estado === 'Devuelto') {
      return false;
    }
    return (
      pedido.estado === 'Pagado' ||
      pedido.estado === 'Empaquetado' ||
      pedido.estado === 'PendienteEntrega' ||
      pedido.estado === 'Entregado'
    );
  }

  onMotivoChange(codigo: string): void {
    this.codigoMotivo.set(codigo);
    this.descripcionMotivo.set(descripcionMotivoNotaCredito(codigo));
    this.aplicarModoMotivoALineas(codigo);
  }

  toggleLinea(key: string, checked: boolean): void {
    if (!this.esMotivoParcial()) {
      return;
    }
    this.lineasNc.update((lineas) =>
      lineas.map((linea) =>
        linea.key === key
          ? {
              ...linea,
              seleccionado: checked,
              cantidad: checked ? (linea.cantidad > 0 ? linea.cantidad : linea.cantidadMax) : 0,
            }
          : linea,
      ),
    );
  }

  onCantidadLinea(key: string, valor: string | number): void {
    if (!this.esMotivoParcial()) {
      return;
    }
    const numero = typeof valor === 'number' ? valor : Number(valor);
    this.lineasNc.update((lineas) =>
      lineas.map((linea) => {
        if (linea.key !== key) {
          return linea;
        }
        if (!Number.isFinite(numero) || numero <= 0) {
          return { ...linea, cantidad: 0, seleccionado: false };
        }
        const cantidad = Math.min(linea.cantidadMax, round2(numero));
        return { ...linea, cantidad, seleccionado: cantidad > 0 };
      }),
    );
  }

  async emitir(): Promise<void> {
    const pedido = this.pedido();
    if (!pedido || !this.puedeEmitirAhora()) {
      this.error.set(
        this.cpeSoloConsulta()
          ? 'Este pedido ya tiene comprobante emitido.'
          : 'El pedido debe estar pagado para emitir el comprobante.',
      );
      return;
    }
    this.error.set('');
    this.enviando.set(true);
    try {
      const tipo = this.tipoSeleccionado();
      if (tipo !== 'BOLETA' && tipo !== 'FACTURA' && tipo !== 'NOTA_VENTA') {
        this.error.set('Selecciona boleta, factura o nota de venta.');
        return;
      }
      const resultado = await this.pedidosApi.emitirComprobanteConsolidado([pedido.id], tipo);
      this.emision.set(mapEmisionConsolidada(resultado, pedido));
      await this.pedidosApi.refrescar().catch(() => undefined);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo emitir el comprobante.');
    } finally {
      this.enviando.set(false);
    }
  }

  async emitirNotaCredito(): Promise<void> {
    const pedido = this.pedido();
    const ventaId = pedido?.ventaId;
    if (!pedido || !ventaId || !this.puedeEmitirNc()) {
      this.error.set('No se puede emitir nota de crédito para este pedido.');
      return;
    }
    const descripcion = this.descripcionMotivo().trim();
    if (descripcion.length < 3) {
      this.error.set('Indica el motivo de la nota de crédito (mínimo 3 caracteres).');
      return;
    }
    const codigo = this.codigoMotivo().trim() || '01';
    const items = this.lineasNc()
      .filter((linea) => linea.seleccionado && linea.cantidad > 0)
      .map((linea) => ({
        productoId: linea.productoId,
        cantidad: linea.cantidad,
      }));
    if (codigo === '07' && items.length === 0) {
      this.error.set('Selecciona al menos un ítem con cantidad para la devolución parcial.');
      return;
    }
    if (this.totalNc() <= 0) {
      this.error.set('El monto de la nota de crédito debe ser mayor a cero.');
      return;
    }
    this.error.set('');
    this.enviando.set(true);
    try {
      const emision = await this.ecosistemaApi.emitirNotaCredito(ventaId, {
        codigoMotivo: codigo,
        descripcionMotivo: descripcion,
        items: codigo === '07' ? items : items.length > 0 ? items : undefined,
      });
      this.notaCredito.set(emision);
      await this.pedidosApi.refrescar().catch(() => undefined);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo emitir la nota de crédito.');
    } finally {
      this.enviando.set(false);
    }
  }

  async descargarXml(fuente: 'cpe' | 'nc' = 'cpe'): Promise<void> {
    const emision = fuente === 'nc' ? this.notaCredito() : this.emision();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.descargarXml(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el XML.');
    }
  }

  async descargarCdr(fuente: 'cpe' | 'nc' = 'cpe'): Promise<void> {
    const emision = fuente === 'nc' ? this.notaCredito() : this.emision();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.descargarCdr(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el CDR.');
    }
  }

  async abrirPdf(formato: 'a4' | 'ticket' = 'a4', fuente: 'cpe' | 'nc' = 'cpe'): Promise<void> {
    const emision = fuente === 'nc' ? this.notaCredito() : this.emision();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.abrirPdf(emision, formato);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo abrir el PDF.');
    }
  }

  private inicializarLineasDesdePedido(pedido: PedidoDigital): void {
    const detalles = this.detallesComprobante(pedido);
    const lineas = detalles.map((detalle, index) => ({
      key: `${detalle.productoId}-${index}`,
      productoId: detalle.productoId,
      descripcion: detalle.descripcion,
      cantidadMax: detalle.cantidad,
      precioUnitario: detalle.precioUnitario,
      totalMax: detalle.total,
      seleccionado: true,
      cantidad: detalle.cantidad,
    }));
    this.lineasNc.set(lineas);
    this.aplicarModoMotivoALineas(this.codigoMotivo());
  }

  private detallesComprobante(pedido: PedidoDigital): PedidoDigitalDetalle[] {
    const ventaId = pedido.ventaId;
    if (!ventaId) {
      return [...pedido.detalles];
    }
    const hermanos = this.pedidosApi
      .pedidos()
      .filter((item) => item.ventaId === ventaId);
    const fuente = hermanos.length > 0 ? hermanos : [pedido];
    const mapa = new Map<string, PedidoDigitalDetalle>();
    for (const hermano of fuente) {
      for (const detalle of hermano.detalles) {
        const actual = mapa.get(detalle.productoId);
        if (!actual) {
          mapa.set(detalle.productoId, { ...detalle });
          continue;
        }
        const cantidad = round2(actual.cantidad + detalle.cantidad);
        const total = round2(actual.total + detalle.total);
        mapa.set(detalle.productoId, {
          ...actual,
          cantidad,
          total,
          precioUnitario:
            cantidad > 0 ? round2(total / cantidad) : actual.precioUnitario,
        });
      }
    }
    return [...mapa.values()];
  }

  private aplicarModoMotivoALineas(codigo: string): void {
    const parcial = codigo === '07';
    this.lineasNc.update((lineas) =>
      lineas.map((linea) =>
        parcial
          ? { ...linea, seleccionado: true, cantidad: linea.cantidadMax }
          : { ...linea, seleccionado: true, cantidad: linea.cantidadMax },
      ),
    );
  }
}

function mapEmisionConsolidada(
  resultado: ComprobanteConsolidadoApi,
  pedido: PedidoDigital,
): EmisionSimulada {
  const comprobante = resultado.comprobante;
  return {
    id: comprobante.id,
    tipo: comprobante.tipo,
    serie: comprobante.serie,
    correlativo: comprobante.correlativo,
    estado: comprobante.estado as EstadoEmisionSunat,
    pedidoId: pedido.id,
    ventaId: resultado.ventaId,
    clienteNombre: comprobante.clienteNombre ?? pedido.clienteNombre,
    total: comprobante.total ?? pedido.total,
    mensaje:
      comprobante.mensaje ||
      `${ETIQUETAS_COMPROBANTE[comprobante.tipo]} ${numeroComprobante(comprobante)} · ${comprobante.estado}`,
    fecha: comprobante.fechaEmision,
    hashFirma: comprobante.hashFirma ?? null,
    tieneXml: comprobante.tieneXml === true,
    tieneCdr: comprobante.tieneCdr === true,
    tienePdf: comprobante.tienePdf !== false,
  };
}
