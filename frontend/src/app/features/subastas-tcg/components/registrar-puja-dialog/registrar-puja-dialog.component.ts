import {
  Component,
  HostListener,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { SubastasTcgApiService } from '../../data-access/subastas-tcg.service';
import { SubastaTcg, montoMinimoSiguiente, subastaVencida } from '../../models/subasta-tcg.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

@Component({
  selector: 'app-registrar-puja-dialog',
  imports: [SolesPipe, ReactiveFormsModule],
  templateUrl: './registrar-puja-dialog.component.html',
  styleUrl: './registrar-puja-dialog.component.scss',
})
export class RegistrarPujaDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly subastasApi = inject(SubastasTcgApiService);

  readonly subasta = input.required<SubastaTcg>();
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly minimo = signal(0);

  readonly form = this.fb.nonNullable.group({
    nombrePostor: ['', [Validators.required, Validators.minLength(2)]],
    monto: [0, [Validators.required, Validators.min(0.01)]],
  });

  ngOnInit(): void {
    const minimo = montoMinimoSiguiente(this.subasta());
    this.minimo.set(minimo);
    this.form.controls.monto.setValue(minimo);
    this.form.controls.monto.setValidators([Validators.required, Validators.min(minimo)]);
    this.form.controls.monto.updateValueAndValidity();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  async guardar(): Promise<void> {
    if (subastaVencida(this.subasta())) {
      this.error.set('La subasta ha finalizado y no acepta más pujas.');
      return;
    }
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }
    try {
      const raw = this.form.getRawValue();
      await this.subastasApi.registrarPuja(this.subasta().id, {
        nombrePostor: raw.nombrePostor,
        monto: Number(raw.monto),
      });
      this.saved.emit();
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }
}
