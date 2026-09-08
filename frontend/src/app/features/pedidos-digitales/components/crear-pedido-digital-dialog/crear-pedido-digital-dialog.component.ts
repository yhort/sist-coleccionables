import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';

import { ClientesApiService } from '../../../clientes/data-access/clientes.service';
import {
  Cliente,
  ETIQUETAS_DOCUMENTO,
  TIPOS_DOCUMENTO,
  TipoDocumentoIdentidad,
  etiquetaDocumento,
  validarDocumento,
} from '../../../clientes/models/cliente.model';
import { CajaApiService } from '../../../caja/data-access/caja.service';
import { ProductoTcg, precioVigente } from '../../../productos-tcg/models/producto-tcg.model';
import { PedidosDigitalesApiService } from '../../data-access/pedidos-digitales.service';
import { SelectorProductoCascadaComponent } from '../../../catalogo-tcg/components/selector-producto-cascada/selector-producto-cascada.component';
import {
  CANALES_CONTACTO,
  CanalContactoCliente,
  ETIQUETAS_CANAL_CONTACTO,
  PUNTOS_ENTREGA_SUGERIDOS,
} from '../../../../shared/models/contacto-entrega.model';
import {
  ETIQUETAS_ORIGEN_PAGO,
  ORIGENES_PAGO_POS,
  OrigenPago,
  round2 as roundPago,
} from '../../../pagos/models/pago.model';
import {
  ETIQUETAS_ORIGEN_PEDIDO,
  ORIGENES_PEDIDO,
  OrigenPedidoDigital,
  PedidoDigital,
  canalDesdeOrigen,
  desgloseIgvDesdeTotal,
  round2,
} from '../../models/pedido-digital.model';

export interface CrearPedidoSavedEvent {
  pedido: PedidoDigital;
  imprimirTicket: boolean;
}

@Component({
  selector: 'app-crear-pedido-digital-dialog',
  imports: [ReactiveFormsModule, DecimalPipe, SelectorProductoCascadaComponent],
  templateUrl: './crear-pedido-digital-dialog.component.html',
  styleUrl: './crear-pedido-digital-dialog.component.scss',
})
export class CrearPedidoDigitalDialogComponent implements AfterViewInit {
  private readonly fb = inject(FormBuilder);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly clientesApi = inject(ClientesApiService);
  private readonly cajaApi = inject(CajaApiService);

  readonly saved = output<CrearPedidoSavedEvent>();
  readonly cancelled = output<void>();

  readonly clienteInput = viewChild<ElementRef<HTMLInputElement>>('clienteInput');

  readonly error = signal('');
  readonly guardando = signal(false);
  readonly sugerencias = signal<Cliente[]>([]);
  readonly clienteId = signal<string | null>(null);
  readonly sedes = this.pedidosApi.sedes;
  readonly origenes = ORIGENES_PEDIDO;
  readonly etiquetasOrigen = ETIQUETAS_ORIGEN_PEDIDO;
  readonly tiposDocumento = TIPOS_DOCUMENTO;
  readonly etiquetasDocumento = ETIQUETAS_DOCUMENTO;
  readonly etiquetaDocumento = etiquetaDocumento;
  readonly canalesContacto = CANALES_CONTACTO;
  readonly etiquetasCanalContacto = ETIQUETAS_CANAL_CONTACTO;
  readonly puntosSugeridos = PUNTOS_ENTREGA_SUGERIDOS;
  readonly origenesPagoPos = ORIGENES_PAGO_POS;
  readonly etiquetasOrigenPago = ETIQUETAS_ORIGEN_PAGO;

  readonly form = this.fb.nonNullable.group({
    clienteNombre: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(160)]],
    clienteTelefono: [''],
    tipoDocumento: this.fb.nonNullable.control<TipoDocumentoIdentidad>('SIN_DOCUMENTO'),
    numeroDocumento: [''],
    esClienteVarios: [false],
    sedeId: [this.sedes()[0]?.id ?? '', Validators.required],
    origen: this.fb.nonNullable.control<OrigenPedidoDigital>('TIENDA_PRESENCIAL'),
    observacion: [''],
    esRecojoTienda: [true],
    destinatarioNombre: [''],
    destinatarioTelefono: [''],
    puntoEntrega: [''],
    canalContacto: this.fb.control<CanalContactoCliente | ''>(''),
    contactoReferencia: [''],
    direccion: [''],
    distrito: [''],
    provincia: ['Lima'],
    departamento: ['Lima'],
    courier: [''],
    guardarPuntoEnCliente: [false],
    cobroInmediato: [false],
    metodoPago: this.fb.nonNullable.control<OrigenPago>('EFECTIVO'),
    codigoOperacion: [''],
    montoRecibido: [null as number | null],
    imprimirTicket: [true],
    detalles: this.fb.array([this.nuevaLinea()]),
  });

  get detalles(): FormArray {
    return this.form.controls.detalles;
  }

  readonly totalEstimado = () =>
    round2(
      this.detalles.controls.reduce((sum, control) => {
        const cantidad = Number(control.get('cantidad')?.value) || 0;
        const precio = Number(control.get('precioUnitario')?.value) || 0;
        return sum + cantidad * precio;
      }, 0),
    );

  readonly montos = () => desgloseIgvDesdeTotal(this.totalEstimado());

  readonly esEfectivo = computed(() => this.form.controls.metodoPago.value === 'EFECTIVO');

  readonly vuelto = computed(() => {
    const recibido = Number(this.form.controls.montoRecibido.value);
    const total = this.totalEstimado();
    if (!Number.isFinite(recibido) || recibido <= 0) {
      return null;
    }
    return roundPago(Math.max(0, recibido - total));
  });

  readonly cajaAbierta = computed(() => {
    const sedeId = this.form.controls.sedeId.value;
    return sedeId ? this.cajaApi.estaAbierta(sedeId) : false;
  });

  readonly alertaCaja = computed(() => {
    if (!this.form.controls.cobroInmediato.value) {
      return '';
    }
    const sedeId = this.form.controls.sedeId.value;
    if (!sedeId) {
      return 'Selecciona una sede para cobrar.';
    }
    if (this.cajaApi.estaAbierta(sedeId)) {
      return '';
    }
    return this.cajaApi.mensajeCerrada(sedeId);
  });

  ngAfterViewInit(): void {
    queueMicrotask(() => this.clienteInput()?.nativeElement.focus());
    void this.refrescarCaja();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.guardando()) {
      this.cancelled.emit();
    }
  }

  async refrescarCaja(): Promise<void> {
    const sedeId = this.form.controls.sedeId.value;
    if (!sedeId) {
      return;
    }
    try {
      await this.cajaApi.refrescarEstado(sedeId);
    } catch {
      /* banner de alerta se muestra si sigue cerrada */
    }
  }

  onSedeChange(): void {
    void this.refrescarCaja();
  }

  onCobroToggle(activo: boolean): void {
    if (activo) {
      this.form.patchValue({ origen: 'TIENDA_PRESENCIAL', esRecojoTienda: true });
      if (this.form.controls.metodoPago.value === 'EFECTIVO') {
        this.form.controls.montoRecibido.setValue(this.totalEstimado());
      }
      void this.refrescarCaja();
    }
  }

  onMetodoPagoChange(): void {
    if (this.form.controls.metodoPago.value === 'EFECTIVO') {
      const actual = this.form.controls.montoRecibido.value;
      if (actual == null || Number(actual) <= 0) {
        this.form.controls.montoRecibido.setValue(this.totalEstimado());
      }
    }
  }

  async buscarCliente(texto: string): Promise<void> {
    this.clienteId.set(null);
    const query = texto.trim();
    if (query.length < 2) {
      this.sugerencias.set([]);
      return;
    }
    try {
      this.sugerencias.set(await this.clientesApi.buscar(query));
    } catch {
      this.sugerencias.set([]);
    }
  }

  elegirCliente(cliente: Cliente): void {
    this.clienteId.set(cliente.id);
    this.sugerencias.set([]);
    this.form.patchValue({
      clienteNombre: cliente.nombre,
      clienteTelefono: cliente.telefono ?? '',
      tipoDocumento: cliente.tipoDocumento,
      numeroDocumento: cliente.numeroDocumento ?? '',
      esClienteVarios: cliente.esPublicoGeneral,
      destinatarioNombre: cliente.nombre,
      destinatarioTelefono: cliente.telefono ?? '',
      puntoEntrega: cliente.puntoEntregaPreferido ?? '',
      canalContacto: cliente.canalContacto ?? '',
      contactoReferencia: cliente.contactoReferencia ?? '',
      guardarPuntoEnCliente: false,
    });
  }

  marcarVarios(activo: boolean): void {
    if (activo) {
      this.clienteId.set(null);
      this.form.patchValue({
        clienteNombre: 'CLIENTES VARIOS',
        tipoDocumento: 'SIN_DOCUMENTO',
        numeroDocumento: '00000000',
      });
      this.form.controls.tipoDocumento.disable();
      return;
    }
    this.form.controls.tipoDocumento.enable();
  }

  agregarLinea(): void {
    this.detalles.push(this.nuevaLinea());
  }

  quitarLinea(index: number): void {
    if (this.detalles.length === 1) {
      return;
    }
    this.detalles.removeAt(index);
  }

  onProductoSeleccionado(index: number, producto: ProductoTcg | null): void {
    this.detalles.at(index).patchValue({
      productoId: producto?.id ?? '',
      precioUnitario: producto ? precioVigente(producto) : 0,
    });
    if (
      this.form.controls.cobroInmediato.value &&
      this.form.controls.metodoPago.value === 'EFECTIVO'
    ) {
      this.form.controls.montoRecibido.setValue(this.totalEstimado());
    }
  }

  readonly stockLibreDe = (productoId: string): number => this.stockLibre(productoId);

  stockLibre(productoId: string): number {
    const sedeId = this.form.controls.sedeId.value;
    if (!productoId || !sedeId) {
      return 0;
    }
    return this.pedidosApi.stockLibre(sedeId, productoId);
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid || this.guardando()) {
      return;
    }
    const raw = this.form.getRawValue();
    const docError = validarDocumento(raw.tipoDocumento, raw.numeroDocumento, raw.esClienteVarios);
    if (docError) {
      this.error.set(docError);
      return;
    }

    if (raw.cobroInmediato) {
      await this.refrescarCaja();
      if (!this.cajaApi.estaAbierta(raw.sedeId)) {
        this.error.set(this.cajaApi.mensajeCerrada(raw.sedeId));
        return;
      }
      if (raw.metodoPago === 'EFECTIVO') {
        const recibido = Number(raw.montoRecibido);
        if (!Number.isFinite(recibido) || recibido + 0.001 < this.totalEstimado()) {
          this.error.set('El monto recibido debe cubrir el total de la venta.');
          return;
        }
      }
    }

    this.guardando.set(true);
    try {
      const pedido = await this.pedidosApi.crear({
        clienteId: this.clienteId(),
        clienteNombre: raw.clienteNombre,
        clienteTelefono: raw.clienteTelefono,
        tipoDocumento: raw.tipoDocumento,
        numeroDocumento: raw.numeroDocumento || null,
        esClienteVarios: raw.esClienteVarios,
        sedeId: raw.sedeId,
        canalPedido: canalDesdeOrigen(raw.origen),
        observacion: raw.observacion,
        detalles: raw.detalles.map((linea) => ({
          productoId: linea.productoId,
          cantidad: Number(linea.cantidad),
          precioUnitario: Number(linea.precioUnitario),
        })),
        entrega: {
          destinatarioNombre: raw.destinatarioNombre || raw.clienteNombre,
          destinatarioTelefono: raw.destinatarioTelefono || raw.clienteTelefono || null,
          direccion: raw.direccion || null,
          distrito: raw.distrito || null,
          provincia: raw.provincia || null,
          departamento: raw.departamento || null,
          courier: raw.courier || null,
          esRecojoTienda: raw.esRecojoTienda,
          puntoEntrega: raw.puntoEntrega || null,
          canalContacto: raw.canalContacto || null,
          contactoReferencia: raw.contactoReferencia || null,
          agencia: raw.puntoEntrega || null,
        },
        guardarPuntoEnCliente: Boolean(raw.guardarPuntoEnCliente && this.clienteId()),
        cobroInmediato: raw.cobroInmediato
          ? {
              origen: raw.metodoPago as CobroInmediatoPedidoInput['origen'],
              codigoOperacion: raw.codigoOperacion.trim() || null,
              referenciaExterna: raw.codigoOperacion.trim() || null,
              montoRecibido:
                raw.metodoPago === 'EFECTIVO' ? Number(raw.montoRecibido) : null,
            }
          : null,
      });
      this.saved.emit({
        pedido,
        imprimirTicket: Boolean(raw.cobroInmediato && raw.imprimirTicket),
      });
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo crear el pedido.');
    } finally {
      this.guardando.set(false);
    }
  }

  private nuevaLinea() {
    return this.fb.nonNullable.group({
      productoId: ['', Validators.required],
      cantidad: [1, [Validators.required, Validators.min(1)]],
      precioUnitario: [0, [Validators.required, Validators.min(0)]],
    });
  }
}

type CobroInmediatoPedidoInput = NonNullable<
  import('../../models/pedido-digital.model').CrearPedidoDigitalRequest['cobroInmediato']
>;
