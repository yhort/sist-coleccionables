import { JsonPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { WooConfigFormComponent } from '../../components/woo-config-form/woo-config-form.component';
import { WooMapeosTableComponent } from '../../components/woo-mapeos-table/woo-mapeos-table.component';
import { WooSyncLogsComponent } from '../../components/woo-sync-logs/woo-sync-logs.component';
import { WooCommerceApiService } from '../../data-access/woocommerce.service';
import { FilaCsvWoo } from '../../models/woocommerce.model';

@Component({
  selector: 'app-woocommerce-page',
  imports: [
    JsonPipe,
    RouterLink,
    WooConfigFormComponent,
    WooMapeosTableComponent,
    WooSyncLogsComponent,
  ],
  templateUrl: './woocommerce-page.component.html',
  styleUrl: './woocommerce-page.component.scss',
})
export class WooCommercePageComponent {
  readonly wooApi = inject(WooCommerceApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly error = signal('');
  readonly aviso = signal('');
  readonly ultimoCsv = signal<FilaCsvWoo[]>([]);
  readonly ultimaReferencia = signal<string | null>(null);

  constructor() {
    this.destroyRef.onDestroy(() => this.wooApi.detenerPolling());
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    try {
      await this.wooApi.cargar();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo cargar WooCommerce.');
    }
  }

  async sincronizarCatalogo(): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    try {
      const resultado = await this.wooApi.sincronizarCatalogo();
      this.ultimoCsv.set(resultado.filasCsv);
      this.aviso.set(
        `Catálogo: ${resultado.enviadas} SKU publicados, ${resultado.omitidas} omitidos, ${resultado.errores} error(es).`,
      );
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo sincronizar el catálogo.');
    }
  }

  async sincronizarStock(): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    try {
      const resultado = await this.wooApi.sincronizarStock();
      this.aviso.set(
        `Stock (CantidadLibre): ${resultado.publicados} SKU publicados, ${resultado.omitidos} omitidos, ${resultado.errores} error(es).`,
      );
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo sincronizar el stock.');
    }
  }

  async importarPedidos(): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    try {
      const resultado = await this.wooApi.importarPedidosPendientes();
      this.ultimaReferencia.set(resultado.referencias[0] ?? null);
      this.aviso.set(mensajeImportacion(resultado.importados, resultado.errores, 'Polling'));
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudieron importar los pedidos.');
    }
  }

  async simularWebhook(): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    try {
      const resultado = await this.wooApi.simularWebhook();
      this.ultimaReferencia.set(resultado.referencias[0] ?? null);
      this.aviso.set(mensajeImportacion(resultado.importados, resultado.errores, 'Webhook'));
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo simular el webhook.');
    }
  }

  async reintentarWebhooks(): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    try {
      const resultado = await this.wooApi.reintentarWebhooks();
      this.aviso.set(mensajeImportacion(resultado.importados, resultado.errores, 'Reintento'));
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudieron reintentar los webhooks.');
    }
  }

  togglePolling(): void {
    this.error.set('');
    try {
      if (this.wooApi.pollingActivo()) {
        this.wooApi.detenerPolling();
        this.aviso.set('Polling detenido.');
        return;
      }
      this.wooApi.iniciarPolling();
      this.aviso.set('Polling cada 7 s. Los pedidos web entran al Kanban (Pendiente de pago o Pagado).');
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo iniciar el polling.');
    }
  }
}

function mensajeImportacion(importados: number, errores: number, canal: string): string {
  if (importados === 0 && errores === 0) {
    return `${canal}: no había pedidos nuevos en la cola de WooCommerce.`;
  }
  return `${canal}: ${importados} pedido(s) insertados en Pedidos Digitales (WOOCOMMERCE). ${errores} error(es).`;
}
