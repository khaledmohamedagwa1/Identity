import { TestBed } from '@angular/core/testing';
import {
  provideRouter,
  Router,
  type UrlTree,
  type ActivatedRouteSnapshot,
  type RouterStateSnapshot,
  convertToParamMap,
} from '@angular/router';
import { BffAuthService } from './bff-auth.service';
import { authGuard, tenantGuard, tenantHomeRedirect } from './guards';
import type { SessionState } from './models';
import { sessionUser } from './testing';

function route(params: Record<string, string> = {}): ActivatedRouteSnapshot {
  return { paramMap: convertToParamMap(params) } as ActivatedRouteSnapshot;
}
const state = (url: string) => ({ url }) as RouterStateSnapshot;

describe('auth guards', () => {
  let session: SessionState;
  const auth = {
    ensureLoaded: () => Promise.resolve(session),
    login: vi.fn<(url?: string) => void>(),
    switchTenant: vi.fn<(slug: string, url?: string) => void>(),
  };

  beforeEach(() => {
    auth.login.mockReset();
    auth.switchTenant.mockReset();
    session = { status: 'authenticated', user: sessionUser() };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: BffAuthService, useValue: auth }],
    });
  });

  const run = <T>(fn: () => T) => TestBed.runInInjectionContext(fn);
  const serialize = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('authGuard starts the BFF login for anonymous users', async () => {
    session = { status: 'anonymous' };
    expect(await run(() => authGuard(route(), state('/t/acme')))).toBe(false);
    expect(auth.login).toHaveBeenCalledWith('/t/acme');
  });

  it('tenantGuard allows the session tenant', async () => {
    expect(await run(() => tenantGuard(route({ tenantSlug: 'acme' }), state('/t/acme')))).toBe(
      true,
    );
  });

  it('tenantGuard re-authorizes when the URL names another of the user tenants', async () => {
    expect(
      await run(() => tenantGuard(route({ tenantSlug: 'globex' }), state('/t/globex/org'))),
    ).toBe(false);
    expect(auth.switchTenant).toHaveBeenCalledWith('globex', '/t/globex/org');
  });

  it('tenantGuard rejects tenants the user is not a member of', async () => {
    const result = await run(() =>
      tenantGuard(route({ tenantSlug: 'initech' }), state('/t/initech')),
    );
    expect(serialize(result)).toBe('/not-found');
    expect(auth.switchTenant).not.toHaveBeenCalled();
  });

  it('tenantHomeRedirect sends users to their active tenant', async () => {
    const result = await run(() => tenantHomeRedirect(route(), state('/')));
    expect(serialize(result)).toBe('/t/acme');
  });

  it('tenantHomeRedirect sends anonymous users to the signed-out page', async () => {
    session = { status: 'anonymous' };
    expect(serialize(await run(() => tenantHomeRedirect(route(), state('/'))))).toBe('/signed-out');
  });
});
