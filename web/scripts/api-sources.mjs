// Single source of truth for which OpenAPI document each generated client comes from.
// The real contracts are published by task F-06 under ../docs/api; the stubs are fallbacks.
import { existsSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const webRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
export const repoRoot = resolve(webRoot, '..');
export const generatedRoot = resolve(webRoot, 'projects/shared/src/api-clients/generated');

export const apis = [
  {
    name: 'admin-api',
    contract: resolve(repoRoot, 'docs/api/admin-api.v1.yaml'),
    stub: resolve(webRoot, 'openapi/admin-api.v1.stub.yaml'),
  },
  {
    name: 'pdp',
    contract: resolve(repoRoot, 'docs/api/pdp.v1.yaml'),
    stub: resolve(webRoot, 'openapi/pdp.v1.stub.yaml'),
  },
];

export function inputFor(api) {
  return existsSync(api.contract)
    ? { path: api.contract, isStub: false }
    : { path: api.stub, isStub: true };
}
