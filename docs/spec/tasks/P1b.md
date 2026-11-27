# P1b Access, PDP, Audit, UI, QA: task specs

These 19 tasks deliver access management, the PDP and its sync, audit, rate limiting, both portals and the G1 test suites. Z-01 and Z-02 are on the critical path.

## A-01 Memberships and invitations

**Lane** L4 · **Depends on** T-01 (c), I-01 (c) · **Estimate** 4 d

**Goal:** users join tenants by invitation or signup policy, with owner protection.

**Scope:** `Membership` (type Member/Guest, status, primary and secondary home units); `Invitation` (email, node, role, 7-day hashed token, resend, revoke); accept flow (existing user or new registration; SSO domain hand-off later in E-04); owner membership created on `TenantCreated`; last-owner rule (BR-02); events `MembershipActivated`, `MembershipSuspended`, `MembershipRemoved`.

**Acceptance criteria:** invitation acceptance creates membership and the pre-assigned role atomically; last owner cannot be removed or demoted.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-A01-01 | I | Mona invites `new@acme.com` to Cairo as Viewer; user accepts | Active membership, primary unit Cairo, Viewer at Cairo |
| TC-A01-02 | I | Accept expired invitation | Rejected; admin can resend |
| TC-A01-03 | I | Remove Olivia when she is the only owner | Rejected (BR-02) |
| TC-A01-04 | I | Omar is member of Acme and Globex | Two memberships, independent statuses |
| TC-A01-05 | I | Remove Omar from Acme | `MembershipRemoved` ; assignments in Acme revoked (C27) |

## A-02 Permission catalog API and roles

**Lane** L4 · **Depends on** F-05 · **Estimate** 4 d

**Goal:** developers register permissions; tenants compose roles.

**Scope:** catalog registration endpoint per application (versioned, validated against the generator rules, triggers model regeneration pipeline); system roles Owner, Admin, Member, Viewer seeded; custom roles (name unique per tenant, permissions from catalog only); clone role; `RolePermissionsChanged` with added and removed keys; impact preview (count of assignments and users affected).

**Acceptance criteria:** BR-08 and BR-09 enforced; removing a permission from the catalog that is used by roles is blocked until roles are updated.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-A02-01 | I | Create custom role with unknown key `foo_bar` | Validation error |
| TC-A02-02 | I | Edit system role Admin | Rejected |
| TC-A02-03 | I | Clone Admin into Branch Admin, remove `tenant_settings_manage` | New role saved |
| TC-A02-04 | I | Add `invoices_approve` to Branch Admin used by 3 assignments | Preview shows 3 assignments; event lists added key |
| TC-A02-05 | I | Deprecate catalog key used by a role | Blocked with list of roles |

## A-03 Role assignments and assignment policy

**Lane** L4 · **Depends on** A-01, A-02, O-01 (c), Z-01 (c) · **Estimate** 5 d

**Goal:** scoped role assignments with delegation ceiling and no self-assignment.

**Scope:** `RoleAssignment` (subject user/group/service account, role, scope node, mode, validity, conditions, source); `AssignmentPolicy` domain service: actor must hold `roles_assign` at scope, role permissions ⊆ actor's effective permissions at scope (PDP batch check), actor ≠ subject (BR-07), subject is active member of same tenant; revoke; list by subject or by scope; events `RoleAssigned`, `RoleRevoked`.

**Acceptance criteria:** C15 and BR-06/BR-07 enforced; duplicate assignment rejected; assignments revoked on membership removal and node archive.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-A03-01 | I | Mona assigns Viewer to a user at Cairo | Allowed |
| TC-A03-02 | I | Mona assigns Admin (has `tenant_settings_manage` , which she lacks) at Cairo | Rejected: ceiling (C15) |
| TC-A03-03 | I | Mona assigns a role at UAE Region (outside her subtree) | Rejected: scope |
| TC-A03-04 | I | Mona assigns a role to herself | Rejected (BR-07) |
| TC-A03-05 | I | Assign same role, scope, mode twice | Conflict |
| TC-A03-06 | I | Archive Sales Team | Its assignments revoked, events published |

## A-04 Groups

**Lane** L4 · **Depends on** A-01 · **Estimate** 3 d

**Goal:** assign roles to groups instead of individuals.

**Scope:** `Group` (tenant-scoped, name unique), members (users, service accounts, groups up to depth 3, cycle detection); groups as assignment subjects; events `GroupMemberAdded`, `GroupMemberRemoved`.

**Acceptance criteria:** nested depth > 3 and cycles rejected; removing a user from a group removes inherited access (verified in Q-02).

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-A04-01 | U | Group A contains B, B contains A | Rejected (cycle) |
| TC-A04-02 | U | Nesting depth 4 | Rejected |
| TC-A04-03 | I | Add Globex user to an Acme group | Rejected (not a member) |

## Z-01 PDP host

**Lane** L5 · **Depends on** F-05 · **Estimate** 5 d

**Goal:** fast, tenant-safe authorization decisions.

**Scope:** Authorization.Host with REST and gRPC: `check`, `batch-check` (≤ 100), `list-objects`; tenant guard (tenant from token, active tenant and membership from a cached read model fed by events); ID builder `t<tenant>_<uuid>`; request context passed to OpenFGA conditions; current time injected for `active_window`; decision IDs; OpenTelemetry spans; fail closed.

**Acceptance criteria:** caller cannot reference another tenant's objects; p95 < 20 ms with SDK cache, < 50 ms uncached at 1,000 rps locally.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-Z01-01 | I | Mona checks `users_manage` on Cairo | allowed, decisionId returned |
| TC-Z01-02 | S | Acme token checks a Globex node ID | denied (ID built with Acme prefix, never matches) |
| TC-Z01-03 | I | Suspended tenant | denied for all checks (C28) |
| TC-Z01-04 | I | Batch of 100 checks | Results in same order |
| TC-Z01-05 | I | OpenFGA unavailable | 503, never allow |
| TC-Z01-06 | P | 1,000 rps uncached | p95 < 50 ms |

## Z-02 Outbox dispatcher, translators, watermark

**Lane** L5 · **Depends on** F-03, F-06 · **Estimate** 6 d

**Goal:** every authorization-relevant change reaches OpenFGA within 2 s, exactly once in effect.

**Scope:** Workers.Host dispatcher reading `platform.outbox` in order per tenant, batching OpenFGA writes (≤ 100 tuples per call), retries with backoff, dead-letter table, watermark per tenant; translators for `OrgNodeCreated/Moved/Archived/Restored`, `InheritanceBlocked/Unblocked`, `OrgNodeSuspended`, `RoleAssigned/Revoked`, `RolePermissionsChanged` (fan-out), `GroupMember*`, `MembershipRemoved`, `TenantCreated/Deleted`; duplicate write and missing delete treated as success; `X-Authz-Revision` returned by AdminApi.

**Acceptance criteria:** p99 propagation < 2 s; replaying the whole outbox produces the same tuple set; poison message does not block other tenants.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-Z02-01 | I | Assign role, then check immediately with `minRevision` | allowed |
| TC-Z02-02 | I | Move Cairo under UAE | `parent` and `inherit_from` tuples of Cairo replaced only |
| TC-Z02-03 | I | Replay all events twice | Tuple set identical |
| TC-Z02-04 | I | Edit role used by 3 assignments | 3 tuples written per added permission |
| TC-Z02-05 | I | Poison event for Acme | Dead-lettered; Globex events still processed |
| TC-Z02-06 | P | 200 changes/s for 5 min | p99 lag < 2 s |

## Z-03 SDK for ASP.NET Core

**Lane** L5 · **Depends on** Z-01 · **Estimate** 5 d

**Goal:** any API can protect endpoints in one line.

**Scope:** `AddPlatformAuthentication` (multi-issuer JWT validation from tenant registry, `aud` check from G-03, reference-token introspection per G-08); `AddPlatformAuthorization`; `RequirePermission` for minimal APIs and attribute for controllers; resource and context resolvers; `FilterAsync` using batch check; decision cache (5 s, per-tenant version invalidation); gRPC client with retries and circuit breaker; NuGet package with sample API.

**Acceptance criteria:** sample API protected end to end; fail-closed proven; no tokens or PII in SDK logs.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-Z03-01 | I | Omar calls `POST /nodes/{Sales}/users` in sample API | 403 |
| TC-Z03-02 | I | Mona calls same | 2xx |
| TC-Z03-03 | I | Token from custom-domain issuer of Acme | Accepted; unknown issuer rejected |
| TC-Z03-04 | I | PDP down, decision cached 2 s ago | Cached allow used; after TTL → 503 |
| TC-Z03-05 | U | Tenant version bump received | Cache entries for tenant dropped |

## Z-04 Effective permissions and explain

**Lane** L5 · **Depends on** Z-01 · **Estimate** 3 d

**Goal:** UIs can render by permission and admins can see why access exists.

**Scope:** `GET /v1/me/permissions?scope=` (batch check over catalog keys for the UI, cached 30 s); `POST /v1/expand` turning the OpenFGA expansion tree into a readable path ("Region Admin at Egypt Region, inherited"); `list-subjects`; `access_explain` permission required to explain others.

**Acceptance criteria:** explain returns all paths, not just the first; output has no IDs from other tenants.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-Z04-01 | I | Mona `me/permissions` at Sales | includes `users_manage` |
| TC-Z04-02 | I | Explain Mona `users_manage` on Sales | Path: Region Admin @ Egypt Region → Cairo → Sales |
| TC-Z04-03 | I | Omar explains Mona without `access_explain` | 403 |

## U-01 Audit store and query

**Lane** L7 · **Depends on** F-06 · **Estimate** 4 d

**Goal:** every privileged action is recorded and searchable (BR-12).

**Scope:** consumer for all integration events plus explicit audit events from handlers; `audit.entries` partitioned monthly; fields from section 7.4; query API with filters (actor, target, action, date, node) and CSV/JSON export; retention job per tenant setting.

**Acceptance criteria:** append-only (no update/delete permissions for app role); export of 100,000 rows streams in < 30 s.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-U01-01 | I | Role assigned | Entry with actor, target, before/after, correlation ID |
| TC-U01-02 | S | App DB role tries UPDATE on audit table | Permission denied |
| TC-U01-03 | I | Acme admin queries audit | Only Acme entries |
| TC-U01-04 | I | Retention 30 days | Older partitions dropped by job |

## U-02 Email notifications

**Lane** L7 · **Depends on** F-06 · **Estimate** 3 d

**Goal:** transactional emails for identity flows.

**Scope:** templates (Razor or Scriban) for verification, invitation, password reset, new sign-in alert, MFA change; English and Arabic; per-tenant branding (logo, colors, sender name); MailKit SMTP; retry and bounce logging.

**Acceptance criteria:** links use the tenant's host; no secrets in email logs.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-U02-01 | I | Invitation for Acme user with Arabic locale | Arabic RTL email in Mailpit with Acme logo |
| TC-U02-02 | I | SMTP fails twice then succeeds | Delivered once after retries |

## G-21 Rate limiting and quotas

**Lane** L10 · **Depends on** F-07 · **Estimate** 3 d

**Goal:** protect login, token and PDP endpoints from abuse and noisy neighbours.

**Scope:** ASP.NET Core rate limiter with Valkey-backed distributed counters; policies per IP, per client, per tenant, per account; limits configurable per plan; `429` with `Retry-After`; metrics and alerts.

**Acceptance criteria:** one tenant exceeding its quota does not degrade others; limits configurable without deploy.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-G21-01 | P | 100 logins/min from one IP (limit 30) | 429 after 30, `Retry-After` set |
| TC-G21-02 | P | Globex floods PDP; Acme normal traffic | Acme p95 unchanged |
| TC-G21-03 | I | Change Acme limit in config | Applied within 1 min |

## W-01 BFF host

**Lane** L9 · **Depends on** P-03 · **Estimate** 4 d

**Goal:** portals call APIs without tokens in the browser.

**Scope:** YARP routes to AdminApi and PDP; OIDC code + PKCE login on the server; HttpOnly, Secure, SameSite=Strict session cookie; access token refresh server-side; anti-forgery header for state-changing calls; `/bff/user`, `/bff/login`, `/bff/logout`, `/bff/switch-tenant`.

**Acceptance criteria:** no token visible in browser storage or responses; CSRF blocked.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-W01-01 | E | Log in to admin portal; inspect storage | No tokens in localStorage/sessionStorage |
| TC-W01-02 | S | POST without anti-forgery header | 400/403 |
| TC-W01-03 | E | Switch tenant Acme → Globex | New session tenant; Acme data no longer visible |

## W-02 Admin: tenant console and org tree

**Lane** L8 · **Depends on** F-10, O-02 · **Estimate** 6 d

**Goal:** admins manage tenant settings and the tree visually.

**Scope:** tenant settings and domains pages; tree with lazy loading (CDK tree), search, node details panel, create/rename, drag-and-drop move with impact preview dialog, block inheritance toggle, archive/restore; permission-aware actions (`*appCan`).

**Acceptance criteria:** UC-02 and UC-09 pass in e2e; 10,000-node tree usable (virtual scroll).

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-W02-01 | E | Drag Cairo onto UAE Region | Preview lists Mona loses, Sara gains; confirm → tree updated |
| TC-W02-02 | E | Omar opens tree | Create/move buttons hidden; API also returns 403 if forced |
| TC-W02-03 | E | Toggle block inheritance on Alexandria | Warning dialog with affected users; badge shown on node |

## W-03 Admin: users, invitations, groups

**Lane** L8 · **Depends on** A-01, A-04 · **Estimate** 5 d

**Goal:** admins manage people.

**Scope:** member list (filter by node, status, role; paging), member details (memberships, assignments, sessions summary), invite dialog (node, role limited to grantable ones), resend/revoke invitation, suspend/remove member, groups CRUD and membership.

**Acceptance criteria:** role picker only shows roles the admin may grant (ceiling); UC-03 passes in e2e.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-W03-01 | E | Mona opens invite dialog | Admin role not offered |
| TC-W03-02 | E | Invite, accept in second browser | New member visible under Cairo |
| TC-W03-03 | E | Remove last owner via UI | Error message from BR-02 |

## W-04 Admin: role builder and assignments

**Lane** L8 · **Depends on** A-03 · **Estimate** 5 d

**Goal:** admins create roles and assign them with scope and inheritance mode.

**Scope:** role list; permission matrix grouped by application with search; impact preview on save; assignment dialog: subject picker, node picker, mode (with plain-language explanation and mini diagram), validity dates; assignments list by user and by node.

**Acceptance criteria:** UC-08 passes in e2e; mode explanations match section 4.2.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-W04-01 | E | Create Branch Admin role with 5 permissions | Saved; appears in assignment dialog |
| TC-W04-02 | E | Assign with mode NodeOnly at Cairo | Listed with mode; Sales not covered (check via explain) |
| TC-W04-03 | E | Edit role in use | Impact preview shows affected count before save |

## W-05 Account portal

**Lane** L9 · **Depends on** I-03, P-03, W-01 · **Estimate** 5 d

**Goal:** users manage their own security.

**Scope:** profile; change password; TOTP enrol/remove; recovery codes regenerate; passkeys add/rename/delete; sessions list with device and location, sign out one or all; tenants list and switcher.

**Acceptance criteria:** sensitive actions require recent authentication (step-up if `auth_time` > 10 min).

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-W05-01 | E | Enrol TOTP, sign out, sign in | MFA prompted |
| TC-W05-02 | E | Sign out another session | That browser loses access on next request |
| TC-W05-03 | E | Delete passkey 20 min after login | Re-authentication requested first |

## W-06 Platform console

**Lane** L8 · **Depends on** T-01 · **Estimate** 3 d

**Goal:** operators manage tenants and plans.

**Scope:** tenant list with search and status; create tenant wizard (UC-01); suspend/reactivate with reason; plan editor; usage summary per tenant.

**Acceptance criteria:** only platform operators see the console; every action audited.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-W06-01 | E | Create tenant Initech with owner email | Owner receives invitation; tenant listed Active |
| TC-W06-02 | E | Suspend Initech | Initech users see suspended page at login |

## Q-01 End-to-end MVP journeys

**Lane** L11 · **Depends on** W-02 to W-06 · **Estimate** 5 d

**Goal:** prove the MVP works as users experience it (gate G1).

**Scope:** Playwright suites for UC-01 to UC-05 and UC-08 to UC-10 against the deployed dev environment; run on every merge to main; screenshots and traces on failure.

**Acceptance criteria:** suite green 5 runs in a row (no flakiness).

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-Q01-01 | E | UC-01 onboard tenant end to end | Owner signs in to new tenant |
| TC-Q01-02 | E | UC-03 invite to unit with role | Invitee sees only permitted pages |
| TC-Q01-03 | E | UC-04/05 sign in, switch tenant | Correct `tid` each time |
| TC-Q01-04 | E | UC-09 move branch | Access changes visible in sample app within 2 s |
| TC-Q01-05 | E | UC-10 app permission check | 403 for Omar on Sales, 2xx for Mona |

## Q-02 Authorization scenario suite C1–C16

**Lane** L11 · **Depends on** Z-02, A-03, O-03 · **Estimate** 5 d

**Goal:** every inheritance rule in section 4.3 (C1–C16) is an executable test through AdminApi → outbox → OpenFGA → PDP.

**Scope:** xUnit suite using `AcmeFixture`, real PostgreSQL and OpenFGA, changes made only through AdminApi commands, assertions only through PDP `check`/`expand`.

**Acceptance criteria:** all cases green; each test named after its case ID.

**Test cases**

| ID | Case | Scenario | Expected |
|---|---|---|---|
| TC-Q02-C1 | C1 | Mona on Sales | allowed |
| TC-Q02-C2 | C2 | Viewer at Dubai + Manager at Cairo; manage Dubai | denied (scope kept) |
| TC-Q02-C3 | C3 | Same role at Egypt and Cairo; revoke Egypt one | still allowed at Cairo |
| TC-Q02-C4 | C4 | Omar reads Egypt Region | denied; `org_navigate` breadcrumb allowed |
| TC-Q02-C5 | C5 | Omar on Alexandria | denied |
| TC-Q02-C6 | C6 | Mona on Legal; Olivia on Legal | denied; allowed |
| TC-Q02-C7 | C7 | Attempt to create deny assignment | API rejects (not supported) |
| TC-Q02-C8 | C8 | Group with Region Admin at Egypt; remove member | allowed, then denied |
| TC-Q02-C9 | C9 | New member with no roles on Cairo admin action | denied |
| TC-Q02-C10 | C10 | User with home units Cairo and Dubai, role only at Cairo | Dubai denied |
| TC-Q02-C11 | C11 | Move Cairo under UAE | Mona denied on Cairo, Sara allowed |
| TC-Q02-C12 | C12 | Merge Alexandria into Cairo | Legal now inherits from Cairo (Mona allowed) |
| TC-Q02-C13 | C13 | Archive Sales Team | assignments at Sales gone; restore brings them back |
| TC-Q02-C14 | C14 | Suspend Egypt Region | Mona denied on Cairo |
| TC-Q02-C15 | C15 | Mona grants beyond her permissions | rejected |
| TC-Q02-C16 | C16 | Remove/demote/block last owner | rejected |

