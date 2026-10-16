import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthSessionService } from '../auth/auth-session.service';

/** 401 en login o en refresh de sesión no deben forzar logout (el refresh ya hace fallback local). */
function esAuthSinLogoutForzado(url: string): boolean {
  return /\/auth\/(login|me|empresas)(\?|$)/.test(url);
}

export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !esAuthSinLogoutForzado(req.url)) {
        auth.clear();
        void router.navigateByUrl('/login');
      }
      return throwError(() => error);
    }),
  );
};
