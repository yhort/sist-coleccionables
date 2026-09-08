import { HttpInterceptorFn } from '@angular/common/http';

import { inject } from '@angular/core';

import { AuthSessionService } from '../auth/auth-session.service';

export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthSessionService);
  const empresaId = auth.empresaId();
  if (!empresaId || req.url.includes('/auth/')) {
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
