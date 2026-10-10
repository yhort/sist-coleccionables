import { Component, HostListener, OnInit, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ClientesApiService } from '../../data-access/clientes.service';
import {
  Cliente,
  ETIQUETAS_DOCUMENTO,
  TELEFONO_MAX_DIGITOS,
  TIPOS_DOCUMENTO,
  TipoDocumentoIdentidad,
  documentoDuplicadoEnLista,
  nombreDuplicadoSinDocumentoEnLista,
  sanitizarTelefono,
  validarDocumento,
  validarTelefono,
} from '../../models/cliente.model';
import {
  CANALES_CONTACTO,
  CanalContactoCliente,
  ETIQUETAS_CANAL_CONTACTO,
  combinarPuntosEntrega,
  leerPuntosEntregaLocales,
  normalizarPuntoEntrega,
  registrarPuntoEntregaLocal,
} from '../../../../shared/models/contacto-entrega.model';

function telefonoValidator(control: AbstractControl): ValidationErrors | null {
  const error = validarTelefono(control.value);
  return error ? { telefonoInvalido: error } : null;
}

@Component({
  selector: 'app-cliente-form-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './cliente-form-dialog.component.html',
  styleUrl: './cliente-form-dialog.component.scss',
})
export class ClienteFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ClientesApiService);

  readonly cliente = input<Cliente | null>(null);
  readonly saved = output<Cliente>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly guardando = signal(false);
  readonly tipos = TIPOS_DOCUMENTO;
  readonly etiquetas = ETIQUETAS_DOCUMENTO;
  readonly canales = CANALES_CONTACTO;
  readonly etiquetasCanal = ETIQUETAS_CANAL_CONTACTO;
  readonly puntosSugeridos = signal<string[]>(combinarPuntosEntrega(leerPuntosEntregaLocales()));
  readonly telefonoMax = TELEFONO_MAX_DIGITOS;

  readonly form = this.fb.nonNullable.group({
    nombre: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(160)]],
    telefono: ['', [telefonoValidator]],
    puntoEntregaPreferido: [''],
    canalContacto: this.fb.control<CanalContactoCliente | ''>(''),
    contactoReferencia: [''],
    tipoDocumento: this.fb.nonNullable.control<TipoDocumentoIdentidad>('DNI'),
    numeroDocumento: [''],
    esPublicoGeneral: [false],
  });

  ngOnInit(): void {
    const actual = this.cliente();
    if (actual) {
      const sinDocumento =
        actual.esPublicoGeneral ||
        actual.tipoDocumento === 'SIN_DOCUMENTO' ||
        !actual.numeroDocumento;
      this.form.patchValue({
        nombre: actual.nombre,
        telefono: actual.telefono ?? '',
        puntoEntregaPreferido: actual.puntoEntregaPreferido ?? '',
        canalContacto: actual.canalContacto ?? '',
        contactoReferencia: actual.contactoReferencia ?? '',
        tipoDocumento: actual.esPublicoGeneral ? 'SIN_DOCUMENTO' : actual.tipoDocumento,
        numeroDocumento: sinDocumento ? '' : (actual.numeroDocumento ?? ''),
        esPublicoGeneral: actual.esPublicoGeneral,
      });
      if (actual.esPublicoGeneral) {
        this.form.controls.tipoDocumento.disable({ emitEvent: false });
        this.form.controls.numeroDocumento.disable({ emitEvent: false });
      } else {
        this.sincronizarCampoNumero();
      }
    }
    void this.cargarPuntosEntrega(actual?.puntoEntregaPreferido);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  onPublicoGeneral(activo: boolean): void {
    if (activo) {
      this.form.patchValue({
        nombre: 'CLIENTES VARIOS',
        tipoDocumento: 'SIN_DOCUMENTO',
        numeroDocumento: '',
      });
      this.form.controls.tipoDocumento.disable({ emitEvent: false });
      this.form.controls.numeroDocumento.disable({ emitEvent: false });
    } else {
      this.form.controls.tipoDocumento.enable({ emitEvent: false });
      this.sincronizarCampoNumero();
    }
  }

  onTipoDocumentoChange(): void {
    this.sincronizarCampoNumero();
  }

  /** Sin documento / público general: deshabilita y limpia el número (se envía null). */
  private sincronizarCampoNumero(): void {
    const raw = this.form.getRawValue();
    const sinDoc = raw.esPublicoGeneral || raw.tipoDocumento === 'SIN_DOCUMENTO';
    if (sinDoc) {
      this.form.controls.numeroDocumento.setValue('', { emitEvent: false });
      this.form.controls.numeroDocumento.disable({ emitEvent: false });
    } else {
      this.form.controls.numeroDocumento.enable({ emitEvent: false });
    }
  }

  onTelefonoInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const limpio = sanitizarTelefono(input.value);
    if (input.value !== limpio) {
      input.value = limpio;
      this.form.controls.telefono.setValue(limpio, { emitEvent: false });
    }
  }

  onPuntoEntregaBlur(): void {
    const punto = normalizarPuntoEntrega(this.form.controls.puntoEntregaPreferido.value);
    if (!punto) {
      return;
    }
    this.form.controls.puntoEntregaPreferido.setValue(punto, { emitEvent: false });
    this.incorporarPunto(punto);
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    const raw = this.form.getRawValue();
    const docError = validarDocumento(raw.tipoDocumento, raw.numeroDocumento, raw.esPublicoGeneral);
    const telError = validarTelefono(raw.telefono);
    if (this.form.invalid || docError || telError) {
      this.error.set(telError ?? docError ?? 'Completa los datos del cliente.');
      return;
    }

    const sinDocumento =
      raw.esPublicoGeneral ||
      raw.tipoDocumento === 'SIN_DOCUMENTO' ||
      !raw.numeroDocumento?.trim();

    if (!raw.esPublicoGeneral) {
      if (!sinDocumento) {
        const duplicadoDoc = documentoDuplicadoEnLista(
          this.api.clientes(),
          raw.numeroDocumento,
          this.cliente()?.id,
        );
        if (duplicadoDoc) {
          this.error.set(
            `El cliente ya se encuentra registrado (${duplicadoDoc.nombre} · ${duplicadoDoc.tipoDocumento} ${duplicadoDoc.numeroDocumento}).`,
          );
          return;
        }
      } else {
        const duplicadoNombre = nombreDuplicadoSinDocumentoEnLista(
          this.api.clientes(),
          raw.nombre,
          this.cliente()?.id,
        );
        if (duplicadoNombre) {
          this.error.set(
            `Ya existe un cliente activo sin documento con el nombre «${duplicadoNombre.nombre}». Edítalo desde el listado para actualizar el punto de entrega u otros datos.`,
          );
          return;
        }
      }
    }

    this.guardando.set(true);
    try {
      const punto = normalizarPuntoEntrega(raw.puntoEntregaPreferido) || null;
      const cliente = await this.api.guardar(
        {
          nombre: raw.nombre.trim().replace(/\s+/g, ' '),
          telefono: raw.telefono || null,
          puntoEntregaPreferido: punto,
          canalContacto: raw.canalContacto || null,
          contactoReferencia: raw.contactoReferencia || null,
          tipoDocumento: sinDocumento ? 'SIN_DOCUMENTO' : raw.tipoDocumento,
          numeroDocumento: sinDocumento ? null : raw.numeroDocumento.trim() || null,
          esPublicoGeneral: raw.esPublicoGeneral,
        },
        this.cliente()?.id,
      );
      if (punto) {
        this.incorporarPunto(punto);
      }
      this.saved.emit(cliente);
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.guardando.set(false);
    }
  }

  private async cargarPuntosEntrega(puntoActual?: string | null): Promise<void> {
    const desdeApi = await this.api.listarPuntosEntrega();
    const desdeClientes = this.api
      .clientes()
      .map((item) => item.puntoEntregaPreferido)
      .filter((item): item is string => !!item?.trim());
    this.puntosSugeridos.set(
      combinarPuntosEntrega(
        leerPuntosEntregaLocales(),
        desdeApi,
        desdeClientes,
        puntoActual ? [puntoActual] : [],
      ),
    );
  }

  private incorporarPunto(punto: string): void {
    registrarPuntoEntregaLocal(punto);
    this.puntosSugeridos.update((actual) => combinarPuntosEntrega(actual, [punto]));
  }
}
