# P1a Tenancy, Org, Identity, Protocol: task specs

These 16 tasks build tenants, the org tree, user accounts and the OAuth/OIDC server. They run in four parallel lanes (L1, L2, L3, and L3 + L4 for G-03) from week 3.

## T-01 Tenant aggregate, plans and limits

**Lane** L1 · **Depends on** F-03, F-06 · **Estimate** 4 d

**Goal:** operators can create, suspend and reactivate tenants with plan limits enforced.

**Scope**
- `Tenant` aggregate: slug (unique, 3–40 chars, lowercase), name, status (Active, Suspended, PendingDeletion, Deleted), plan, settings, parent tenant (for X-03).
- `Plan` with limits: max users, nodes, depth, SSO connections, custom roles.
- Commands: `CreateTenant` (also requests root node and owner invitation via events), `SuspendTenant`, `ReactivateTenant`, `RequestTenantDeletion` (30-day grace), `ChangePlan`.
- `ITenantLimits` query used by other modules (BR-14).
- Platform endpoints in section 9.4.

**Acceptance criteria**
- `TenantCreated`, `TenantSuspended`, `TenantReactivated`, `TenantDeletionRequested` published through the outbox.
- Only platform operators can call these endpoints.
- Downgrading a plan below current usage is rejected with a list of violations.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-T01-01 | U | Create tenant with slug `Acme Corp` | Validation error (format) |
| TC-T01-02 | I | Create two tenants with slug `acme` | Second fails with Conflict |
| TC-T01-03 | I | Create Acme | `TenantCreated` in outbox; root node and owner invitation created by consumers |
| TC-T01-04 | U | Suspend an already suspended tenant | Conflict error, no event |
| TC-T01-05 | I | Change Acme plan to max 5 users when it has 8 | Rejected, violation `users: 8 > 5` |
| TC-T01-06 | S | Tenant admin calls `/platform/tenants` | 403 |

## T-02 Tenant domains and DNS verification

**Lane** L1 · **Depends on** T-01 · **Estimate** 3 d

**Goal:** tenants prove ownership of email domains so they can drive discovery, auto-join and SSO enforcement.

**Scope:** add domain → generate TXT token (`_platform-verify.<domain>`); verify job checks DNS (retry for 72 h); one verified owner per domain platform-wide; remove domain; `DomainVerified` event.

**Acceptance criteria:** a domain verified by one tenant cannot be verified by another; unverified domains have no effect on login.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-T02-01 | I | Add `acme.com` , DNS stub returns correct TXT | Status Verified, event published |
| TC-T02-02 | I | DNS returns wrong token | Status Pending; retried; Failed after 72 h |
| TC-T02-03 | I | Globex adds `acme.com` after Acme verified it | Rejected with Conflict |
| TC-T02-04 | U | Add `localhost` or IP address | Validation error |

## G-18 Self-signup policy per tenant

**Lane** L1 · **Depends on** T-02 · **Estimate** 2 d

**Goal:** each tenant controls how new users join.

**Scope:** policy values `InviteOnly` (default), `VerifiedDomainAutoJoin`, `VerifiedDomainWithApproval`, `Open`; join requests queue for approval; policy exposed to login (P-02) and Access (A-01).

**Acceptance criteria:** policy is enforced server-side at join time; changes are audited.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-G18-01 | I | `InviteOnly` , new user from `acme.com` signs up | No membership; shown "ask your admin" |
| TC-G18-02 | I | `VerifiedDomainAutoJoin` , user `x@acme.com` verifies email | Active membership with default Member role |
| TC-G18-03 | I | `VerifiedDomainWithApproval` | Join request created; admin approves → membership |
| TC-G18-04 | I | `VerifiedDomainAutoJoin` , user `x@gmail.com` | Not joined |

## O-01 Org node types and nodes

**Lane** L1 · **Depends on** T-01 (c) · **Estimate** 5 d

**Goal:** tenants build a typed org tree.

**Scope**
- `OrgNodeType` (name, allowed child types, max depth) with default template Company → Region → Branch → Department → Team.
- `OrgNode` aggregate: create, rename, change code, attributes; invariants from section 6.3.
- Closure table maintenance on create; `path` (ltree) for sorting.
- Root node created on `TenantCreated`.
- Queries: get node, children (paged), subtree to depth N, ancestors (breadcrumb).

**Acceptance criteria:** invariants enforced in the domain; closure rows correct for every insert; tree of 100,000 nodes queries children in < 50 ms.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-O01-01 | U | Create Branch directly under Company when type rules forbid | Validation error |
| TC-O01-02 | U | Two siblings named Cairo Branch | Conflict |
| TC-O01-03 | U | Create node at depth 16 with limit 15 | Rejected (BR-14) |
| TC-O01-04 | I | Create Sales Team under Cairo | Closure rows: (Acme,Sales,3), (Egypt,Sales,2), (Cairo,Sales,1), (Sales,Sales,0) |
| TC-O01-05 | I | `TenantCreated` consumed | Exactly one root node |
| TC-O01-06 | P | Load 100,000 nodes, query children of a node with 500 children | p95 < 50 ms |

## O-02 Move node with cycle guard and impact preview

**Lane** L1 · **Depends on** O-01 · **Estimate** 4 d

**Goal:** re-parent a subtree safely and show who gains or loses access first.

**Scope:** `MoveTo(newParent, ITreeGuard)`; closure update SQL (section 7.4) in the same transaction; `OrgNodeMoved` event with old and new parent; preview endpoint returning type-rule violations, depth result and access changes (computed through PDP `list-subjects` before and after, using a simulated tuple set).

**Acceptance criteria:** cycles and cross-tenant moves impossible; preview and actual result match.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-O02-01 | U | Move Egypt Region under Sales Team (its descendant) | Rejected (cycle) |
| TC-O02-02 | U | Move root | Rejected |
| TC-O02-03 | I | Move Cairo Branch under UAE Region | Closure rows of Cairo and Sales updated; no orphan rows |
| TC-O02-04 | I | Preview same move | Mona loses access to Cairo and Sales; Sara gains access to Cairo and Sales |
| TC-O02-05 | I | Move where subtree depth would exceed limit | Rejected with depth error |
| TC-O02-06 | I | Two concurrent moves of same node | One succeeds, other gets Conflict |

## O-03 Block inheritance, suspend, archive, restore, merge

**Lane** L1 · **Depends on** O-02 · **Estimate** 5 d

**Goal:** all remaining lifecycle operations from cases C6, C12, C13, C14.

**Scope:** `BlockInheritance` / `UnblockInheritance` (needs `org_inheritance_manage` at parent; root not allowed); `Suspend` (whole subtree effectively paused); `Archive` with cascade target or rejection; `Restore` within 30 days; `Merge(source, target)` moving children, members, assignments, resources; preview for each.

**Acceptance criteria:** events `InheritanceBlocked`, `InheritanceUnblocked`, `OrgNodeSuspended`, `OrgNodeArchived`, `OrgNodeRestored`, `OrgNodesMerged` published; previews list every affected user.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-O03-01 | I | Block inheritance at Alexandria | Event published; preview lists Mona losing access |
| TC-O03-02 | U | Block inheritance at root | Rejected |
| TC-O03-03 | I | Archive Cairo with active Sales Team child, no cascade target | Rejected |
| TC-O03-04 | I | Archive Sales Team, restore after 10 days | Node and its assignments back |
| TC-O03-05 | I | Restore after 31 days | Rejected (expired) |
| TC-O03-06 | I | Merge Alexandria into Cairo | Legal Team moves under Cairo; duplicate assignments collapsed; Alexandria archived |
| TC-O03-07 | I | Suspend Egypt Region | PDP denies Mona on Cairo (via Q-02 later) |

## O-04 CSV import with dry run

**Lane** L1 · **Depends on** O-01 · **Estimate** 3 d

**Goal:** load an existing org chart in one step.

**Scope:** CSV columns `code, name, type, parentCode`; dry run returns errors per row; real run is all-or-nothing in one transaction; limits checked; result report.

**Acceptance criteria:** 10,000 rows import in < 30 s; any error → nothing imported.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-O04-01 | I | Valid file of 50 rows, dry run | 0 errors, nothing written |
| TC-O04-02 | I | Row with unknown `parentCode` | Error on that row; nothing imported |
| TC-O04-03 | I | Rows forming a cycle (A parent B, B parent A) | Both rows reported |
| TC-O04-04 | P | 10,000 valid rows | < 30 s, closure rows correct |

## I-01 User aggregate, registration, verification, passwords

**Lane** L2 · **Depends on** F-03 · **Estimate** 5 d

**Goal:** global user accounts with secure password handling.

**Scope:** `User` (email citext unique, status, security stamp); register; email verification token (24 h, single use, hashed); password hashing with ASP.NET Core `PasswordHasher` (PBKDF2, high iteration count) behind `IPasswordHasher` port; password policy (min 12 chars, breach check through k-anonymity range lookup with offline fallback list); events `UserRegistered`, `UserEmailVerified`.

**Acceptance criteria:** no plaintext password or token stored or logged; registration does not reveal whether an email exists.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-I01-01 | U | Register with `password123456` (in breach list) | Rejected |
| TC-I01-02 | I | Register existing email | Same response as success; "already registered" email sent instead |
| TC-I01-03 | I | Verify with token twice | First succeeds; second fails |
| TC-I01-04 | I | Verify with token after 25 h | Fails (expired) |
| TC-I01-05 | S | Inspect DB and logs after registration | No plaintext password or token |

## I-02 TOTP and recovery codes

**Lane** L2 · **Depends on** I-01 · **Estimate** 3 d

**Goal:** users can enrol an authenticator app and recover access.

**Scope:** TOTP (RFC 6238, 30 s, 6 digits, ±1 step) with encrypted secret; QR provisioning URI; 10 single-use recovery codes (hashed); replay protection (same code not accepted twice); events `MfaEnrolled`, `MfaRemoved`.

**Acceptance criteria:** removing the last MFA factor is blocked when tenant policy requires MFA.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-I02-01 | U | Code from previous 30 s window | Accepted |
| TC-I02-02 | U | Same valid code used twice | Second rejected |
| TC-I02-03 | I | Use a recovery code | Login succeeds; code marked used; 9 left |
| TC-I02-04 | I | Remove TOTP when tenant requires MFA and no other factor | Rejected |

## I-03 Passkeys (WebAuthn)

**Lane** L2 · **Depends on** I-01 · **Estimate** 4 d

**Goal:** phishing-resistant passwordless sign-in.

**Scope:** registration and assertion ceremonies (ASP.NET Core Identity passkey APIs or Fido2NetLib); RP ID per host (custom domains); store credential ID, public key, sign count, transports, friendly name; passkey as first factor and as MFA.

**Acceptance criteria:** works in Chrome, Safari and Firefox via Playwright virtual authenticator; sign-count regression flagged.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-I03-01 | E | Register passkey with virtual authenticator, then sign in | Success; `amr` contains `hwk` or `swk` |
| TC-I03-02 | I | Assertion with wrong origin | Rejected |
| TC-I03-03 | I | Sign count lower than stored | Rejected and security alert event |
| TC-I03-04 | I | Delete a passkey | Cannot sign in with it |

## I-04 Account recovery, lockout, security stamp

**Lane** L2 · **Depends on** I-01 · **Estimate** 3 d

**Goal:** safe recovery and brute-force protection.

**Scope:** forgot-password flow (token 1 h, single use, same response for unknown emails); lockout after 10 failures in 15 min (per account) and IP throttling; security stamp rotated on password/MFA change; `UserCredentialChanged` event (P-03 revokes other sessions).

**Acceptance criteria:** enumeration-safe responses and timings; lockout auditable and admin-resettable.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-I04-01 | I | 10 wrong passwords in 5 min | Account locked 15 min; event audited |
| TC-I04-02 | I | Forgot password for unknown email | Same response and similar timing as known email |
| TC-I04-03 | I | Reset password | Security stamp changed; `UserCredentialChanged` published |
| TC-I04-04 | I | Reuse reset token | Rejected |

## P-01 OpenIddict setup, keys, discovery, client registry

**Lane** L3 · **Depends on** F-03 · **Estimate** 4 d

**Goal:** a running OIDC provider with rotating keys and managed clients.

**Scope:** OpenIddict core + server with EF stores in `protocol` schema; configuration from section 7.5; key store backed by OpenBao (dev: file) with current, next, previous keys and 90-day rotation job; discovery and JWKS; client CRUD through AdminApi (`/clients`), client types (SPA via BFF, web, native, machine), redirect URI exact match, allowed grants and scopes, token format (G-08).

**Acceptance criteria:** discovery document validates; rotation publishes the next key 7 days before use; old key kept until its tokens expire.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-P01-01 | I | GET discovery | All endpoints of section 9.1 (enabled ones) listed |
| TC-P01-02 | I | Trigger key rotation | JWKS shows 3 keys; new tokens signed with new key; old tokens still validate |
| TC-P01-03 | I | Register client with wildcard redirect URI | Rejected |
| TC-P01-04 | S | Client secret stored | Only hash in DB |

## P-02 Authorization flow, tenant resolution, login pages

**Lane** L3 · **Depends on** P-01, I-01 (c), T-01 (c) · **Estimate** 6 d

**Goal:** users sign in through a tenant-aware code + PKCE flow.

**Scope:** passthrough `AuthorizeController` and token handling; tenant resolution order (section 7.3); Razor pages: login (email first), password, MFA, tenant picker, consent placeholder, errors; home-realm discovery hook (E-01 plugs in); membership and tenant status checks; claims and destinations from section 5.2; `acr_values` and `prompt` (`login`, `none`, `select_account`), `max_age`, `login_hint`.

**Acceptance criteria:** UC-04 works end to end; suspended tenant shows a dedicated page; strict CSP on all pages.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-P02-01 | E | Omar signs in via generic domain | Tenant picker shows Acme and Globex; token `tid` = chosen tenant |
| TC-P02-02 | E | Omar signs in at `acme.<platform>` subdomain | No picker; `tid` = Acme |
| TC-P02-03 | I | Code exchange without PKCE verifier | `invalid_grant` |
| TC-P02-04 | I | User with no membership in requested tenant | Error page, no token |
| TC-P02-05 | I | Tenant suspended | Suspended page, no token (C28) |
| TC-P02-06 | I | `prompt=none` with no session | `login_required` error to client |
| TC-P02-07 | S | Check headers on login page | CSP, HSTS, X-Frame-Options/frame-ancestors present |

## P-03 Sessions, refresh rotation, revocation, logout

**Lane** L3 · **Depends on** P-02 · **Estimate** 4 d

**Goal:** sessions that can be listed, ended and revoked quickly.

**Scope:** `Session` (sid, user, tenant, device, IP, created, last seen); refresh token rotation with reuse detection (reuse → revoke whole family); revocation endpoint; end-session; back-channel logout to registered clients; consumers for `MembershipRemoved`, `TenantSuspended`, `UserCredentialChanged` revoking sessions (BR-13).

**Acceptance criteria:** revocation effective within 60 s; reuse detection proven.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-P03-01 | I | Use refresh token, then reuse the old one | Reuse rejected; whole token family revoked |
| TC-P03-02 | I | Remove Omar from Acme | His Acme sessions and refresh tokens revoked < 60 s; Globex session intact |
| TC-P03-03 | I | End session from client | Back-channel logout token sent to other clients of that session |
| TC-P03-04 | I | Password change with "sign out other devices" | Other sessions revoked |

## P-04 Client credentials and service accounts

**Lane** L3 · **Depends on** P-01 · **Estimate** 3 d

**Goal:** machine-to-machine scenario M1 works.

**Scope:** `ServiceAccount` (tenant-scoped, linked to one machine client); client credentials grant issuing `sub` = service account ID, `tid` fixed; scopes and `resource` checked against client allow-list (G-03); service account usable as subject in role assignments.

**Acceptance criteria:** a machine token can never carry a different tenant; disabled service account gets `invalid_client`.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-P04-01 | I | `svc-acme-erp` requests token | `sub` = service account, `tid` = Acme |
| TC-P04-02 | I | Same client sends `tenant=t_globex` | Rejected |
| TC-P04-03 | I | Request scope not allowed for client | `invalid_scope` |
| TC-P04-04 | I | Disable service account, request token | `invalid_client` |

## G-03 API resource registry and resource indicators

**Lane** L3 + L4 · **Depends on** P-01, A-02 (c) · **Estimate** 4 d

**Goal:** every access token is bound to the API it is meant for.

**Scope:** `ApiResource` (identifier URI, display name, scopes, linked application in the permission catalog); clients granted resources and scopes; `resource` parameter (RFC 8707) on authorize and token; `aud` set to the resource; default resource per client; SDK validates `aud`.

**Acceptance criteria:** a token for API A is rejected by API B; requests for ungranted resources fail.

**Test cases**

| ID | Level | Scenario | Expected |
|---|---|---|---|
| TC-G03-01 | I | Request token with `resource=https://invoices.api` | `aud` = that URI; scopes limited to its scopes |
| TC-G03-02 | I | Send that token to the users API (SDK) | 401 (wrong audience) |
| TC-G03-03 | I | Client not granted the resource | `invalid_target` |
| TC-G03-04 | I | Two resources requested at once | Rejected (one resource per access token) |

