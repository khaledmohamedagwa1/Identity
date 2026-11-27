import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BffAuthService, type SessionUser } from '@ip/shared/auth';
import { PermissionService } from '@ip/shared/permissions';
import { provideI18n } from '../i18n/provide-i18n';
import { TranslationService } from '../i18n/translation.service';
import { ShellComponent } from './shell.component';

describe('ShellComponent', () => {
  const user = signal<SessionUser | null>({
    sub: 'u1',
    name: 'Mona',
    tenant: { id: 't1', slug: 'acme', name: 'Acme' },
    tenants: [{ id: 't1', slug: 'acme', name: 'Acme' }],
  });

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideI18n({
          en: { title: 'Admin portal', a: 'Overview', b: 'Users' },
          ar: { title: 'بوابة الإدارة' },
        }),
        {
          provide: BffAuthService,
          useValue: {
            user,
            activeTenant: () => user()?.tenant ?? null,
            login: vi.fn(),
            logout: vi.fn(),
            switchTenant: vi.fn(),
          },
        },
        {
          provide: PermissionService,
          useValue: { can: (p: string) => () => p !== 'users_manage' },
        },
      ],
    });
  });

  async function render() {
    const fixture = TestBed.createComponent(ShellComponent);
    fixture.componentRef.setInput('titleKey', 'title');
    fixture.componentRef.setInput('navItems', [
      { link: '.', labelKey: 'a', exact: true },
      { link: 'users', labelKey: 'b', permission: 'users_manage' },
    ]);
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('shows the title, tenant and user', async () => {
    const { el } = await render();
    expect(el.querySelector('[data-testid="app-title"]')?.textContent).toContain('Admin portal');
    expect(el.querySelector('[data-testid="tenant-name"]')?.textContent).toContain('Acme');
    expect(el.querySelector('[data-testid="account-menu"]')?.textContent).toContain('Mona');
  });

  it('hides navigation items the user lacks permission for', async () => {
    const { el } = await render();
    const labels = [...el.querySelectorAll('nav a')].map((a) => a.textContent.trim());
    expect(labels).toEqual(['Overview']);
  });

  it('switches to Arabic and sets dir=rtl on the shell', async () => {
    const { fixture, el } = await render();
    el.querySelector<HTMLButtonElement>('[data-testid="language-toggle"]')!.click();
    await fixture.whenStable();
    expect(TestBed.inject(TranslationService).locale()).toBe('ar');
    expect(el.querySelector('.shell')?.getAttribute('dir')).toBe('rtl');
    expect(el.querySelector('[data-testid="app-title"]')?.textContent).toContain('بوابة الإدارة');
  });
});
