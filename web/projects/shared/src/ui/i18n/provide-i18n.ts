import {
  inject,
  makeEnvironmentProviders,
  provideEnvironmentInitializer,
  type EnvironmentProviders,
} from '@angular/core';
import { SHARED_CATALOG } from './shared.catalog';
import { I18N_CATALOGS, TranslationService, type Catalogs } from './translation.service';

/**
 * Registers the shared texts plus the app's own catalogs (later catalogs win on duplicate keys).
 * The service starts with the app so `?lang=` is read before any redirect drops it and `<html dir>` is set at once.
 */
export function provideI18n(...appCatalogs: Catalogs[]): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: I18N_CATALOGS, useValue: [SHARED_CATALOG, ...appCatalogs] },
    provideEnvironmentInitializer(() => inject(TranslationService)),
  ]);
}
