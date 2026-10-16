import { HttpInterceptorFn } from '@angular/common/http';

import { inject } from '@angular/core';

import { AuthSessionService } from '../auth/auth-session.service';

/** Login/empresas no llevan tenant; /auth/me sí (header o JWT en el backend). */
function esAuthAnonimo(url: string): boolean {
  try {
    const path = new URL(url, 'http://local.invalid').pathname;
    return /\/auth\/(login|empresas)\/?$/.test(path);
  } catch {
    return /\/auth\/(login|empresas)(\?|$|\/)/.test(url);
  }
}

export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthSessionService);
  const empresaId = auth.empresaId();
  if (!empresaId || esAuthAnonimo(req.url)) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: {
        'X-CapitalPos-EmpresaId': empresaId,
      },
    }),
  );
};
