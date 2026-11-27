import { inject } from '@angular/core';
import { Router, type ActivatedRouteSnapshot, type CanActivateFn } from '@angular/router';
import { PermissionService } from './permission.service';

/**
 * Functional route guard: `canActivate: [requirePermission('users_manage')]`.
 * `scopeParam` names a route parameter holding the org node ID; without it the check runs at the tenant root.
 */
export function requirePermission(
  permission: string,
  options: { scopeParam?: string } = {},
): CanActivateFn {
  return async (route: ActivatedRouteSnapshot) => {
    const permissions = inject(PermissionService);
    const router = inject(Router);
    const scope = options.scopeParam
      ? (route.paramMap.get(options.scopeParam) ?? undefined)
      : undefined;
    try {
      const granted = await permissions.load(scope);
      return granted.has(permission) ? true : router.parseUrl('/forbidden');
    } catch {
      return router.parseUrl('/forbidden');
    }
  };
}
