import { Component, HostListener, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

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
import { PagosApiService } from '../../../pagos/data-access/pagos.service';
import {
  ETIQUETAS_ORIGEN_PAGO,
  ORIGENES_PAGO,
  OrigenPago,
  esOrigenDigital,
} from '../../../pagos/models/pago.model';
import {
  ComprobanteConsolidadoApi,
  PedidosDigitalesApiService,
} from '../../data-access/pedidos-digitales.service';
import { PedidoDigital, round2 } from '../../models/pedido-digital.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-registrar-pago-lote-dialog',
  imports: [SolesPipe, ReactiveFormsModule],
  templateUrl: './registrar-pago-lote-dialog.component.html',
  styleUrl: './registrar-pago-lote-dialog.component.scss',
})
export class RegistrarPagoLoteDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly pagosApi = inject(PagosApiService);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly ecosistemaApi = inject(EcosistemaApiService);

  readonly pedidos = input.required<PedidoDigital[]>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly aviso = signal('');
  readonly enviando = signal(false);
  readonly emision = signal<EmisionSimulada | null>(null);
  readonly pagoConfirmadoSinCpe = signal(false);
  readonly origenes = ORIGENES_PAGO;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PAGO;
  readonly tipos = TIPOS_COMPROBANTE_POS;
  readonly etiquetasCpe = ETIQUETAS_COMPROBANTE;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_EMISION;
  readonly numeroDe = numeroComprobante;
  readonly puedeXml = puedeDescargarXml;
  readonly puedeCdr = puedeDescargarCdr;
  readonly puedePdf = puedeAbrirPdf;

  readonly form = this.fb.nonNullable.group({
    origen: this.fb.nonNullable.control<OrigenPago>('YAPE'),
    codigoOperacion: [''],
    referenciaExterna: [''],
    observacion: [''],
    emitirComprobante: this.fb.nonNullable.control(false),
    tipoComprobante: this.fb.nonNullable.control<'BOLETA' | 'FACTURA' | 'NOTA_VENTA'>('BOLETA'),
  });

  readonly total = computed(() => round2(this.pedidos().reduce((sum, pedido) => sum + pedido.total, 0)));

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
      if (this.emision() || this.pagoConfirmadoSinCpe()) {
        this.saved.emit();
        return;
      }
      this.cancelled.emit();
    }
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    this.aviso.set('');
    const pedidos = this.pedidos();
    if (pedidos.length === 0) {
      this.error.set('Selecciona al menos un pedido pendiente de pago.');
      return;
    }

    const raw = this.form.getRawValue();
    if (esOrigenDigital(raw.origen) && !raw.codigoOperacion.trim()) {
      this.error.set('Indica el código de operación Yape, Plin, Izipay o tarjeta.');
      return;
    }

    this.enviando.set(true);
    try {
      await this.pagosApi.registrarLote({
        origen: raw.origen,
        monto: this.total(),
        codigoOperacion: raw.codigoOperacion,
        referenciaExterna: raw.referenciaExterna,
        observacion: raw.observacion,
        confirmar: true,
        pedidoDigitalIds: pedidos.map((pedido) => pedido.id),
      });

      if (!raw.emitirComprobante) {
        this.saved.emit();
        return;
      }

      try {
        const resultado = await this.pedidosApi.emitirComprobanteConsolidado(
          pedidos.map((pedido) => pedido.id),
          raw.tipoComprobante,
        );
        this.emision.set(mapEmisionConsolidada(resultado, pedidos, this.total()));
      } catch (err) {
        this.aviso.set(
          'Pago registrado y pedidos en Pagado. No se pudo emitir el CPE: ' +
            (err instanceof Error ? err.message : 'error desconocido.'),
        );
        this.pagoConfirmadoSinCpe.set(true);
      }
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo registrar el pago.');
    } finally {
      this.enviando.set(false);
    }
  }

  cerrarTrasEmision(): void {
    this.saved.emit();
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
