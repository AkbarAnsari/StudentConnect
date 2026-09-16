import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { UserRole } from './auth.model';

// Improves user experience (redirects to Login before a request is even made) — this is
// NOT the real security boundary. The API enforces authorization independently and would
// reject an unauthorized request even if a guard were bypassed or misconfigured.
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export function roleGuard(allowedRoles: UserRole[]): CanActivateFn {
  return (_route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    const role = authService.role();
    if (role && allowedRoles.includes(role)) {
      return true;
    }

    return router.createUrlTree(['/unauthorized'], { queryParams: { from: state.url } });
  };
}
