import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideI18n } from '@ip/shared/ui';
import { routes } from './app.routes';
import { ACCOUNT_CATALOG } from './i18n/account.catalog';

describe('account portal routes', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    localStorage.setItem('ip.locale', 'en');
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideI18n(ACCOUNT_CATALOG),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  it('renders the profile of the signed-in user', async () => {
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/');
    (await vi.waitFor(() => backend.expectOne('/bff/user'))).flush({
      sub: 'u1',
      name: 'Mona',
      email: 'mona@acme.test',
      tenant: null,
      tenants: [{ id: 't1', slug: 'acme', name: 'Acme' }],
    });
    await navigation;
    await harness.fixture.whenStable();

    const el = harness.routeNativeElement!;
    expect(el.querySelector('[data-testid="page-title"]')?.textContent).toContain('Profile');
    expect(el.textContent).toContain('mona@acme.test');
    expect(el.textContent).toContain('Acme');
  });
});
