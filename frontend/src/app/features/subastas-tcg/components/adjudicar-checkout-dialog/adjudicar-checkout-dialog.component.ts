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
import { Cliente } from '../../../clientes/models/cliente.model';
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
  esEventoIndividuales,
  etiquetaLoteSubasta,
  nombreVisibleLinea,
  pujaGanadoraActual,
  unidadesLote,
} from '../../models/subasta-tcg.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-adjudicar-checkout-dialog',
  imports: [SolesPipe, ReactiveFormsModule],
  templateUrl: './adjudicar-checkout-dialog.component.html',
  styleUrl: './adjudicar-checkout-dialog.component.scss',
})
export class AdjudicarCheckoutDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly subastasApi = inject(SubastasTcgApiService);
  private readonly clientesApi = inject(ClientesApiService);

  readonly subasta = input.required<SubastaTcg>();
  /** En eventos individuales: carta a adjudicar. */
  readonly subastaDetalleId = input<string | null>(null);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly enviando = signal(false);
  readonly metodoEnvio = signal<MetodoEnvioCheckout>('RECOJO_TIENDA');
  readonly tieneCliente = signal(false);
  /** Adjudicación directa (sin historial de pujas). */
  readonly nombrePostorDirecto = signal('');
  readonly clienteIdDirecto = signal<string | null>(null);
  readonly montoDirecto = signal(0);
  readonly sugerenciasCliente = signal<Cliente[]>([]);
  readonly metodos = METODOS_ENVIO_CHECKOUT;
  readonly origenes = ORIGENES_PAGO_CHECKOUT;
  readonly etiquetasMetodo = ETIQUETAS_METODO_ENVIO_CHECKOUT;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PAGO_CHECKOUT;
  readonly canalesContacto = CANALES_CONTACTO;
  readonly etiquetasCanalContacto = ETIQUETAS_CANAL_CONTACTO;
  readonly puntosSugeridos = PUNTOS_ENTREGA_SUGERIDOS;
  readonly nombreVisibleLinea = nombreVisibleLinea;

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
      const ganadora = this.ganadora();
      if (!ganadora) {
        const base = this.subasta().precioBase;
        if (this.montoDirecto() < base) {
          this.montoDirecto.set(base);
        }
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

  requiereAdjudicacionDirecta(): boolean {
    return !this.ganadora();
  }

  lineaSeleccionada() {
    const id = this.subastaDetalleId();
    if (!id) {
      return null;
    }
    return this.subasta().detalles.find((d) => d.id === id) ?? null;
  }

  ganadora() {
    return pujaGanadoraActual(this.subasta(), this.subastaDetalleId());
  }

  /** Descripción del lote / carta (secundaria al título del evento). */
  resumenLote() {
    const linea = this.lineaSeleccionada();
    if (linea && esEventoIndividuales(this.subasta())) {
      return nombreVisibleLinea(linea);
    }
    return etiquetaLoteSubasta(this.subasta());
  }

  unidades() {
    const linea = this.lineaSeleccionada();
    if (linea && esEventoIndividuales(this.subasta())) {
      return linea.cantidad;
    }
    return unidadesLote(this.subasta());
  }

  async buscarGanador(texto: string): Promise<void> {
    this.clienteIdDirecto.set(null);
    this.tieneCliente.set(false);
    this.nombrePostorDirecto.set(texto);
    this.form.controls.destinatarioNombre.setValue(texto, { emitEvent: false });

    const query = texto.trim();
    if (query.length < 2) {
      this.sugerenciasCliente.set([]);
      return;
    }

    try {
      const hits = await this.clientesApi.buscar(query);
      const q = query.toLowerCase();
      this.sugerenciasCliente.set(
        hits.filter(
          (c) =>
            c.nombre.toLowerCase().includes(q) ||
            (c.telefono ?? '').includes(query) ||
            (c.contactoReferencia ?? '').toLowerCase().includes(q),
        ),
      );
    } catch {
      this.sugerenciasCliente.set([]);
    }
  }

  elegirClienteGanador(cliente: Cliente): void {
    this.clienteIdDirecto.set(cliente.id);
    this.tieneCliente.set(true);
    this.nombrePostorDirecto.set(cliente.nombre);
    this.sugerenciasCliente.set([]);
    this.form.patchValue({
      destinatarioNombre: cliente.nombre,
      destinatarioTelefono: cliente.telefono || '',
      puntoEntrega: cliente.puntoEntregaPreferido || '',
      canalContacto: cliente.canalContacto || '',
      contactoReferencia: cliente.contactoReferencia || '',
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.sugerenciasCliente().length) {
      this.sugerenciasCliente.set([]);
      return;
    }
    if (!this.enviando()) {
      this.cancelled.emit();
    }
  }

  async confirmar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    this.sugerenciasCliente.set([]);
    if (this.form.invalid) {
      this.error.set('Completa los datos de entrega requeridos.');
      return;
    }

    if (esEventoIndividuales(this.subasta()) && !this.subastaDetalleId()) {
      this.error.set('Selecciona la carta a adjudicar.');
      return;
    }

    const directa = this.requiereAdjudicacionDirecta();
    if (directa) {
      const nombre =
        this.nombrePostorDirecto().trim() || this.form.controls.destinatarioNombre.value.trim();
      const monto = Number(this.montoDirecto());
      if (nombre.length < 2) {
        this.error.set('Indica el nombre del ganador para adjudicar sin pujas previas.');
        return;
      }
      if (!Number.isFinite(monto) || monto < this.subasta().precioBase) {
        this.error.set(
          `El monto adjudicado debe ser al menos ${this.subasta().precioBase.toFixed(2)}.`,
        );
        return;
      }
      if (!this.form.controls.destinatarioNombre.value.trim()) {
        this.form.patchValue({ destinatarioNombre: nombre });
      }
    }

    const raw = this.form.getRawValue();
    const recojo = raw.metodoEnvio === 'RECOJO_TIENDA';
    const punto = raw.puntoEntrega.trim() || null;
    this.enviando.set(true);
    try {
      await this.subastasApi.adjudicar(this.subasta().id, {
        subastaDetalleId: this.subastaDetalleId(),
        nombrePostor: directa
          ? this.nombrePostorDirecto().trim() || raw.destinatarioNombre.trim()
          : null,
        clienteId: directa ? this.clienteIdDirecto() : null,
        montoAdjudicado: directa ? Number(this.montoDirecto()) : null,
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
