import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import { BffAuthService } from './bff-auth.service';

/** Requires a BFF session; otherwise starts the server-side login for the requested URL. */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(BffAuthService);
  const session = await auth.ensureLoaded();
  if (session.status === 'authenticated') {
    return true;
  }
  auth.login(state.url);
  return false;
};

/**
 * For routes under `/t/:tenantSlug`. The API takes the tenant only from the token (spec 7.3), so the URL slug
 * must match the session's tenant: if it differs, the BFF re-authorizes for that tenant.
 */
export const tenantGuard: CanActivateFn = async (route, state) => {
  const auth = inject(BffAuthService);
  const router = inject(Router);
  const slug = route.paramMap.get('tenantSlug');
  const session = await auth.ensureLoaded();
  if (session.status !== 'authenticated' || !slug) {
    return router.parseUrl('/');
  }
  const { tenant, tenants } = session.user;
  if (tenant?.slug === slug) {
    return true;
  }
  if (tenants.some((t) => t.slug === slug)) {
    auth.switchTenant(slug, state.url);
    return false;
  }
  return router.parseUrl('/not-found');
};

/** Sends `/` to the active tenant's home, or to the sign-in page. */
export const tenantHomeRedirect: CanActivateFn = async () => {
  const auth = inject(BffAuthService);
  const router = inject(Router);
  const session = await auth.ensureLoaded();
  if (session.status === 'authenticated' && session.user.tenant) {
    return router.createUrlTree(['/t', session.user.tenant.slug]);
  }
  return session.status === 'authenticated'
    ? router.parseUrl('/no-tenant')
    : router.parseUrl('/signed-out');
};
