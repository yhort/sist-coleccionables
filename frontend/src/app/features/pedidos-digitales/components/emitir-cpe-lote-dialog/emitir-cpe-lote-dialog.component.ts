import { Component, HostListener, computed, inject, input, output, signal } from '@angular/core';

import { EcosistemaApiService } from '../../../ecosistema/data-access/ecosistema.service';
import {
  ETIQUETAS_COMPROBANTE,
  ETIQUETAS_ESTADO_EMISION,
  EmisionSimulada,
  EstadoEmisionSunat,
  TIPOS_COMPROBANTE_POS,
  numeroComprobante,
  puedeAbrirPdf,
  puedeDescargarCdr,
  puedeDescargarXml,
} from '../../../ecosistema/models/ecosistema.model';
import {
  ComprobanteConsolidadoApi,
  PedidosDigitalesApiService,
} from '../../data-access/pedidos-digitales.service';
import { PedidoDigital, etiquetaCantidadPedidos, round2 } from '../../models/pedido-digital.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-emitir-cpe-lote-dialog',
  imports: [SolesPipe],
  templateUrl: './emitir-cpe-lote-dialog.component.html',
  styleUrl: './emitir-cpe-lote-dialog.component.scss',
})
export class EmitirCpeLoteDialogComponent {
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly ecosistemaApi = inject(EcosistemaApiService);

  readonly pedidos = input.required<PedidoDigital[]>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly enviando = signal(false);
  readonly emision = signal<EmisionSimulada | null>(null);
  readonly tipoSeleccionado = signal<'BOLETA' | 'FACTURA' | 'NOTA_VENTA'>('BOLETA');
  readonly tipos = TIPOS_COMPROBANTE_POS;
  readonly etiquetas = ETIQUETAS_COMPROBANTE;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_EMISION;
  readonly numeroDe = numeroComprobante;
  readonly puedeXml = puedeDescargarXml;
  readonly puedeCdr = puedeDescargarCdr;
  readonly puedePdf = puedeAbrirPdf;
  readonly etiquetaCantidad = etiquetaCantidadPedidos;

  readonly total = computed(() =>
    round2(this.pedidos().reduce((sum, pedido) => sum + pedido.total, 0)),
  );

  readonly clienteNombre = computed(() => this.pedidos()[0]?.clienteNombre ?? 'Cliente');

  readonly lineas = computed(() =>
    this.pedidos().flatMap((pedido) =>
      pedido.detalles.map((detalle) => ({
        pedidoCodigo: pedido.codigo,
        descripcion:
          detalle.cantidad > 1
            ? `${detalle.descripcion.trim()} x${detalle.cantidad}`
            : detalle.descripcion.trim(),
        total: detalle.total,
      })),
    ),
  );

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.enviando()) {
      this.cancelled.emit();
    }
  }

  setTipo(valor: string): void {
    if (valor === 'BOLETA' || valor === 'FACTURA' || valor === 'NOTA_VENTA') {
      this.tipoSeleccionado.set(valor);
    }
  }

  async emitir(): Promise<void> {
    const pedidos = this.pedidos();
    if (pedidos.length === 0) {
      this.error.set('Selecciona al menos un pedido entregado.');
      return;
    }

    this.error.set('');
    this.enviando.set(true);
    try {
      const resultado = await this.pedidosApi.emitirComprobanteConsolidado(
        pedidos.map((pedido) => pedido.id),
        this.tipoSeleccionado(),
      );
      this.emision.set(mapEmisionConsolidada(resultado, pedidos, this.total()));
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo emitir el comprobante.');
    } finally {
      this.enviando.set(false);
    }
  }

  async descargarXml(): Promise<void> {
    const emision = this.emision();
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
    const emision = this.emision();
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
    const emision = this.emision();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.abrirPdf(emision, formato);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo abrir el PDF.');
    }
  }
}

function mapEmisionConsolidada(
  resultado: ComprobanteConsolidadoApi,
  pedidos: PedidoDigital[],
  totalFallback: number,
): EmisionSimulada {
  const comprobante = resultado.comprobante;
  return {
    id: comprobante.id,
    tipo: comprobante.tipo,
    serie: comprobante.serie,
    correlativo: comprobante.correlativo,
    estado: comprobante.estado as EstadoEmisionSunat,
    pedidoId: pedidos[0]?.id ?? '',
    ventaId: resultado.ventaId,
    clienteNombre: comprobante.clienteNombre ?? pedidos[0]?.clienteNombre ?? '',
    total: comprobante.total ?? totalFallback,
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
