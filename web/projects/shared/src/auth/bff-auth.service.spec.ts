import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { BffAuthService } from './bff-auth.service';
import { ACME, sessionUser } from './testing';

describe('BffAuthService', () => {
  let service: BffAuthService;
  let backend: HttpTestingController;
  const assign = vi.fn<(url: string) => void>();

  beforeEach(() => {
    assign.mockReset();
    const fakeDocument = {
      location: { pathname: '/t/acme/users', search: '?page=2', assign },
    } as unknown as Document;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DOCUMENT, useValue: fakeDocument },
      ],
    });
    service = TestBed.inject(BffAuthService);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
  });

  it('loads the session from the BFF once, even with concurrent callers', async () => {
    const first = service.ensureLoaded();
    const second = service.ensureLoaded();
    backend.expectOne('/bff/user').flush(sessionUser());
    await Promise.all([first, second]);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.activeTenant()).toEqual(ACME);
    await service.ensureLoaded();
    backend.expectNone('/bff/user');
  });

  it('treats 401 as anonymous', async () => {
    const pending = service.ensureLoaded();
    backend.expectOne('/bff/user').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect((await pending).status).toBe('anonymous');
    expect(service.user()).toBeNull();
  });

  it('defaults a missing tenants list to empty', async () => {
    const pending = service.ensureLoaded();
    const withoutTenants = { ...sessionUser(), tenants: undefined };
    backend.expectOne('/bff/user').flush(withoutTenants);
    await pending;
    expect(service.user()?.tenants).toEqual([]);
  });

  it('propagates server errors instead of pretending the user is signed out', async () => {
    const pending = service.ensureLoaded();
    backend.expectOne('/bff/user').flush(null, { status: 500, statusText: 'Server Error' });
    await expect(pending).rejects.toBeTruthy();
  });

  it('redirects to the BFF login with the current path as return URL', () => {
    service.login();
    expect(assign).toHaveBeenCalledWith('/bff/login?returnUrl=%2Ft%2Facme%2Fusers%3Fpage%3D2');
  });

  it('switches tenant through the BFF', () => {
    service.switchTenant('globex');
    expect(assign).toHaveBeenCalledWith('/bff/switch-tenant?tenant=globex&returnUrl=%2Ft%2Fglobex');
  });
});
