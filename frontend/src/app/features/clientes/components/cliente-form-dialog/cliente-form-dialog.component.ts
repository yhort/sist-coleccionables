import { DatePipe } from '@angular/common';
import { Component, HostListener, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ClientesApiService } from '../../data-access/clientes.service';
import {
  Cliente,
  ETIQUETAS_DOCUMENTO,
  TIPOS_DOCUMENTO,
  TipoDocumentoIdentidad,
  validarDocumento,
} from '../../models/cliente.model';
import {
  CANALES_CONTACTO,
  CanalContactoCliente,
  ETIQUETAS_CANAL_CONTACTO,
  PUNTOS_ENTREGA_SUGERIDOS,
} from '../../../../shared/models/contacto-entrega.model';

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
  readonly tipos = TIPOS_DOCUMENTO;
  readonly etiquetas = ETIQUETAS_DOCUMENTO;
  readonly canales = CANALES_CONTACTO;
  readonly etiquetasCanal = ETIQUETAS_CANAL_CONTACTO;
  readonly puntosSugeridos = PUNTOS_ENTREGA_SUGERIDOS;

  readonly form = this.fb.nonNullable.group({
    nombre: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(160)]],
    telefono: [''],
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

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    const raw = this.form.getRawValue();
    const docError = validarDocumento(raw.tipoDocumento, raw.numeroDocumento, raw.esPublicoGeneral);
    if (this.form.invalid || docError) {
      this.error.set(docError ?? 'Completa los datos del cliente.');
      return;
    }

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
    }
  }
}
