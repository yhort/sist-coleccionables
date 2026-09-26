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
  sanitizarTelefono,
  validarDocumento,
  validarTelefono,
} from '../../models/cliente.model';
import {
  CANALES_CONTACTO,
  CanalContactoCliente,
  ETIQUETAS_CANAL_CONTACTO,
  PUNTOS_ENTREGA_SUGERIDOS,
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
  readonly puntosSugeridos = PUNTOS_ENTREGA_SUGERIDOS;
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
      this.form.patchValue({
        nombre: actual.nombre,
        telefono: actual.telefono ?? '',
        puntoEntregaPreferido: actual.puntoEntregaPreferido ?? '',
        canalContacto: actual.canalContacto ?? '',
        contactoReferencia: actual.contactoReferencia ?? '',
        tipoDocumento: actual.tipoDocumento,
        numeroDocumento: actual.numeroDocumento ?? '',
        esPublicoGeneral: actual.esPublicoGeneral,
      });
      if (actual.esPublicoGeneral) {
        this.form.controls.tipoDocumento.disable();
        this.form.controls.numeroDocumento.disable();
      }
    }
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
        numeroDocumento: '00000000',
      });
      this.form.controls.tipoDocumento.disable();
      this.form.controls.numeroDocumento.disable();
    } else {
      this.form.controls.tipoDocumento.enable();
      this.form.controls.numeroDocumento.enable();
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

    if (!raw.esPublicoGeneral) {
      const duplicado = documentoDuplicadoEnLista(
        this.api.clientes(),
        raw.numeroDocumento,
        this.cliente()?.id,
      );
      if (duplicado) {
        this.error.set(
          `El cliente ya se encuentra registrado (${duplicado.nombre} · ${duplicado.tipoDocumento} ${duplicado.numeroDocumento}).`,
        );
        return;
      }
    }

    this.guardando.set(true);
    try {
      const cliente = await this.api.guardar(
        {
          nombre: raw.nombre,
          telefono: raw.telefono || null,
          puntoEntregaPreferido: raw.puntoEntregaPreferido || null,
          canalContacto: raw.canalContacto || null,
          contactoReferencia: raw.contactoReferencia || null,
          tipoDocumento: raw.esPublicoGeneral ? 'SIN_DOCUMENTO' : raw.tipoDocumento,
          numeroDocumento: raw.esPublicoGeneral ? '00000000' : raw.numeroDocumento || null,
          esPublicoGeneral: raw.esPublicoGeneral,
        },
        this.cliente()?.id,
      );
      this.saved.emit(cliente);
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.guardando.set(false);
    }
  }
}
