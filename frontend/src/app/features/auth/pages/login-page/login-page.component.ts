import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthSessionService, EmpresaLogin } from '../../../../core/auth/auth-session.service';
import { readApiError } from '../../../../core/http/api-error';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss',
})
export class LoginPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthSessionService);
  private readonly router = inject(Router);

  readonly error = signal('');
  readonly enviando = signal(false);
  readonly cargandoEmpresas = signal(true);
  readonly empresas = signal<EmpresaLogin[]>([]);

  readonly form = this.fb.nonNullable.group({
    email: ['', Validators.required],
    password: ['', Validators.required],
    empresaId: [''],
  });

  readonly empresaUnica = computed(() =>
    this.empresas().length === 1 ? this.empresas()[0] : null,
  );
  readonly mostrarSelector = computed(() => this.empresas().length > 1);

  constructor() {
    void this.cargarEmpresas();
  }

  etiquetaEmpresa(empresa: EmpresaLogin): string {
    if (empresa.nombreComercial && empresa.razonSocial && empresa.nombreComercial !== empresa.razonSocial) {
      return `${empresa.nombreComercial} · ${empresa.razonSocial}`;
    }
    return empresa.nombreComercial || empresa.razonSocial;
  }

  async entrar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }
    if (this.mostrarSelector() && !this.form.controls.empresaId.value.trim()) {
      this.error.set('Selecciona la empresa por su nombre comercial o razón social.');
      return;
    }

    this.enviando.set(true);
    try {
      const raw = this.form.getRawValue();
      const empresaId = this.empresaUnica()?.id || raw.empresaId.trim() || null;
      await this.auth.login(raw.email, raw.password, empresaId);
      await this.router.navigateByUrl('/app/dashboard');
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.enviando.set(false);
    }
  }

  private async cargarEmpresas(): Promise<void> {
    this.cargandoEmpresas.set(true);
    try {
      const { empresas } = await this.auth.listarEmpresas();
      this.empresas.set(empresas);
      if (empresas.length === 1) {
        this.form.controls.empresaId.setValue(empresas[0].id);
      }
    } catch {
      this.empresas.set([]);
    } finally {
      this.cargandoEmpresas.set(false);
    }
  }
}
