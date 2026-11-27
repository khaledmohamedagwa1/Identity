import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { BffAuthService, type SessionUser } from '@ip/shared/auth';
import { PermissionService } from './permission.service';

const CAIRO = '0193b6b0-0000-7000-8000-00000000ca10';
const user = (tenantId: string): SessionUser => ({
  sub: 'u1',
  tenant: { id: tenantId, slug: tenantId, name: tenantId },
  tenants: [],
});

describe('PermissionService', () => {
  let service: PermissionService;
  let backend: HttpTestingController;
  const currentUser = signal<SessionUser | null>(user('acme'));

  beforeEach(() => {
    currentUser.set(user('acme'));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: BffAuthService, useValue: { user: currentUser } },
      ],
    });
    service = TestBed.inject(PermissionService);
    backend = TestBed.inject(HttpTestingController);
    TestBed.tick();
  });

  afterEach(() => {
    backend.verify();
  });

  it('loads tenant-root permissions without a scope parameter', async () => {
    const pending = service.load();
    backend.expectOne('/pdp/v1/me/permissions').flush({ permissions: ['users_read'] });
    expect([...(await pending)]).toEqual(['users_read']);
    expect(service.has('users_read')).toBe(true);
  });

  it('loads per node with ?scope= and caches each node separately', async () => {
    const pending = service.load(CAIRO);
    backend
      .expectOne(`/pdp/v1/me/permissions?scope=${CAIRO}`)
      .flush({ permissions: ['users_manage'] });
    await pending;

    expect(service.has('users_manage', CAIRO)).toBe(true);
    expect(service.has('users_manage')).toBe(false);
    await service.load(CAIRO);
    backend.expectNone(() => true);
  });

  it('shares one request between concurrent loads', async () => {
    const a = service.load();
    const b = service.load();
    backend.expectOne('/pdp/v1/me/permissions').flush({ permissions: [] });
    await Promise.all([a, b]);
  });

  it('fails closed while loading and after an error', async () => {
    const allowed = service.can('users_manage');
    expect(allowed()).toBe(false);
    await Promise.resolve();
    backend
      .expectOne('/pdp/v1/me/permissions')
      .flush(null, { status: 503, statusText: 'Unavailable' });
    await Promise.resolve();
    expect(allowed()).toBe(false);
  });

  it('can() becomes true once permissions arrive', async () => {
    const allowed = service.can('users_manage');
    await Promise.resolve();
    backend.expectOne('/pdp/v1/me/permissions').flush({ permissions: ['users_manage'] });
    await vi.waitFor(() => {
      expect(allowed()).toBe(true);
    });
  });

  it('forgets permissions when the tenant changes', async () => {
    const pending = service.load();
    backend.expectOne('/pdp/v1/me/permissions').flush({ permissions: ['users_manage'] });
    await pending;

    currentUser.set(user('globex'));
    TestBed.tick();
    expect(service.has('users_manage')).toBe(false);
  });
});
