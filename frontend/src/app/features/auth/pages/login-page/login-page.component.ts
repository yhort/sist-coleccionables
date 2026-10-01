import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthSessionService } from '../../../../core/auth/auth-session.service';
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
  readonly mostrarPassword = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', Validators.required],
    password: ['', Validators.required],
  });

  togglePassword(): void {
    this.mostrarPassword.update((v) => !v);
  }

  async entrar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }

    this.enviando.set(true);
    try {
      const raw = this.form.getRawValue();
      // Tenant multi-tenant se resuelve por subdominio / BD en el backend.
      await this.auth.login(raw.email, raw.password);
      await this.router.navigateByUrl('/app/dashboard');
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.enviando.set(false);
    }
  }
}
