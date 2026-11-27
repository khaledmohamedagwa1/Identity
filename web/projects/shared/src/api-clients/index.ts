import { InjectionToken, makeEnvironmentProviders, type EnvironmentProviders } from '@angular/core';

/**
 * Typed clients generated from the OpenAPI documents (`npm run api:generate`).
 * Never hand-write request/response types here (spec 7.8).
 */
export * as adminApi from './generated/admin-api/functions';
export type * as AdminApiModels from './generated/admin-api/models';
export * as pdpApi from './generated/pdp/functions';
export type * as PdpModels from './generated/pdp/models';

/**
 * Root URLs the portals use to reach the backends. All traffic goes through Bff.Host
 * (same origin, cookie session), which forwards to AdminApi and the PDP with tokens attached server-side.
 */
export interface ApiRoots {
  /** Prefix for AdminApi paths. The OpenAPI paths already include `/api/v1`. */
  readonly adminApi: string;
  /** Prefix under which the BFF exposes the PDP REST API (`/v1/...`). */
  readonly pdp: string;
  /** Prefix of the BFF's own endpoints (`/user`, `/login`, `/logout`, `/switch-tenant`). */
  readonly bff: string;
}

export const DEFAULT_API_ROOTS: ApiRoots = { adminApi: '', pdp: '/pdp', bff: '/bff' };

export const API_ROOTS = new InjectionToken<ApiRoots>('API_ROOTS', {
  providedIn: 'root',
  factory: () => DEFAULT_API_ROOTS,
});

export function provideApiRoots(roots: Partial<ApiRoots>): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: API_ROOTS, useValue: { ...DEFAULT_API_ROOTS, ...roots } },
  ]);
}
