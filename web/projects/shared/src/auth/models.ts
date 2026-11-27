/**
 * Shape of `GET /bff/user`. Bff.Host is delivered in task W-01; until then this is the agreed placeholder.
 * Built from ID-token claims (spec 5.2): sub, tid, name, email, picture, amr, auth_time.
 */
export interface TenantSummary {
  readonly id: string;
  readonly slug: string;
  readonly name: string;
}

export interface SessionUser {
  readonly sub: string;
  readonly name?: string;
  readonly email?: string;
  readonly picture?: string;
  /** Active tenant of the session (the token's `tid`). */
  readonly tenant: TenantSummary | null;
  /** Tenants the user is a member of, for the tenant switcher. */
  readonly tenants: readonly TenantSummary[];
  /** Coarse tenant-level role from `ten_role`, if any. */
  readonly tenantRole?: 'owner' | 'admin' | 'member' | 'guest';
  /** True when the user is a platform operator (platform console access). */
  readonly isPlatformOperator?: boolean;
  /** Unix seconds; used to decide when step-up is needed (W-05). */
  readonly authTime?: number;
}

export type SessionState =
  | { readonly status: 'unknown' }
  | { readonly status: 'anonymous' }
  | { readonly status: 'authenticated'; readonly user: SessionUser };
