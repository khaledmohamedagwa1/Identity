import { provideBrowserGlobalErrorListeners, type ApplicationConfig } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideBffHttp } from '@ip/shared/auth';
import { provideI18n } from '@ip/shared/ui';
import { routes } from './app.routes';
import { ADMIN_CATALOG } from './i18n/admin.catalog';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideBffHttp(),
    provideI18n(ADMIN_CATALOG),
  ],
};
