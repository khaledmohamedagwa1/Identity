import { HttpClient } from '@angular/common/http';
import {
  computed,
  effect,
  inject,
  Injectable,
  signal,
  untracked,
  type Signal,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_ROOTS, pdpApi } from '@ip/shared/api-clients';
import { BffAuthService } from '@ip/shared/auth';

/** Key used for the tenant root (no `scope` query parameter). */
const ROOT = '';

type Entry =
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly permissions: ReadonlySet<string> }
  | { readonly status: 'error' };

/**
 * Effective permission keys per org node, from the PDP's `GET /v1/me/permissions?scope=` (spec 5.4).
 * For rendering only: hiding a button is a convenience, the server always enforces.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly http = inject(HttpClient);
  private readonly roots = inject(API_ROOTS);
  private readonly auth = inject(BffAuthService);

  private readonly entries = signal<ReadonlyMap<string, Entry>>(new Map());
  private readonly inFlight = new Map<string, Promise<ReadonlySet<string>>>();

  constructor() {
    // Permissions belong to one session and tenant; drop them when either changes.
    let lastKey: string | undefined;
    effect(() => {
      const user = this.auth.user();
      const key = user ? `${user.sub}|${user.tenant?.id ?? ''}` : '';
      if (lastKey !== undefined && key !== lastKey) {
        this.clear();
      }
      lastKey = key;
    });
  }

  /** Loads (once) and returns the permissions at a node. `undefined` = tenant root. */
  load(scope?: string): Promise<ReadonlySet<string>> {
    const key = scope ?? ROOT;
    const entry = this.entries().get(key);
    if (entry?.status === 'loaded') {
      return Promise.resolve(entry.permissions);
    }
    const pending = this.inFlight.get(key);
    if (pending) {
      return pending;
    }
    this.setEntry(key, { status: 'loading' });
    const request = firstValueFrom(
      pdpApi.getMyPermissions(
        this.http,
        this.roots.pdp,
        scope === undefined ? undefined : { scope },
      ),
    )
      .then((response) => {
        const permissions: ReadonlySet<string> = new Set(response.body.permissions);
        this.setEntry(key, { status: 'loaded', permissions });
        return permissions;
      })
      .catch((error: unknown) => {
        this.setEntry(key, { status: 'error' });
        throw error;
      })
      .finally(() => this.inFlight.delete(key));
    this.inFlight.set(key, request);
    return request;
  }

  /**
   * Reactive check. Triggers a load if needed and is `false` until permissions arrive (fail closed).
   */
  can(permission: string, scope?: string): Signal<boolean> {
    const key = scope ?? ROOT;
    if (!untracked(this.entries).has(key)) {
      // Deferred so `can()` is safe to call from templates and computeds (no signal writes while rendering).
      queueMicrotask(() => {
        this.load(scope).catch(() => undefined);
      });
    }
    return computed(() => this.has(permission, scope));
  }

  /** Synchronous check against what is already loaded (fail closed). */
  has(permission: string, scope?: string): boolean {
    const entry = this.entries().get(scope ?? ROOT);
    return entry?.status === 'loaded' && entry.permissions.has(permission);
  }

  /** Forgets cached permissions, e.g. after a role change or tenant switch. */
  clear(scope?: string): void {
    if (scope === undefined) {
      this.entries.set(new Map());
      this.inFlight.clear();
      return;
    }
    this.entries.update((map) => {
      const next = new Map(map);
      next.delete(scope);
      return next;
    });
    this.inFlight.delete(scope);
  }

  private setEntry(key: string, entry: Entry): void {
    this.entries.update((map) => new Map(map).set(key, entry));
  }
}
