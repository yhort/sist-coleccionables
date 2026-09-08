import { CurrencyPipe } from '@angular/common';
import {
  Component,
  HostListener,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ClientesApiService } from '../../../clientes/data-access/clientes.service';
import {
  CANALES_CONTACTO,
  CanalContactoCliente,
  ETIQUETAS_CANAL_CONTACTO,
  PUNTOS_ENTREGA_SUGERIDOS,
} from '../../../../shared/models/contacto-entrega.model';
import { SubastasTcgApiService } from '../../data-access/subastas-tcg.service';
import {
  ETIQUETAS_METODO_ENVIO_CHECKOUT,
  ETIQUETAS_ORIGEN_PAGO_CHECKOUT,
  METODOS_ENVIO_CHECKOUT,
  ORIGENES_PAGO_CHECKOUT,
  SubastaTcg,
  MetodoEnvioCheckout,
  OrigenPagoCheckout,
  etiquetaLoteSubasta,
  pujaGanadoraActual,
  unidadesLote,
} from '../../models/subasta-tcg.model';

@Component({
  selector: 'app-adjudicar-checkout-dialog',
  imports: [CurrencyPipe, ReactiveFormsModule],
  templateUrl: './adjudicar-checkout-dialog.component.html',
  styleUrl: './adjudicar-checkout-dialog.component.scss',
})
export class AdjudicarCheckoutDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly subastasApi = inject(SubastasTcgApiService);
  private readonly clientesApi = inject(ClientesApiService);

  readonly subasta = input.required<SubastaTcg>();
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly enviando = signal(false);
  readonly metodoEnvio = signal<MetodoEnvioCheckout>('RECOJO_TIENDA');
  readonly tieneCliente = signal(false);
  readonly metodos = METODOS_ENVIO_CHECKOUT;
  readonly origenes = ORIGENES_PAGO_CHECKOUT;
  readonly etiquetasMetodo = ETIQUETAS_METODO_ENVIO_CHECKOUT;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PAGO_CHECKOUT;
  readonly canalesContacto = CANALES_CONTACTO;
  readonly etiquetasCanalContacto = ETIQUETAS_CANAL_CONTACTO;
  readonly puntosSugeridos = PUNTOS_ENTREGA_SUGERIDOS;

  readonly form = this.fb.nonNullable.group({
    origenPagoPreferido: this.fb.nonNullable.control<OrigenPagoCheckout>('YAPE'),
    metodoEnvio: this.fb.nonNullable.control<MetodoEnvioCheckout>('RECOJO_TIENDA'),
    destinatarioNombre: [''],
    destinatarioTelefono: [''],
    puntoEntrega: [''],
    canalContacto: this.fb.control<CanalContactoCliente | ''>(''),
    contactoReferencia: [''],
    entregaDireccion: [''],
    entregaDistrito: [''],
    entregaProvincia: ['Lima'],
    entregaDepartamento: ['Lima'],
    agencia: [''],
    guardarPuntoEnCliente: [false],
  });

  constructor() {
    effect(() => {
      const ganadora = pujaGanadoraActual(this.subasta());
      if (!ganadora) {
        return;
      }
      if (!this.form.controls.destinatarioNombre.value) {
        this.form.patchValue({ destinatarioNombre: ganadora.nombrePostor });
      }
      void this.cargarPreferenciasCliente(ganadora.clienteId);
    });

    this.form.controls.metodoEnvio.valueChanges.subscribe((metodo) => {
      this.metodoEnvio.set(metodo);
      const dir = this.form.controls.entregaDireccion;
      if (metodo === 'RECOJO_TIENDA') {
        dir.clearValidators();
      } else {
        dir.setValidators([Validators.required, Validators.minLength(5)]);
      }
      dir.updateValueAndValidity({ emitEvent: false });
    });
  }

  ganadora() {
    return pujaGanadoraActual(this.subasta());
  }

  lote() {
    return etiquetaLoteSubasta(this.subasta());
  }

  unidades() {
    return unidadesLote(this.subasta());
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.enviando()) {
      this.cancelled.emit();
    }
  }

  async confirmar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      this.error.set('Completa los datos de entrega requeridos.');
      return;
    }

    const raw = this.form.getRawValue();
    const recojo = raw.metodoEnvio === 'RECOJO_TIENDA';
    const punto = raw.puntoEntrega.trim() || null;
    this.enviando.set(true);
    try {
      await this.subastasApi.adjudicar(this.subasta().id, {
        metodoEnvio: raw.metodoEnvio,
        origenPagoPreferido: raw.origenPagoPreferido,
        destinatarioNombre: raw.destinatarioNombre.trim() || this.ganadora()?.nombrePostor,
        destinatarioTelefono: raw.destinatarioTelefono.trim() || null,
        entregaDireccion: recojo ? null : raw.entregaDireccion.trim(),
        entregaDistrito: recojo ? null : raw.entregaDistrito.trim() || null,
        entregaProvincia: recojo ? null : raw.entregaProvincia.trim() || null,
        entregaDepartamento: recojo ? null : raw.entregaDepartamento.trim() || null,
        agencia: recojo ? null : raw.agencia.trim() || punto,
        puntoEntrega: punto,
        canalContacto: raw.canalContacto || null,
        contactoReferencia: raw.contactoReferencia.trim() || null,
        guardarPuntoEnCliente: Boolean(raw.guardarPuntoEnCliente && this.tieneCliente()),
      });
      this.confirmed.emit();
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.enviando.set(false);
    }
  }

  private async cargarPreferenciasCliente(clienteId: string | null | undefined): Promise<void> {
    if (!clienteId) {
      this.tieneCliente.set(false);
      return;
    }
    this.tieneCliente.set(true);
    try {
      const cliente = await this.clientesApi.obtener(clienteId);
      if (!cliente) {
        return;
      }
      this.form.patchValue({
        destinatarioTelefono: this.form.controls.destinatarioTelefono.value || cliente.telefono || '',
        puntoEntrega: this.form.controls.puntoEntrega.value || cliente.puntoEntregaPreferido || '',
        canalContacto: this.form.controls.canalContacto.value || cliente.canalContacto || '',
        contactoReferencia:
          this.form.controls.contactoReferencia.value || cliente.contactoReferencia || '',
      });
    } catch {
      // Autofill best-effort.
    }
  }
}
