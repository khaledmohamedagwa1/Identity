import type { Page } from '@playwright/test';

export const ACME = { id: '0193b6b0-0000-7000-8000-00000000acme', slug: 'acme', name: 'Acme' };
export const GLOBEX = {
  id: '0193b6b0-0000-7000-8000-00000000g1bx',
  slug: 'globex',
  name: 'Globex',
};

export interface MockOptions {
  readonly signedIn?: boolean;
  readonly permissions?: readonly string[];
}

/** Stands in for Bff.Host (W-01): session endpoint, PDP permissions, and a guard that no other call escapes. */
export async function mockBff(page: Page, options: MockOptions = {}): Promise<void> {
  const { signedIn = true, permissions = [] } = options;
  await page.route('**/bff/user', (route) =>
    signedIn
      ? route.fulfill({
          json: {
            sub: 'u-mona',
            name: 'Mona',
            email: 'mona@acme.test',
            tenant: ACME,
            tenants: [ACME, GLOBEX],
          },
        })
      : route.fulfill({ status: 401 }),
  );
  await page.route('**/pdp/v1/me/permissions**', (route) =>
    route.fulfill({ json: { permissions } }),
  );
  await page.route('**/bff/login**', (route) => route.fulfill({ status: 200, body: 'login page' }));
}
