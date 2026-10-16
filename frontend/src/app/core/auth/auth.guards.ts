import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthSessionService } from './auth-session.service';
import { moduloDesdeRuta } from '../../layout/navigation';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return true;
  }
  return router.createUrlTree([auth.primeraRutaPermitida()]);
};

/** Requiere Lectura (L) del módulo declarado en route.data.module o inferido por URL. */
export const permissionGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  const modulo =
    (route.data['module'] as string | undefined) ??
    moduloDesdeRuta(state.url) ??
    null;

  if (!modulo || auth.hasPermission(modulo, 'L')) {
    return true;
  }

  const fallback = auth.primeraRutaPermitida();
  if (fallback && !state.url.startsWith(fallback)) {
    return router.createUrlTree([fallback]);
  }

  return router.createUrlTree(['/login']);
};
