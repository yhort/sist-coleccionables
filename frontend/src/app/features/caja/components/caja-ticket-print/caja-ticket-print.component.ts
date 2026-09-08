import { CurrencyPipe } from '@angular/common';
import { Component, HostListener, input, output } from '@angular/core';

import {
  CajaTicketReporte,
  ETIQUETAS_ESTADO_CAJA,
  ETIQUETAS_MOVIMIENTO_CAJA,
} from '../../models/caja.model';

@Component({
  selector: 'app-caja-ticket-print',
  imports: [CurrencyPipe],
  templateUrl: './caja-ticket-print.component.html',
  styleUrl: './caja-ticket-print.component.scss',
})
export class CajaTicketPrintComponent {
  readonly ticket = input.required<CajaTicketReporte>();
  readonly closed = output<void>();

  readonly etiquetasEstado = ETIQUETAS_ESTADO_CAJA;
  readonly etiquetasMovimiento = ETIQUETAS_MOVIMIENTO_CAJA;

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }

  imprimir(): void {
    globalThis.print();
  }
}
