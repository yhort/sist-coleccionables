import { CurrencyPipe } from '@angular/common';
import {
  Component,
  HostListener,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CajaApiService } from '../../data-access/caja.service';
import {
  CajaResumen,
  CajaTicketReporte,
  ETIQUETAS_DIFERENCIA_CAJA,
  ETIQUETAS_MOVIMIENTO_CAJA,
  round2,
  tipoDiferenciaDe,
} from '../../models/caja.model';
import { CajaTicketPrintComponent } from '../caja-ticket-print/caja-ticket-print.component';

@Component({
  selector: 'app-caja-arqueo-dialog',
  imports: [CurrencyPipe, FormsModule, CajaTicketPrintComponent],
  templateUrl: './caja-arqueo-dialog.component.html',
  styleUrl: './caja-arqueo-dialog.component.scss',
})
export class CajaArqueoDialogComponent implements OnInit {
  private readonly cajaApi = inject(CajaApiService);

  readonly sedeId = input.required<string>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly resumen = signal<CajaResumen | null>(null);
  readonly ticket = signal<CajaTicketReporte | null>(null);
  readonly error = signal('');
  readonly cargando = signal(true);
  readonly enviando = signal(false);
  readonly cerrado = signal(false);
  readonly montoReal = signal(0);
  readonly observacion = signal('');

  readonly etiquetasMovimiento = ETIQUETAS_MOVIMIENTO_CAJA;
  readonly etiquetasDiferencia = ETIQUETAS_DIFERENCIA_CAJA;

  readonly teorico = computed(() => this.resumen()?.efectivo.montoEfectivoTeorico ?? 0);
  readonly diferencia = computed(() => round2(this.montoReal() - this.teorico()));
  readonly tipoDiferencia = computed(() => tipoDiferenciaDe(this.diferencia()));

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.ticket()) {
      this.ticket.set(null);
      return;
    }
    this.cancelled.emit();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set('');
    try {
      const resumen = await this.cajaApi.resumenActual(this.sedeId());
      this.resumen.set(resumen);
      this.montoReal.set(resumen.efectivo.montoEfectivoReal ?? resumen.efectivo.montoEfectivoTeorico);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo cargar el arqueo.');
    } finally {
      this.cargando.set(false);
    }
  }

  onMontoReal(valor: string | number | null): void {
    const numero = valor === '' || valor === null ? 0 : Number(valor);
    this.montoReal.set(Number.isFinite(numero) ? round2(numero) : 0);
  }

  async cerrarTurno(): Promise<void> {
    const resumen = this.resumen();
    if (!resumen || this.enviando()) {
      return;
    }
    this.error.set('');
    this.enviando.set(true);
    try {
      const cerrado = await this.cajaApi.cerrar({
        sedeId: this.sedeId(),
        cajaSesionId: resumen.cajaSesionId,
        montoEfectivoReal: this.montoReal(),
        observacion: this.observacion().trim() || null,
      });
      this.resumen.set(cerrado);
      this.cerrado.set(true);
      this.saved.emit();
      await this.imprimir(cerrado.cajaSesionId);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo cerrar la caja.');
    } finally {
      this.enviando.set(false);
    }
  }

  async imprimir(sesionId?: string): Promise<void> {
    const id = sesionId ?? this.resumen()?.cajaSesionId;
    if (!id) {
      return;
    }
    this.error.set('');
    try {
      this.ticket.set(await this.cajaApi.reporteTicket(id));
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo armar el ticket.');
    }
  }

  cerrarTicket(): void {
    this.ticket.set(null);
    if (this.cerrado()) {
      this.cancelled.emit();
    }
  }
}
