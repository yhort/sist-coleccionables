import { Component, HostListener, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { CatalogoTcgApiService } from '../../data-access/catalogo-tcg.service';
import { TcgSet } from '../../models/catalogo-tcg.model';

@Component({
  selector: 'app-editar-set-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './editar-set-dialog.component.html',
  styleUrl: './editar-set-dialog.component.scss',
})
export class EditarSetDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly catalogoApi = inject(CatalogoTcgApiService);

  readonly set = input.required<TcgSet>();
  readonly saved = output<TcgSet>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly guardando = signal(false);

  readonly codigosBloqueados = computed(() => {
    const actual = this.set();
    if (actual.codigosBloqueados != null) {
      return actual.codigosBloqueados;
    }
    return (actual.cartasCount ?? 0) > 0 || (actual.skusCount ?? 0) > 0;
  });

  readonly form = this.fb.nonNullable.group({
    nombreSerie: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    nombreSet: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    nombreEn: [''],
    codigoSerie: ['', [Validators.required, Validators.maxLength(32)]],
    codigoSet: ['', [Validators.required, Validators.maxLength(32)]],
  });

  ngOnInit(): void {
    const actual = this.set();
    this.form.patchValue({
      nombreSerie: actual.serieNombre,
      nombreSet: actual.nombre,
      nombreEn: actual.nombreEn ?? '',
      codigoSerie: actual.serieCodigo,
      codigoSet: actual.codigo,
    });
    if (this.codigosBloqueados()) {
      this.form.controls.codigoSerie.disable();
      this.form.controls.codigoSet.disable();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.guardando()) {
      this.cancelled.emit();
    }
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      this.error.set('Completa los nombres del set y la serie.');
      return;
    }

    const raw = this.form.getRawValue();
    this.guardando.set(true);
    try {
      const actualizado = await this.catalogoApi.actualizarSet(this.set().id, {
        nombreSerie: raw.nombreSerie.trim(),
        nombreSet: raw.nombreSet.trim(),
        nombreEn: raw.nombreEn.trim() || null,
        codigoSerie: raw.codigoSerie.trim().toUpperCase(),
        codigoSet: raw.codigoSet.trim().toUpperCase(),
      });
      this.saved.emit(actualizado);
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.guardando.set(false);
    }
  }
}
