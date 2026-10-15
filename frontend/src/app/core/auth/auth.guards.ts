import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { TenantService } from '../tenant/tenant.service';
import { AuthSessionService } from './auth-session.service';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthSessionService);
  const tenant = inject(TenantService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }
  if (!tenant.settings()) {
    try {
      await tenant.refreshFromApi();
    } catch {
      // Menú usa defaults seguros hasta el próximo login.
    }
  }
  return true;
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);
  return auth.isAuthenticated() ? router.createUrlTree(['/app/dashboard']) : true;
};
