import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CajaApiService } from '../../data-access/caja.service';
import { CajaAperturaDialogComponent } from '../caja-apertura-dialog/caja-apertura-dialog.component';
import { CajaArqueoDialogComponent } from '../caja-arqueo-dialog/caja-arqueo-dialog.component';

@Component({
  selector: 'app-caja-turno-banner',
  imports: [
    CurrencyPipe,
    DatePipe,
    RouterLink,
    CajaAperturaDialogComponent,
    CajaArqueoDialogComponent,
  ],
  templateUrl: './caja-turno-banner.component.html',
  styleUrl: './caja-turno-banner.component.scss',
})
export class CajaTurnoBannerComponent {
  private readonly cajaApi = inject(CajaApiService);

  readonly sedeId = input.required<string>();
  readonly compacto = input(false);
  readonly changed = output<void>();

  readonly dialog = signal<'apertura' | 'arqueo' | null>(null);

  readonly estado = computed(() => {
    this.cajaApi.estados();
    return this.cajaApi.estadoDe(this.sedeId());
  });

  readonly sedes = this.cajaApi.sedes;

  abrirCaja(): void {
    this.dialog.set('apertura');
  }

  abrirArqueo(): void {
    this.dialog.set('arqueo');
  }

  onDialogSaved(): void {
    this.dialog.set(null);
    this.changed.emit();
  }

  cerrarDialog(): void {
    this.dialog.set(null);
  }
}
