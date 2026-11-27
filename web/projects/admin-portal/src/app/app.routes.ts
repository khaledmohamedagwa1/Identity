import type { Routes } from '@angular/router';
import { authGuard, tenantGuard, tenantHomeRedirect } from '@ip/shared/auth';
import { notFoundRoute, ShellComponent, statusRoutes, type NavItem } from '@ip/shared/ui';

/**
 * Navigation of the admin portal. Feature areas (org tree, users, roles, ...) are added by tasks W-02..W-06;
 * each adds its nav item with the permission that reveals it and guards its route with requirePermission().
 */
export const ADMIN_NAV: readonly NavItem[] = [{ link: '.', labelKey: 'nav.overview', exact: true }];

export const routes: Routes = [
  { path: '', pathMatch: 'full', canActivate: [tenantHomeRedirect], children: [] },
  {
    // Tenant context lives in the URL (spec 7.8); the API still takes the tenant only from the token.
    path: 't/:tenantSlug',
    canActivate: [authGuard, tenantGuard],
    component: ShellComponent,
    data: { titleKey: 'app.title', navItems: ADMIN_NAV },
    children: [
      {
        path: '',
        loadComponent: () => import('./overview/overview.page').then((m) => m.OverviewPage),
        title: 'Overview',
      },
    ],
  },
  ...statusRoutes,
  notFoundRoute,
];
