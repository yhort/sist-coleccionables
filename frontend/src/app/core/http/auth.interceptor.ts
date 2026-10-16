import { HttpInterceptorFn } from '@angular/common/http';

import { inject } from '@angular/core';

import { AuthSessionService } from '../auth/auth-session.service';

/**
 * Solo login y listado público van sin Bearer.
 * /auth/me y el resto de la API deben incluir Authorization cuando hay sesión.
 */
function esAuthAnonimo(url: string): boolean {
  try {
    const path = new URL(url, 'http://local.invalid').pathname;
    return /\/auth\/(login|empresas)\/?$/.test(path);
  } catch {
    return /\/auth\/(login|empresas)(\?|$|\/)/.test(url);
  }
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthSessionService);
  const token = auth.accessToken();
  if (!token || esAuthAnonimo(req.url)) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`,
      },
    }),
  );
};
