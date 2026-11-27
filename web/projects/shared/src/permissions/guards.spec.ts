import { TestBed } from '@angular/core/testing';
import {
  convertToParamMap,
  provideRouter,
  Router,
  type ActivatedRouteSnapshot,
  type RouterStateSnapshot,
  type UrlTree,
} from '@angular/router';
import { requirePermission } from './guards';
import { PermissionService } from './permission.service';

describe('requirePermission', () => {
  const load = vi.fn<(scope?: string) => Promise<ReadonlySet<string>>>();

  beforeEach(() => {
    load.mockReset();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: PermissionService, useValue: { load } }],
    });
  });

  const run = (guard: ReturnType<typeof requirePermission>, params: Record<string, string> = {}) =>
    TestBed.runInInjectionContext(() =>
      guard(
        { paramMap: convertToParamMap(params) } as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
      ),
    );

  it('allows navigation when the permission is granted', async () => {
    load.mockResolvedValue(new Set(['users_manage']));
    expect(await run(requirePermission('users_manage'))).toBe(true);
    expect(load).toHaveBeenCalledWith(undefined);
  });

  it('checks at the node named by a route parameter', async () => {
    load.mockResolvedValue(new Set(['users_manage']));
    await run(requirePermission('users_manage', { scopeParam: 'nodeId' }), { nodeId: 'cairo' });
    expect(load).toHaveBeenCalledWith('cairo');
  });

  it.each([
    ['denied', () => Promise.resolve(new Set<string>())],
    ['PDP unavailable', () => Promise.reject(new Error('503'))],
  ])('redirects to /forbidden when %s', async (_case, impl) => {
    load.mockImplementation(impl);
    const result = await run(requirePermission('users_manage'));
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/forbidden');
  });
});
