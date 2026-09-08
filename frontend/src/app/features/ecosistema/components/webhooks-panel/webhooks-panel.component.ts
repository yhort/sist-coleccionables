import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { EcosistemaApiService } from '../../data-access/ecosistema.service';
import {
  ETIQUETAS_EVENTO_WEBHOOK,
  EVENTOS_WEBHOOK,
  EventoWebhook,
} from '../../models/ecosistema.model';

@Component({
  selector: 'app-webhooks-panel',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './webhooks-panel.component.html',
  styleUrl: './webhooks-panel.component.scss',
})
export class WebhooksPanelComponent {
  private readonly fb = inject(FormBuilder);
  readonly api = inject(EcosistemaApiService);
  readonly eventos = EVENTOS_WEBHOOK;
  readonly etiquetas = ETIQUETAS_EVENTO_WEBHOOK;
  readonly aviso = signal('');
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    nombre: ['', Validators.required],
    url: ['', Validators.required],
    token: [''],
    activo: [true],
    pedidoEstado: [true],
    guiaEstado: [true],
  });

  constructor() {
    effect(() => {
      const hook = this.api.webhooks()[0];
      if (!hook) {
        return;
      }
      this.form.patchValue({
        nombre: hook.nombre,
        url: hook.url,
        token: hook.token,
        activo: hook.activo,
        pedidoEstado: hook.eventos.includes('pedido.estado'),
        guiaEstado: hook.eventos.includes('guia.estado'),
      });
    });
  }

  private get hook() {
    return this.api.webhooks()[0] ?? {
      id: '',
      nombre: 'WhatsApp · pedidos y guías',
      url: '',
      token: '',
      canal: 'WHATSAPP' as const,
      eventos: ['pedido.estado'] as EventoWebhook[],
      activo: true,
    };
  }

  async guardar(): Promise<void> {
    this.error.set('');
    try {
      const raw = this.form.getRawValue();
      const eventos: EventoWebhook[] = [];
      if (raw.pedidoEstado) {
        eventos.push('pedido.estado');
      }
      if (raw.guiaEstado) {
        eventos.push('guia.estado');
      }
      await this.api.guardarWebhook({
        ...this.hook,
        nombre: raw.nombre,
        url: raw.url,
        token: raw.token,
        activo: raw.activo,
        eventos,
      });
      this.aviso.set('Webhook de WhatsApp actualizado.');
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar el webhook.');
    }
  }

  async probar(evento: EventoWebhook): Promise<void> {
    this.error.set('');
    try {
      const log = await this.api.probarWebhook(evento);
      this.aviso.set(
        log.estado === 'ENVIADO'
          ? `Notificación de prueba enviada (${evento}).`
          : 'Webhook inactivo u evento no suscrito. Se registró como omitido.',
      );
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo probar el webhook.');
    }
  }
}
