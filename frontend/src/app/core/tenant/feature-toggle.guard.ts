import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthSessionService } from '../auth/auth-session.service';
import { TenantFeatureName } from './tenant-settings.model';
import { TenantService } from './tenant.service';

export function featureToggleGuard(feature: TenantFeatureName): CanActivateFn {
  return featureToggleAnyGuard(feature);
}

/** Permite la ruta si el tenant tiene al menos uno de los features. */
export function featureToggleAnyGuard(...features: TenantFeatureName[]): CanActivateFn {
  return async () => {
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
        return router.createUrlTree(['/app/dashboard']);
      }
    }

    const allowed = features.some((feature) => tenant.hasFeature(feature));
    return allowed ? true : router.createUrlTree(['/app/dashboard']);
  };
}
