import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideI18n } from '@ip/shared/ui';
import { routes } from './app.routes';
import { ADMIN_CATALOG } from './i18n/admin.catalog';

const ACME = { id: 't-acme', slug: 'acme', name: 'Acme' };

describe('admin portal routes', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    localStorage.setItem('ip.locale', 'en');
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideI18n(ADMIN_CATALOG),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  it('redirects / to the active tenant and renders the overview in the shell', async () => {
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/');
    (await vi.waitFor(() => backend.expectOne('/bff/user'))).flush({
      sub: 'u1',
      name: 'Mona',
      tenant: ACME,
      tenants: [ACME],
    });
    await navigation;
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/t/acme');
    const el = harness.routeNativeElement!;
    expect(el.querySelector('[data-testid="app-title"]')?.textContent).toContain('Admin portal');
    expect(el.textContent).toContain('Welcome, Mona.');
  });

  it('sends anonymous users to the signed-out page', async () => {
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/');
    (await vi.waitFor(() => backend.expectOne('/bff/user'))).flush(null, {
      status: 401,
      statusText: 'Unauthorized',
    });
    await navigation;
    expect(TestBed.inject(Router).url).toBe('/signed-out');
  });
});
