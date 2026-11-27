import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { API_ROOTS } from '@ip/shared/api-clients';
import type { SessionState, SessionUser } from './models';

/** `tenants` may be omitted by the BFF when the user has a single membership. */
type BffUserResponse = Omit<SessionUser, 'tenants'> & { readonly tenants?: SessionUser['tenants'] };

/**
 * Session state for the BFF pattern (spec 7.8): the browser only holds an HttpOnly cookie.
 * No access or refresh token is ever read, stored or sent by Angular code.
 */
@Injectable({ providedIn: 'root' })
export class BffAuthService {
  private readonly http = inject(HttpClient);
  private readonly roots = inject(API_ROOTS);
  private readonly document = inject(DOCUMENT);

  private readonly state = signal<SessionState>({ status: 'unknown' });
  private loading: Promise<SessionState> | null = null;

  readonly session = this.state.asReadonly();
  readonly user = computed<SessionUser | null>(() => {
    const s = this.state();
    return s.status === 'authenticated' ? s.user : null;
  });
  readonly isAuthenticated = computed(() => this.state().status === 'authenticated');
  readonly activeTenant = computed(() => this.user()?.tenant ?? null);

  /** Loads the session once; concurrent callers share the same request. */
  ensureLoaded(): Promise<SessionState> {
    const current = this.state();
    if (current.status !== 'unknown') {
      return Promise.resolve(current);
    }
    this.loading ??= this.fetchSession().finally(() => {
      this.loading = null;
    });
    return this.loading;
  }

  /** Re-reads the session, e.g. after the BFF reports 401. */
  refresh(): Promise<SessionState> {
    this.state.set({ status: 'unknown' });
    return this.ensureLoaded();
  }

  /** Full-page redirect: the BFF runs the OIDC code + PKCE flow server-side. */
  login(returnUrl: string = this.currentPath()): void {
    this.navigate(`${this.roots.bff}/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  logout(): void {
    this.navigate(`${this.roots.bff}/logout`);
  }

  /** Silent re-authorization for another tenant: the BFF obtains a token with the new `tid`. */
  switchTenant(tenantSlug: string, returnUrl = `/t/${encodeURIComponent(tenantSlug)}`): void {
    this.navigate(
      `${this.roots.bff}/switch-tenant?tenant=${encodeURIComponent(tenantSlug)}&returnUrl=${encodeURIComponent(returnUrl)}`,
    );
  }

  private async fetchSession(): Promise<SessionState> {
    let next: SessionState;
    try {
      const user = await firstValueFrom(this.http.get<BffUserResponse>(`${this.roots.bff}/user`));
      next = { status: 'authenticated', user: { ...user, tenants: user.tenants ?? [] } };
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 401 || error.status === 403)) {
        next = { status: 'anonymous' };
      } else {
        throw error;
      }
    }
    this.state.set(next);
    return next;
  }

  private currentPath(): string {
    const loc = this.document.location;
    return `${loc.pathname}${loc.search}`;
  }

  private navigate(url: string): void {
    this.document.location.assign(url);
  }
}
