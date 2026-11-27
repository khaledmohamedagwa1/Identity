import type { SessionUser } from './models';

/** Test data shared by specs (AcmeFixture-like names from the spec's use cases). */
export const ACME = {
  id: '00000000-0000-7000-8000-00000000acme',
  slug: 'acme',
  name: 'Acme',
} as const;
export const GLOBEX = {
  id: '00000000-0000-7000-8000-0000000g10be',
  slug: 'globex',
  name: 'Globex',
} as const;

export function sessionUser(overrides: Partial<SessionUser> = {}): SessionUser {
  return {
    sub: '00000000-0000-7000-8000-000000000001',
    name: 'Mona',
    email: 'mona@acme.test',
    tenant: ACME,
    tenants: [ACME, GLOBEX],
    ...overrides,
  };
}
