import type { Routes } from '@angular/router';
import { StatusPageComponent } from './status-page.component';

/** Standard top-level status routes. Put them before the app's `**` route, or use `notFoundRoute` last. */
export const statusRoutes: Routes = [
  {
    path: 'forbidden',
    component: StatusPageComponent,
    data: { titleKey: 'page.forbidden.title', bodyKey: 'page.forbidden.body', action: 'home' },
  },
  {
    path: 'signed-out',
    component: StatusPageComponent,
    data: { titleKey: 'page.signedOut.title', bodyKey: 'page.signedOut.body', action: 'signIn' },
  },
  {
    path: 'no-tenant',
    component: StatusPageComponent,
    data: { titleKey: 'page.noTenant.title', bodyKey: 'page.noTenant.body', action: 'none' },
  },
  {
    path: 'not-found',
    component: StatusPageComponent,
    data: { titleKey: 'page.notFound.title', bodyKey: 'page.notFound.body', action: 'home' },
  },
];

export const notFoundRoute: Routes[number] = { path: '**', redirectTo: 'not-found' };
