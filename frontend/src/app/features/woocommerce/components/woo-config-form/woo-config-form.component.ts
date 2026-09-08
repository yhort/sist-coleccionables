import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { WooCommerceApiService } from '../../data-access/woocommerce.service';
import { ETIQUETAS_CONEXION_WOO } from '../../models/woocommerce.model';

@Component({
  selector: 'app-woo-config-form',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './woo-config-form.component.html',
  styleUrl: './woo-config-form.component.scss',
})
export class WooConfigFormComponent {
  private readonly fb = inject(FormBuilder);
  readonly wooApi = inject(WooCommerceApiService);

  readonly etiquetas = ETIQUETAS_CONEXION_WOO;
  readonly sedes = this.wooApi.sedes;
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    urlTienda: ['', [Validators.required]],
    consumerKey: ['', [Validators.required]],
    consumerSecret: [''],
    sedeOrigenId: ['', Validators.required],
  });

  constructor() {
    effect(() => {
      const config = this.wooApi.config();
      const sedes = this.sedes();
      this.form.patchValue({
        urlTienda: config.urlTienda,
        consumerKey: config.consumerKey,
        consumerSecret: config.consumerSecret,
        sedeOrigenId: config.sedeOrigenId || sedes[0]?.id || '',
      });
    });
  }

  async guardarYProbar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }
    try {
      await this.wooApi.guardarConfig(this.form.getRawValue());
      await this.wooApi.probarConexion();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo probar la conexión.');
    }
  }
}
