import { DatePipe } from '@angular/common';
import { Component, HostListener, effect, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { EcosistemaApiService } from '../../../ecosistema/data-access/ecosistema.service';
import {
  ETIQUETAS_COMPROBANTE,
  ETIQUETAS_ESTADO_EMISION,
  EmisionSimulada,
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
import { PedidoDigital } from '../../models/pedido-digital.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-pedido-accion-preparada-dialog',
  imports: [RouterLink, SolesPipe, DatePipe],
  templateUrl: './pedido-accion-preparada-dialog.component.html',
  styleUrl: './pedido-accion-preparada-dialog.component.scss',
})
export class PedidoAccionPreparadaDialogComponent {
  private readonly ecosistemaApi = inject(EcosistemaApiService);

  readonly tipo = input.required<'pago' | 'pago-consulta' | 'cpe'>();
  readonly pedido = input<PedidoDigital | null>(null);
  readonly pagos = input<Pago[]>([]);
  readonly closed = output<void>();

  readonly error = signal('');
  readonly enviando = signal(false);
  readonly emision = signal<EmisionSimulada | null>(null);
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

  constructor() {
    effect(() => {
      const ventaId = this.tipo() === 'cpe' ? this.pedido()?.ventaId : null;
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
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }

  async emitir(): Promise<void> {
    const ventaId = this.pedido()?.ventaId;
    if (!ventaId) {
      this.error.set('El pedido no tiene una venta confirmada.');
      return;
    }
    this.error.set('');
    this.enviando.set(true);
    try {
      const emision = await this.ecosistemaApi.emitirPrueba({
        tipo: this.tipoSeleccionado(),
        documentoId: ventaId,
      });
      this.emision.set(emision);
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
