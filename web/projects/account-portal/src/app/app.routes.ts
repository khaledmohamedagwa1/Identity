import type { Routes } from '@angular/router';
import { authGuard } from '@ip/shared/auth';
import { notFoundRoute, ShellComponent, statusRoutes, type NavItem } from '@ip/shared/ui';

/** Security pages (password, MFA, passkeys, sessions, apps) are added by task W-05. */
export const ACCOUNT_NAV: readonly NavItem[] = [
  { link: '/', labelKey: 'nav.profile', exact: true },
];

export const routes: Routes = [
  ...statusRoutes,
  {
    path: '',
    canActivate: [authGuard],
    component: ShellComponent,
    data: { titleKey: 'app.title', navItems: ACCOUNT_NAV },
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./profile/profile.page').then((m) => m.ProfilePage),
        title: 'Profile',
      },
    ],
  },
  notFoundRoute,
];
