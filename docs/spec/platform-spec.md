# Multi-Tenant Identity & Authorization Platform — Full Documentation

 · 

## 1. Executive summary

We will build a multi-tenant identity and authorization platform on ASP.NET Core (.NET 10) and Angular, designed with DDD and using only permissively licensed open-source components. It ships in five phases over about 24 weeks, with work split into lanes so several agents can build in parallel.

**What the platform does**
- Authenticates users for many tenants (customer companies), each with its own organization tree, policies, branding and enterprise SSO.
- Issues OAuth 2.1 / OpenID Connect tokens with **coarse context only** (who, which tenant, which session, how they authenticated).
- Answers fine-grained questions such as *can user U approve invoice I in branch B?* through a separate **Policy Decision Point (PDP)** that understands the org hierarchy, roles, inheritance and resource relationships.

**Guiding decisions (fixed for this document)**

| # | Decision | Consequence |
|---|---|---|
| D1 | The identity server authenticates and issues tokens with coarse context | Small tokens that rarely go stale; no permission lists in JWTs |
| D2 | A separate PDP answers `check(subject, action, resource)` | Apps call it through an SDK or ASP.NET Core middleware |
| D3 | PostgreSQL is the source of truth for the tree, roles and assignments | Changes flow to the authorization engine (OpenFGA) through a transactional outbox |
| D4 | DDD with bounded contexts, built as a modular monolith first | Modules can be split into services later without redesign |
| D5 | No commercial libraries | OpenIddict instead of Duende IdentityServer; no MediatR 13+, AutoMapper 15+, MassTransit 9, FluentAssertions 8, HashiCorp Vault (BSL) or Redis 8 |
| D6 | Global user identity plus per-tenant memberships | One person can belong to several tenants with different roles in each |

**Scope:** in scope are authentication, federation, provisioning, tenant and org management, roles and permissions, the PDP, admin and account portals, audit, and production operations. Out of scope for v1 are a billing engine, identity governance (access reviews and certification campaigns are Phase 4 candidates) and a consumer social-graph product.

## 2. Competitor analysis

No product combines a deep org hierarchy with delegated admin, a native Zanzibar-style PDP and a permissive license. That gap is our product position. Zitadel and Keycloak come closest on tenancy, Ory on authorization, and Auth0/WorkOS on the B2B admin experience.

### 2.1 Identity platforms

| Product | License / model | Tenancy model | Org hierarchy | Authorization | Standout features to learn from |
|---|---|---|---|---|---|
| Keycloak | Apache 2.0, self-hosted (Java/Quarkus) | Realm per tenant, or Organizations inside one realm (supported since v26) | Organization Groups add hierarchical groups inside an organization (v26.6) | Roles, groups, UMA-based authorization services | Per-org IdPs and domains; users in several orgs with all memberships in the token |
| Zitadel | AGPL v3 (open source), plus cloud | Instance → Organization → Project | Flat organizations; delegation through project grants | Project roles; org and project grants in OIDC claims | Project grants let one org hand a subset of roles to another; manager memberships; event-sourced core |
| Auth0 Organizations | Commercial SaaS; availability depends on plan | Organizations inside a tenant | Flat | Global and org-scoped roles; FGA as separate product | Per-org connections, domain discovery, auto-membership, invitations, delegated member management with escalation guardrails |
| WorkOS | Commercial SaaS, priced per connection | Organizations as containers for enterprise features | Flat; FGA adds resource hierarchy | RBAC plus FGA | Hosted Admin Portal where customer IT admins set up SSO, SCIM and domains themselves; audit log streaming |
| Ory (Kratos, Hydra, Keto) | Apache 2.0 components; B2B orgs and multi-tenancy only in Enterprise License | Open-source Kratos is single-tenant only | None in OSS | Keto: Zanzibar-style relationship checks | Clean split of identity, OAuth server and permission service; headless APIs |
| Microsoft Entra ID | Commercial | Tenant per organization; multitenant organization groups tenants | Administrative units (flat scoping) | Directory roles, app roles, Conditional Access | Cross-tenant sync, B2B collaboration, guest vs member users, conditional access policies |
| Logto | Open source (MPL-2.0) plus cloud | Organizations as core primitive | Flat | Org roles and permissions | Developer-first console and SDKs; SCIM support limited |
| FusionAuth / Authentik | FusionAuth: free community + paid; Authentik: open source + enterprise | Tenants | Flat | RBAC; Authentik adds policy engine | Full self-hosting; Authentik has strong SCIM and flexible flows |
| Clerk | Commercial SaaS | Organizations | Flat | Org roles and permissions in session token ( `o.rol` , `o.per` ) | Polished embeddable UI; verified domains with automatic invitations |

### 2.2 .NET protocol libraries

| Option | License | Verdict |
|---|---|---|
| Duende IdentityServer | Commercial (free only for dev/test or qualifying small companies) | Excluded by D5 |
| OpenIddict | Apache-2.0, no license key, no revenue threshold | **Chosen.** We write the endpoints, which also gives us full control of tenant-aware flows |
| ASP.NET Core Identity | MIT | Reuse only its password hasher and helpers; our own DDD `User` aggregate replaces `IdentityUser` |

### 2.3 Authorization engines

| Engine | Style | License | Fit |
|---|---|---|---|
| OpenFGA | Zanzibar ReBAC, CNCF project, conditional tuples (CEL) | Apache 2.0 | **Chosen.** Hierarchies and inheritance are native; official .NET SDK |
| SpiceDB | Zanzibar ReBAC with strong consistency controls | Apache 2.0 | Strong alternative; keep the PDP interface engine-neutral |
| OPA / Cedar | Policy as code (ABAC) | Apache 2.0 | Good for conditions, weak at deep hierarchies; Cedar runs as an embedded library |
| Permify | Zanzibar ReBAC | Check license before use | Not chosen |

### 2.4 Features we adopt
- **From Auth0 and Clerk:** domain discovery, auto-membership by verified domain, invitations with pre-assigned roles, and the rule that a delegated admin can never grant more than they hold.
- **From WorkOS:** a self-service admin portal where customer IT admins configure SSO, SCIM and domains; audit log export and streaming per tenant.
- **From Zitadel:** grants between tenants (a reseller or partner manages part of another tenant), manager roles, and machine users as first-class members.
- **From Keycloak:** per-tenant identity providers and multiple memberships per user; hierarchical groups.
- **From Entra:** guest vs member users, time-bound delegated admin for partners, and conditional access policies per tenant.
- **From Ory and OpenFGA:** keep the authorization engine as its own service with check, list-objects and expand APIs.

**Our differentiator:** a typed org tree of any depth with scoped role assignments, three inheritance modes, inheritance blocking, and delegated admin ceilings, all answered by one PDP call.

## 3. Business analysis

The platform serves B2B SaaS products whose customers are organizations with internal structure (regions, branches, departments). It must let each customer run its own identity and access setup safely without the operator's help.

### 3.1 Personas

| Persona | Goal | Typical actions |
|---|---|---|
| Platform super-admin (operator) | Run the platform for all tenants | Create and suspend tenants, set plan limits, support with audited impersonation, manage system roles and the permission catalog |
| Partner / reseller admin | Manage several customer tenants | Create child tenants, act inside them with time-bound delegated access |
| Tenant owner | Own one customer account | Configure security policy, SSO, domains, billing contact; cannot be locked out |
| Tenant admin | Run day-to-day access for the whole tenant | Manage the org tree, users, groups, custom roles, apps |
| Org-unit admin (delegated) | Run access for one branch and below | Invite users, assign roles within their subtree, create sub-units |
| End user / employee | Sign in and work | Log in, switch tenant or unit, manage MFA, passkeys and sessions |
| Guest / external collaborator | Access selected resources of another tenant | Accept invitation, sign in with own identity |
| Customer IT admin | Connect corporate directory | Set up SAML/OIDC SSO, SCIM provisioning, verify domains |
| Developer / integrator | Build apps on the platform | Register clients, define permissions, call the PDP through the SDK |
| Machine / service account | Call APIs without a user | Client credentials, scoped API keys |
| Auditor / security officer | Prove compliance | Read audit logs, export reports, review who has access to what |

### 3.2 Business rules

| ID | Rule |
|---|---|
| BR-01 | A user identity is global; access to a tenant exists only through a membership in that tenant |
| BR-02 | Every tenant has at least one active owner; removing or demoting the last owner is rejected |
| BR-03 | Every tenant has exactly one root org node, created with the tenant; nodes never move across tenants |
| BR-04 | A role assignment always has a subject (user, group or service account), a role and a scope (tenant root or an org node) |
| BR-05 | By default an assignment applies to its node and all descendants (see section 4 for other modes) |
| BR-06 | A delegated admin can only act inside their scope and can only grant roles whose permissions are a subset of their own at that scope |
| BR-07 | Nobody can change their own role assignments |
| BR-08 | Permissions are defined by developers per application; tenants compose them into custom roles but cannot invent new permission keys |
| BR-09 | System roles (Owner, Admin, Member, Viewer) are immutable; tenants can clone them into custom roles |
| BR-10 | If a tenant enforces SSO for a verified domain, users with that email domain must sign in through the tenant's IdP |
| BR-11 | Security policies (password, MFA, session lifetime) are set per tenant; a user in several tenants must satisfy the policy of the tenant they enter, through step-up if needed |
| BR-12 | Every privileged action is audited with actor, impersonator (if any), tenant, target, before/after values and correlation ID |
| BR-13 | Deprovisioning (SCIM delete, membership removal, tenant suspension) revokes refresh tokens and sessions for that tenant within 60 seconds |
| BR-14 | Plan limits (users, org nodes, depth, SSO connections, custom roles) are enforced at command time |

### 3.3 Functional requirements

| Area | Requirements |
|---|---|
| Tenancy | Create, suspend, reactivate, delete (with grace period) tenants; plans and limits; custom domains; branding; tenant hierarchy for resellers |
| Org structure | Typed nodes with allowed child types; create, rename, move, merge, archive, restore; depth and size limits; import from CSV or directory |
| Identity | Registration, email verification, password with breach check, passkeys, TOTP, recovery codes, account linking, profile, deletion (GDPR) |
| Authentication | OIDC/OAuth 2.1: authorization code + PKCE, client credentials, refresh rotation, device code, token exchange (RFC 8693), PAR, back-channel logout, introspection and revocation |
| Federation | Per-tenant SAML 2.0 and OIDC upstream IdPs, home-realm discovery by email domain, JIT provisioning with claim-to-node and claim-to-role mapping, social login |
| Provisioning | SCIM 2.0 server (Users, Groups) per tenant with bearer tokens, group-to-role mapping |
| Access | Permission catalog per application, custom roles, groups, scoped assignments with inheritance modes, time-bound assignments, delegation (deputy), invitations |
| Authorization (PDP) | Check, batch check, list objects, list subjects, expand (explain), effective permissions for UI, conditions (ABAC), resource sharing |
| Admin portal | Platform console, tenant console, delegated unit console, SSO and SCIM setup wizards, effective access explorer, audit viewer |
| Account portal | Profile, security, sessions and devices, tenant and unit switcher, connected apps |
| Audit and events | Immutable audit log, export, webhooks and event stream per tenant |

### 3.4 Non-functional requirements

| Attribute | Target |
|---|---|
| Availability | 99.95% for login and token endpoints; PDP 99.95% |
| Latency (p95) | Token endpoint < 150 ms; PDP check < 20 ms with cache, < 50 ms uncached |
| Throughput (initial sizing) | 500 logins/s, 5,000 PDP checks/s, horizontally scalable |
| Propagation | Authorization changes visible in the PDP within 2 s (p99); read-your-writes available on demand |
| Isolation | No cross-tenant read or write, enforced in app code and by PostgreSQL row-level security |
| Security | OWASP ASVS L2, OAuth 2.0 Security BCP, signing key rotation every 90 days, secrets in OpenBao or cloud KMS |
| Scale | 10,000 tenants, 5 million users, 100,000 nodes per tenant, depth up to 15 |
| Compliance | GDPR (export, erasure), audit retention configurable per tenant (default 1 year) |
| Operability | OpenTelemetry traces, metrics and logs; zero-downtime deploys; documented runbooks |

### 3.5 Key use cases

| ID | Use case | Main actor | Main flow (short) | Key alternate or exception |
|---|---|---|---|---|
| UC-01 | Onboard a new tenant | Operator or self-signup | Create tenant → root node → owner invited → owner sets password or passkey | Signup with a verified domain already claimed → route to that tenant's join request |
| UC-02 | Build the org tree | Tenant admin | Create typed nodes, or import CSV → validate types and depth → save | Import exceeds plan limit → partial import rejected as a whole |
| UC-03 | Invite a user to a unit with a role | Unit admin | Enter email, unit, role → ceiling check → email → user accepts → membership + assignment | Email belongs to an SSO-enforced domain → accept through IdP |
| UC-04 | Sign in with tenant selection | End user | Enter email → home-realm discovery → authenticate → pick tenant if several → token with `tid` | Tenant MFA policy stricter than session → step-up |
| UC-05 | Switch tenant or active unit | End user | Choose from switcher → silent re-authorization → new token | Target tenant requires SSO → redirect to IdP |
| UC-06 | Configure enterprise SSO | Customer IT admin | Verify domain → upload metadata or enter OIDC details → test login → enforce | Certificate expiry → warning 30 days ahead |
| UC-07 | Automatic provisioning via SCIM | Customer directory | Create/update/deactivate users and groups → map groups to roles at nodes | Deactivation → sessions revoked (BR-13) |
| UC-08 | Create a custom role | Tenant admin | Pick permissions from the catalog → save → assign | Role in use and edited → impact preview, then fan-out update |
| UC-09 | Move a branch to another region | Tenant admin | Drag node → impact preview (who gains or loses access) → confirm → tree and PDP updated | Target is a descendant → rejected (cycle) |
| UC-10 | App checks a permission | Application (SDK) | API receives token → middleware calls PDP `check` → allow or 403 | PDP unreachable → fail closed, cached decisions honored for their TTL |
| UC-11 | Explain access | Admin or auditor | Pick user + permission + resource → PDP expand → path shown ("Admin at Egypt Region, inherited") | — |
| UC-12 | Deputy while on leave | Manager | Delegate own role at a unit to a colleague for a date range | Deputy tries to re-delegate → rejected |
| UC-13 | Support impersonation | Operator support | Request with reason → tenant consent (if policy) → token with `act` claim → audited | Impersonating an owner on sensitive actions → blocked |
| UC-14 | Partner manages customer tenant | Reseller admin | Tenant grant with role and expiry → partner acts inside customer tenant | Grant expires → access removed automatically |
| UC-15 | Offboard a tenant | Operator | Suspend → export → grace period → hard delete including PDP tuples | Legal hold → delete deferred |

## 4. Organization hierarchy and inheritance

Access flows **down** the tree by default, never up and never sideways. Every exception (node-only grants, descendants-only grants, blocked inheritance, sharing, deputies, partner grants) is an explicit, audited record.

### 4.1 Two hierarchies, kept separate
- **Tenant hierarchy:** platform → partner/reseller tenant → customer tenant. Used only for reseller management. A parent tenant gets **no** implicit access to a child tenant; access needs an explicit, time-bound *tenant grant*.
- **Org hierarchy:** inside one tenant, a tree of typed nodes, for example Company → Region → Branch → Department → Team. This is where role inheritance happens.

### 4.2 The org node model

| Element | Description |
|---|---|
| `OrgNode` | Id, tenant, parent, type, name, code, status (Active, Suspended, Archived), attributes (JSON), `inheritanceBlocked` flag |
| `OrgNodeType` | Tenant-defined types with allowed child types and max depth, for example Region may contain Branch only |
| `Membership` | User belongs to the tenant; optional *home units* (one primary, several secondary for matrix orgs). Membership alone grants only the tenant's default Member role at the root |
| `RoleAssignment` | Subject + role + scope node + **inheritance mode** + optional validity window + optional conditions |
| `Resource ownership` | Business resources (documents, invoices, devices) point to an owning node; access to them follows the node's permissions plus any explicit sharing |

**Inheritance modes on an assignment**

| Mode | Applies to | Example |
|---|---|---|
| `NodeAndDescendants` (default) | The node and everything below | Admin at Egypt Region manages Cairo and Alexandria branches |
| `NodeOnly` | Only that node | HR officer at HQ handles HQ staff only, not branches |
| `DescendantsOnly` | Everything below, not the node itself | Regional auditor audits branches but not the regional office |

Mona's role at Egypt Region flows to Cairo and Sales but stops at Alexandria; Omar's node-only role does not reach Sales; Sara's descendants-only role applies in Dubai, not in UAE Region itself.

### 4.3 All inheritance cases and how they resolve

| # | Case | Rule | Notes and edge cases |
|---|---|---|---|
| C1 | Role at a node | Applies by its inheritance mode | Default `NodeAndDescendants` |
| C2 | Several roles at different nodes | Union of permissions, **each kept within its own scope** | Manager at Cairo + Viewer at Alexandria → manage Cairo, view Alexandria, nothing else |
| C3 | Same role at a node and its ancestor | No conflict; redundant assignment flagged in the UI | Removing the ancestor one keeps the child one |
| C4 | Upward visibility | No permission flows up. Parents see children through their own inherited permissions. Children may see ancestor **names** for breadcrumbs ( `org.navigate` ), nothing more | Roll-up reports are a permission at the parent, never a child grant |
| C5 | Sibling access | None by default | Cross-branch work uses sharing (C17) or a role at the common ancestor |
| C6 | Blocked inheritance at a node | Assignments from above stop at that node; its subtree inherits only from the node down | Tenant owners and break-glass role are **never** blocked; blocking needs the `org.inheritance.manage` permission at the parent; UI shows who loses access before saving |
| C7 | Explicit deny | Not supported in v1; blocking (C6) and NodeOnly/DescendantsOnly cover real cases | Avoids the classic allow/deny ordering bugs; revisit only with a concrete requirement |
| C8 | Group assignment | Group members get the role at the scope; groups are tenant-scoped | Nested groups allowed up to depth 3; cycles rejected |
| C9 | Membership vs role | Being a member of a unit gives no admin rights there | Default Member role is configurable per tenant |
| C10 | Matrix organization | A user may have several home units; roles are assigned separately at each | One primary unit drives defaults (UI landing, reports) |
| C11 | Move a node (re-parent) | Inherited access is recomputed from the new ancestors immediately; assignments on the moved subtree stay | Moving under its own descendant is rejected; moving across tenants is impossible; type rules re-checked; impact preview mandatory; audited |
| C12 | Merge nodes | Children, members, assignments and resources move to the target; source archived | Duplicate assignments collapse; conflicts listed before confirm |
| C13 | Archive (delete) a node | Rejected if it has active children or owned resources, unless a cascade target is chosen | Assignments at the node are revoked; restore within 30 days brings them back |
| C14 | Suspend a node | All assignments scoped at or under the node are paused | Users whose only access is there can still sign in but see no data in that unit |
| C15 | Delegated admin ceiling | Unit admin acts only inside their subtree and grants only roles that are subsets of their own effective permissions there | Cannot edit own assignments (BR-07); cannot block inheritance above their scope; cannot create nodes beyond depth or type rules |
| C16 | Last owner / lockout protection | Last tenant owner cannot be removed, demoted or blocked | Break-glass account per tenant, stored offline |
| C17 | Resource sharing | A resource can be shared to a user, group or another node with a resource role (viewer, editor) | Sharing to another node includes its descendants unless shared `NodeOnly` |
| C18 | Time-bound assignment | Valid only between `validFrom` and `validUntil` | Expiry handled by condition in the PDP and a cleanup job |
| C19 | Deputy delegation | A user delegates one of their assignments to another user for a period | Deputy cannot re-delegate; delegator keeps access; both audited; revoked if delegator loses the role |
| C20 | Impersonation | Support acts as a user with an `act` claim | All checks run as the target user plus an impersonation policy that blocks sensitive actions |
| C21 | Guest from another tenant | Guest membership with limited default role and restricted scope | Guest cannot become owner or admin unless converted to a full member |
| C22 | Partner tenant grant | Partner users act in a customer tenant with a granted role at a scope, until expiry | Customer owner can revoke at any time; partner cannot see grants of other partners |
| C23 | Conditional (ABAC) permissions | An assignment may carry conditions, for example `amount <= 10000` , working hours, IP range | Evaluated by the PDP using request context; missing context = deny |
| C24 | Permission implication | Some permissions imply others, for example `users.manage` implies `users.read` | Defined in the catalog; computed in the authorization model, not in the database |
| C25 | Role definition changed | All assignments of that role change effect | Impact preview; system roles immutable; tuple fan-out handled by the sync worker |
| C26 | SSO / SCIM driven placement | IdP claims or SCIM groups place users into nodes and roles | Mappings are tenant rules; manual changes to mapped assignments are flagged and may be overwritten on next sync |
| C27 | Deprovisioning | Removing membership revokes all assignments, sessions and refresh tokens in that tenant | Other tenants untouched |
| C28 | Tenant suspended | Every check in that tenant returns deny except platform support with impersonation | Login shows a tenant-suspended page |

### 4.4 Resolution algorithm

Given subject *S*, permission *P*, resource *R* in tenant *T*:
1. Reject if the token's tenant is not *T*, the tenant is suspended, or *S* has no active membership in *T* (C27, C28).
1. Find the owning node *N* of *R* (for a node itself, *N = R*).
1. Collect assignments that reach *N*: direct at *N* (any mode except `DescendantsOnly`), and from each ancestor *A* walking up until a node with `inheritanceBlocked` is crossed (modes `NodeAndDescendants` or `DescendantsOnly`). Owners and break-glass ignore blocking.
1. Add assignments held through groups and active deputy delegations; drop expired or suspended ones.
1. Expand each role to permissions, including implied ones (C24).
1. Add resource-level grants from sharing (C17) and partner grants (C22).
1. If *P* is present, evaluate its conditions against request context (C23). Allow if any path passes; otherwise deny.
1. Return the decision with a decision ID; `expand` returns the path for UC-11.

Steps 3 to 7 are what the OpenFGA model in section 5 computes in one `check` call.

## 5. Authorization model

Authorization combines scoped RBAC (roles at org nodes), ReBAC (relationships such as parent, owner node, shared with) and ABAC conditions. OpenFGA evaluates all three in one `check`; our PDP wraps it with tenant guards, caching, context and explanations.

### 5.1 Responsibilities

| Component | Owns | Never does |
|---|---|---|
| Identity server | Who the user is, which tenant, how they signed in | Decide business permissions |
| Admin API (Access module) | Roles, assignments, groups, tree; writes to PostgreSQL + outbox | Answer runtime checks |
| Sync worker | Translates domain events into OpenFGA tuple writes and deletes; nightly reconciliation | Hold business rules |
| PDP (Authorization service) | `check` , `batch-check` , `list-objects` , `list-subjects` , `expand` , effective permissions for UI | Write tuples directly from user input |
| Apps via SDK | Enforce decisions at endpoints | Cache decisions beyond the configured TTL |

### 5.2 Token design (coarse context)

| Claim | Token | Purpose |
|---|---|---|
| `iss` , `aud` , `exp` , `iat` , `jti` , `client_id` , `scope` | Access | Standard; access token lifetime 10 minutes |
| `sub` | Access, ID | Global user ID (UUIDv7) |
| `tid` | Access, ID | Active tenant; switching tenant = new token |
| `sid` | Access, ID | Session ID for back-channel logout and revocation |
| `amr` , `acr` , `auth_time` | Access, ID | How and when the user authenticated; drives step-up |
| `ten_role` (optional) | Access | Only coarse tenant-level role: `owner` , `admin` , `member` , `guest` |
| `act` | Access | Present only during impersonation or token exchange (RFC 8693) |
| `name` , `email` , `email_verified` , `picture` | ID | Profile for the UI |

No permission lists, node lists or group lists go into tokens. Refresh tokens rotate on every use, are bound to the session, and are revoked on deprovisioning (BR-13).

### 5.3 OpenFGA model (generated from the permission catalog)

Developers register permissions per application (for example `users_manage`, `invoices_approve`). A generator turns the catalog into the model below, one block of relations per permission. Tenants create roles from these permissions; roles never change the model.

```
model
  schema 1.1

type user
type service_account

type group
  relations
    define member: [user, service_account, group#member]

type tenant
  relations
    define owner: [user]
    define break_glass: [user]

# One object per role assignment (subject + role + scope + mode)
type role_assignment
  relations
    define assignee: [user, user with active_window, service_account, group#member]

type org_node
  relations
    define tenant: [tenant]
    define parent: [org_node]
    # written = parent, unless the node blocks inheritance (case C6)
    define inherit_from: [org_node]

    # ---- generated block for permission: invoices_approve ----
    define invoices_approve_here: [role_assignment#assignee, role_assignment#assignee with amount_limit]
    define invoices_approve_subtree: [role_assignment#assignee, role_assignment#assignee with amount_limit]
    define invoices_approve_below: [role_assignment#assignee, role_assignment#assignee with amount_limit]
    define invoices_approve_inherited: invoices_approve_subtree from inherit_from or invoices_approve_below from inherit_from or invoices_approve_inherited from inherit_from
    define invoices_approve: invoices_approve_here or invoices_approve_subtree or invoices_approve_inherited or owner from tenant or break_glass from tenant

    # ---- generated block for permission: users_manage / users_read (implication C24) ----
    define users_manage_here: [role_assignment#assignee]
    define users_manage_subtree: [role_assignment#assignee]
    define users_manage_below: [role_assignment#assignee]
    define users_manage_inherited: users_manage_subtree from inherit_from or users_manage_below from inherit_from or users_manage_inherited from inherit_from
    define users_manage: users_manage_here or users_manage_subtree or users_manage_inherited or owner from tenant or break_glass from tenant
    # users_read_* generated the same way, then:
    define can_users_read: users_read or users_manage

# Example business resource owned by a node, shareable (case C17)
type invoice
  relations
    define owner_node: [org_node]
    define shared_node: [org_node]
    define approver: [user, group#member]
    define can_approve: approver or invoices_approve from owner_node or invoices_approve from shared_node

condition active_window(current_time: timestamp, valid_from: timestamp, valid_until: timestamp) {
  current_time >= valid_from && current_time < valid_until
}

condition amount_limit(amount: double, max_amount: double) {
  amount <= max_amount
}
```

**How the cases map to tuples**

| Case | Tuples written by the sync worker |
|---|---|
| Node created under parent P | `org_node:N#parent@org_node:P` , `org_node:N#inherit_from@org_node:P` , `org_node:N#tenant@tenant:T` |
| Inheritance blocked (C6) | Delete `org_node:N#inherit_from@org_node:P` (parent tuple stays for navigation) |
| Node moved (C11) | Replace `parent` and `inherit_from` tuples of N only; the subtree follows automatically |
| Role assignment RA (role with permissions p1..pn, scope N, mode M) | `role_assignment:RA#assignee@user:U` + for each permission `org_node:N#p_{here,subtree,below}@role_assignment:RA#assignee` |
| Role edited (C25) | Add or delete one tuple per affected assignment for the changed permission (batched) |
| Time-bound or deputy (C18, C19) | Assignee tuple with condition `active_window` and its parameters |
| Conditional permission (C23) | Permission tuple with condition `amount_limit` , `max_amount` stored on the tuple; `amount` sent as request context |
| Sharing (C17) | `invoice:I#shared_node@org_node:X` or `invoice:I#approver@user:U` |

**Tenant isolation in OpenFGA.** One shared store, with every object ID prefixed by the tenant (`org_node:t42_<uuid>`). The PDP builds IDs from the token's `tid`, so a caller can never name an object in another tenant. Dedicated-tier tenants can get their own store.

### 5.4 PDP API

| Endpoint | Request | Response |
|---|---|---|
| `POST /v1/check` | `subject` , `permission` , `resource {type,id}` , `context {}` , `consistency?` , `minRevision?` | `allowed` , `decisionId` |
| `POST /v1/batch-check` | up to 100 checks | list of results, same order |
| `POST /v1/list-objects` | `subject` , `permission` , `resourceType` | IDs the subject can act on (paged) |
| `POST /v1/list-subjects` | `permission` , `resource` | users and groups with access |
| `POST /v1/expand` | `subject` , `permission` , `resource` | Path explanation for the access explorer (UC-11) |
| `GET /v1/me/permissions?scope=node` | token only | Effective permission keys at a node, for UI rendering only |

The subject defaults to the token's `sub` and tenant to `tid`; checking another subject requires the `access.explain` permission. Transport is REST and gRPC; the SDK prefers gRPC.

### 5.5 Consistency, caching and failure
- **Propagation:** each admin write returns an `X-Authz-Revision` (the outbox sequence). A PDP call with `minRevision` waits up to 2 s until the sync watermark reaches it, otherwise uses OpenFGA's higher-consistency mode.
- **Caching:** decisions cached in-process by the SDK for 5 s, keyed by tenant, subject, permission, resource and context hash. A per-tenant authorization version published on change invalidates caches early.
- **Failure:** fail closed. If the PDP is down, only unexpired cached allows are honored; everything else returns 503 or 403.
- **Drift control:** a nightly reconciliation job recomputes expected tuples from PostgreSQL, compares with OpenFGA, repairs differences and reports a drift metric (target: 0).

## 6. DDD architecture

The system is nine bounded contexts in a modular monolith, deployed as five hosts. Organization Structure and Access are the core domain; that is where we invest the most modelling effort and tests.

Admin changes are written to PostgreSQL with an outbox record in the same transaction; the workers project them into OpenFGA, which the PDP reads for every decision.

### 6.1 Bounded contexts

| Context | Subdomain type | Responsibility | Main aggregates |
|---|---|---|---|
| Tenancy | Supporting | Tenants, plans and limits, custom domains, branding, tenant grants | `Tenant` , `TenantGrant` , `Plan` |
| Organization Structure | **Core** | Typed org tree, moves, merges, archive, closure table | `OrgNode` , `OrgNodeType` |
| Identity | Supporting | Global user accounts, credentials, MFA factors, passkeys, linked logins, recovery | `User` , `ServiceAccount` |
| Access | **Core** | Permission catalog, roles, groups, memberships, role assignments, delegations, invitations | `Membership` , `Role` , `Group` , `RoleAssignment` , `Delegation` , `Invitation` , `PermissionCatalog` |
| Authentication & Protocol | Generic (OpenIddict) + supporting | OIDC/OAuth endpoints, clients, scopes, sessions, consent, token issuance, login UI | `Client` , `Session` (OpenIddict owns tokens and authorizations) |
| Federation | Supporting | Upstream SAML/OIDC IdPs, home-realm discovery, claim mappings, JIT | `IdentityProviderConnection` , `DomainVerification` |
| Provisioning | Supporting | SCIM 2.0 server, SCIM tokens, group-to-role mappings | `ScimEndpoint` , `ScimMapping` |
| Authorization (PDP) | **Core** (read side) | Decisions, explain, effective permissions; OpenFGA projection and sync | No write aggregates; projection only |
| Audit & Notifications | Generic | Audit log, webhooks, email/SMS | `AuditEntry` (append-only), `WebhookSubscription` |

### 6.2 Context map

| Upstream → Downstream | Relationship | Integration |
|---|---|---|
| Tenancy → all contexts | Published language ( `TenantId` , tenant status) | Integration events `TenantCreated` , `TenantSuspended` |
| Identity → Access | Customer–supplier | `UserRegistered` , `UserDeactivated` ; Access stores `UserId` only |
| Organization Structure → Access, Authorization | Customer–supplier | `OrgNodeCreated` , `OrgNodeMoved` , `InheritanceBlocked` , `OrgNodeArchived` |
| Access → Authorization | Published language | `RoleAssigned` , `RoleRevoked` , `RolePermissionsChanged` , `GroupMemberAdded` , `DelegationGranted` → tuples |
| Federation, Provisioning → Identity, Access | Anti-corruption layer | SAML assertions and SCIM payloads translated to commands ( `ProvisionUser` , `AssignRole` ) |
| Authentication → Identity, Access, Tenancy | Conformist (queries) | Reads user status, membership and tenant policy at login |
| All → Audit | Open host | Every integration event is also audit input |

### 6.3 Aggregates and invariants (core domain)

**OrgNode** (Organization Structure)
- Invariants: name unique among siblings; child type allowed by parent type; depth ≤ tenant limit; root cannot be moved, archived or blocked; cannot archive with active children unless a cascade target is given.
- Behaviour: `Create`, `Rename`, `MoveTo(newParent, ITreeGuard)`, `BlockInheritance`, `UnblockInheritance`, `Suspend`, `Archive`, `Restore`.
- `ITreeGuard` is a domain service that asks the closure table *is X a descendant of Y?* to prevent cycles (aggregate boundaries stay small: one node, not the whole tree).

**RoleAssignment** (Access)
- Invariants: subject is an active member (or tenant group or service account) of the same tenant; role belongs to the tenant or is a system role; scope node belongs to the tenant; validity window is consistent; no duplicate (subject, role, scope, mode).
- Creation goes through the `AssignmentPolicy` domain service, which enforces the delegation ceiling (BR-06) and no self-assignment (BR-07) using the PDP.

**Role**: permissions must exist in the catalog of the target application; system roles immutable; `ChangePermissions` raises `RolePermissionsChanged` with added and removed keys.

**Membership**: one per (user, tenant); status Invited, Active, Suspended, Removed; type Member or Guest; home units; removing the last owner rejected (BR-02, checked through a tenant-wide owner count).

**Delegation**: references a source `RoleAssignment`; period required; not re-delegable; auto-revoked when the source is revoked.

### 6.4 Domain events (selection)

| Event | Raised by | Consumers |
|---|---|---|
| `TenantCreated` / `TenantSuspended` / `TenantDeleted` | Tenant | Org (create root), Access (owner membership), Authorization, Auth (session revoke), Audit |
| `OrgNodeCreated` / `OrgNodeMoved` / `OrgNodeArchived` | OrgNode | Closure table updater (same transaction), Authorization sync, Audit |
| `InheritanceBlocked` / `InheritanceUnblocked` | OrgNode | Authorization sync, Audit |
| `MembershipActivated` / `MembershipRemoved` | Membership | Authorization sync, Auth (revoke sessions), Notifications |
| `RoleAssigned` / `RoleRevoked` | RoleAssignment | Authorization sync, Audit, Notifications |
| `RolePermissionsChanged` | Role | Authorization sync (fan-out), Audit |
| `UserCredentialChanged` / `MfaEnrolled` | User | Auth (revoke other sessions per policy), Notifications, Audit |
| `IdpConnectionEnforced` | IdentityProviderConnection | Auth (login routing), Audit |

Domain events are dispatched in-process before commit for same-context side effects (for example the closure table). Integration events are written to the **outbox** in the same transaction and delivered at least once to other modules, the sync worker and webhooks.

### 6.5 Architecture rules
1. Each module has `Domain`, `Application`, `Infrastructure`, `Api` and `Contracts` projects. Other modules may reference **only** `Contracts`.
1. Each module owns its PostgreSQL schema (`tenancy`, `org`, `identity`, `access`, …). No cross-schema joins or foreign keys; read models are built from events.
1. The domain layer has no reference to EF Core, ASP.NET Core or OpenFGA.
1. Commands and queries go through our own small dispatcher (CQRS-lite) with pipeline behaviours for validation, tenant context, transactions, audit and idempotency.
1. Architecture tests (NetArchTest) fail the build when a rule is broken.

## 7. Implementation details (ASP.NET Core + Angular)

The build uses .NET 10, ASP.NET Core, EF Core, PostgreSQL, OpenIddict, OpenFGA and Angular, with every dependency under a permissive or weak-copyleft open-source license.

### 7.1 Technology stack (no commercial libraries)

| Concern | Choice | License | Replaces (commercial or source-available) |
|---|---|---|---|
| Runtime | .NET 10 LTS, ASP.NET Core, EF Core 10 | MIT | — |
| Database | PostgreSQL 17+, Npgsql | PostgreSQL / PostgreSQL License | — |
| OAuth 2.1 / OIDC server | OpenIddict | Apache-2.0 | Duende IdentityServer |
| Password hashing, data protection | ASP.NET Core Identity `PasswordHasher` , Data Protection | MIT | — |
| Passkeys / WebAuthn | ASP.NET Core Identity passkey support (.NET 10) or Fido2NetLib | MIT | — |
| SAML 2.0 | ITfoxtec.Identity.Saml2 | BSD-3-Clause | Commercial SAML add-ons |
| SCIM 2.0 | Own implementation (small, spec-driven) | — | — |
| Authorization engine | OpenFGA server + `OpenFga.Sdk` | Apache-2.0 | Hosted FGA services |
| CQRS dispatcher | Own ~200-line dispatcher, or `Mediator` (source generator) | MIT | MediatR 13+ |
| Validation | FluentValidation | Apache-2.0 | — |
| Mapping | Mapperly or manual | Apache-2.0 | AutoMapper 15+ |
| Messaging | Own transactional outbox + RabbitMQ.Client (or Rebus) | Apache-2.0 / MPL-2.0 / MIT | MassTransit 9 |
| Background jobs | Quartz.NET | Apache-2.0 | Hangfire Pro |
| Cache | HybridCache + Valkey (StackExchange.Redis client) | MIT / BSD-3 | Redis 8 server |
| Secrets and keys | OpenBao or cloud KMS | MPL-2.0 | HashiCorp Vault (BSL) |
| Reverse proxy / BFF | YARP | MIT | Duende BFF |
| Observability | OpenTelemetry, Serilog, Prometheus, Grafana, Tempo, Loki | Apache-2.0 / AGPL (Grafana stack self-hosted, unmodified) | — |
| API docs | Built-in OpenAPI + Scalar | MIT | — |
| Email | MailKit | MIT | — |
| Tests | xUnit, Shouldly, NSubstitute, Testcontainers, NetArchTest, Playwright, k6 | Apache / BSD / MIT / AGPL (k6 as tool) | FluentAssertions 8, Moq |
| Frontend | Angular 20+, Angular CDK + Material, NgRx Signals, angular-auth-oidc-client | MIT | Commercial UI kits |

A CI step runs a license check (for example `dotnet-project-licenses` and `license-checker` for npm) against an allow-list and fails the build on anything else.

### 7.2 Solution structure

```
identity-platform/
├─ src/
│  ├─ BuildingBlocks/
│  │  ├─ Platform.Domain/            # Entity, AggregateRoot, ValueObject, DomainEvent, Result, strongly typed IDs
│  │  ├─ Platform.Application/       # ICommand/IQuery, dispatcher, pipeline behaviours, ITenantContext
│  │  └─ Platform.Infrastructure/    # EF base DbContext, outbox, RLS interceptor, inbox (idempotency)
│  ├─ Modules/
│  │  ├─ Tenancy/        {Domain, Application, Infrastructure, Api, Contracts}
│  │  ├─ Organization/   {…}
│  │  ├─ Identity/       {…}
│  │  ├─ Access/         {…}
│  │  ├─ Federation/     {…}
│  │  ├─ Provisioning/   {…}
│  │  ├─ Authorization/  {Application, Infrastructure (OpenFGA), Api, Contracts}
│  │  └─ Audit/          {…}
│  ├─ Hosts/
│  │  ├─ IdentityServer.Host/        # OpenIddict endpoints, Razor login/consent/MFA pages, federation callbacks
│  │  ├─ AdminApi.Host/              # management REST API for portals and automation
│  │  ├─ Authorization.Host/         # PDP (REST + gRPC)
│  │  ├─ Workers.Host/               # outbox dispatcher, OpenFGA sync, reconciliation, jobs, webhooks
│  │  └─ Bff.Host/                   # YARP BFF for the Angular portals (cookie session, token handling server-side)
│  └─ Sdk/
│     ├─ Platform.Authorization.Client/      # typed gRPC/REST client
│     └─ Platform.Authorization.AspNetCore/  # middleware, RequirePermission, decision cache, multi-issuer JWT validation
├─ web/                                     # Angular workspace
│  ├─ projects/admin-portal/
│  ├─ projects/account-portal/
│  └─ projects/shared/ {auth, permissions, ui, api-clients (generated from OpenAPI)}
├─ authz/model.fga.template + generator      # catalog → OpenFGA model
├─ tests/ {Unit, Integration, Architecture, Contract, E2E, Load}
├─ deploy/ {docker-compose, helm, k8s}
└─ docs/adr/
```

### 7.3 Multi-tenancy implementation
1. **Tenant resolution** (in this order): custom domain or subdomain → `tenant` parameter or `login_hint` domain on `/connect/authorize` → tenant picker after authentication. APIs take the tenant only from the token's `tid`, never from headers.
1. **ITenantContext** is set by middleware per request and is required by every tenant-owned command.
1. **EF Core global query filters** on `TenantId` for every tenant-owned entity.
1. **PostgreSQL row-level security** as defence in depth. A `DbConnectionInterceptor` runs `select set_config('app.tenant_id', @tid, true)` at the start of each transaction (safe with connection pooling).

```sql
ALTER TABLE access.role_assignments ENABLE ROW LEVEL SECURITY;
ALTER TABLE access.role_assignments FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON access.role_assignments
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
```

1. Platform-level jobs use a separate database role with `BYPASSRLS`, only in Workers.Host, and every such query is audited.

### 7.4 Core data model (PostgreSQL)

| Schema.table | Key columns |
|---|---|
| `tenancy.tenants` | id, slug, name, status, plan_id, parent_tenant_id, settings jsonb, created_at |
| `tenancy.tenant_domains` | tenant_id, domain, verified_at, sso_enforced |
| `tenancy.tenant_grants` | id, grantor_tenant_id, grantee_tenant_id, role_id, scope_node_id, expires_at |
| `org.node_types` | id, tenant_id, name, allowed_child_type_ids uuid[], max_depth |
| `org.nodes` | id, tenant_id, parent_id, type_id, name, code, status, inheritance_blocked, path ltree, attributes jsonb, version |
| `org.node_closure` | tenant_id, ancestor_id, descendant_id, depth (PK ancestor_id, descendant_id) |
| `identity.users` | id, email (citext, unique), email_verified, password_hash, status, security_stamp, created_at |
| `identity.credentials` | id, user_id, type (passkey, totp, recovery), data jsonb, created_at, last_used_at |
| `identity.external_logins` | user_id, provider_connection_id, subject |
| `access.memberships` | id, tenant_id, user_id, type, status, primary_node_id, secondary_node_ids uuid[] |
| `access.permissions` | key, application_id, description, implies text[] |
| `access.roles` | id, tenant_id (null = system), name, is_system, version |
| `access.role_permissions` | role_id, permission_key |
| `access.groups` , `access.group_members` | id, tenant_id, name / group_id, member_id, member_type |
| `access.role_assignments` | id, tenant_id, subject_id, subject_type, role_id, scope_node_id, mode, valid_from, valid_until, conditions jsonb, source (manual, scim, jit, grant) |
| `access.delegations` | id, tenant_id, source_assignment_id, delegate_user_id, valid_from, valid_until |
| `access.invitations` | id, tenant_id, email, node_id, role_id, token_hash, expires_at, status |
| `federation.idp_connections` | id, tenant_id, protocol, metadata jsonb, claim_mappings jsonb, enforced |
| `provisioning.scim_tokens` | id, tenant_id, token_hash, created_by, expires_at |
| `platform.outbox` | id (bigserial = revision), tenant_id, type, payload jsonb, occurred_at, processed_at |
| `audit.entries` | id, tenant_id, actor_id, impersonator_id, action, target, before jsonb, after jsonb, ip, correlation_id, at (partitioned monthly) |

**Moving a subtree in the closure table** (same transaction as `OrgNodeMoved`):

```sql
-- 1. detach the subtree from its old ancestors
DELETE FROM org.node_closure c
WHERE c.descendant_id IN (SELECT descendant_id FROM org.node_closure WHERE ancestor_id = @node)
  AND c.ancestor_id  IN (SELECT ancestor_id  FROM org.node_closure WHERE descendant_id = @node AND ancestor_id <> @node);

-- 2. attach it under the new parent
INSERT INTO org.node_closure (tenant_id, ancestor_id, descendant_id, depth)
SELECT @tenant, sup.ancestor_id, sub.descendant_id, sup.depth + sub.depth + 1
FROM org.node_closure sup
CROSS JOIN org.node_closure sub
WHERE sup.descendant_id = @newParent AND sub.ancestor_id = @node;
```

### 7.5 Identity server (OpenIddict)

```csharp
builder.Services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<ProtocolDbContext>())
    .AddServer(o =>
    {
        o.SetAuthorizationEndpointUris("connect/authorize")
         .SetPushedAuthorizationEndpointUris("connect/par")
         .SetTokenEndpointUris("connect/token")
         .SetEndSessionEndpointUris("connect/endsession")
         .SetIntrospectionEndpointUris("connect/introspect")
         .SetRevocationEndpointUris("connect/revoke")
         .SetUserInfoEndpointUris("connect/userinfo")
         .SetDeviceAuthorizationEndpointUris("connect/device");

        o.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()
         .AllowClientCredentialsFlow()
         .AllowRefreshTokenFlow()
         .AllowDeviceAuthorizationFlow()
         .AllowTokenExchangeFlow();                         // RFC 8693, handled in TokenController (section 9.3)

        o.SetEndUserVerificationEndpointUris("connect/verify"); // device flow: page where the user types the code
        o.RequirePushedAuthorizationRequests();             // optional per client; mandatory for high-security clients
        // Client authentication: client_secret_basic for simple M2M,
        // private_key_jwt (client assertions) as default for confidential clients, mTLS in Phase 3

        o.SetAccessTokenLifetime(TimeSpan.FromMinutes(10));
        o.RegisterScopes("openid", "profile", "email", "offline_access", "tenant");

        o.AddSigningKey(keyStore.CurrentSigningKey())      // rotated every 90 days, kept in OpenBao/KMS
         .AddEncryptionKey(keyStore.CurrentEncryptionKey());

        o.UseAspNetCore()
         .EnableAuthorizationEndpointPassthrough()
         .EnableTokenEndpointPassthrough()
         .EnableEndSessionEndpointPassthrough();
    })
    .AddValidation(o => { o.UseLocalServer(); o.UseAspNetCore(); });
```

Method names follow recent OpenIddict releases; confirm against the version pinned in Phase 0 (PAR and token exchange depend on the release).

**Login flow in the passthrough ****AuthorizeController****:** resolve tenant (7.3) → home-realm discovery by email domain → local credentials or upstream IdP → enforce tenant policy (MFA, allowed methods, step-up through `acr_values`) → check active membership → build the `ClaimsPrincipal` with `sub`, `tid`, `sid`, `amr` and set claim destinations (access vs ID token) → `SignIn`. Login, consent, MFA and tenant-picker pages are server-rendered Razor Pages, so no tokens touch the browser during login and a strict CSP applies.

**Custom domains and issuers:** each tenant domain becomes its own issuer. The SDK validates tokens with a multi-issuer resolver that loads allowed issuers and JWKS from the tenant registry.

### 7.6 Outbox and OpenFGA sync

```csharp
// Written in the same EF transaction as the aggregate change
public sealed record RoleAssignedIntegrationEvent(
    Guid TenantId, Guid AssignmentId, Guid SubjectId, SubjectType SubjectType,
    Guid RoleId, Guid ScopeNodeId, InheritanceMode Mode,
    DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil) : IIntegrationEvent;

public sealed class RoleAssignedTranslator(IRoleCatalogView roles) : ITupleTranslator<RoleAssignedIntegrationEvent>
{
    public TupleChangeSet Translate(RoleAssignedIntegrationEvent e)
    {
        var ra = Fga.Obj("role_assignment", e.TenantId, e.AssignmentId);
        var node = Fga.Obj("org_node", e.TenantId, e.ScopeNodeId);
        var suffix = e.Mode switch { InheritanceMode.NodeOnly => "here",
                                     InheritanceMode.DescendantsOnly => "below", _ => "subtree" };
        var set = new TupleChangeSet();
        set.Write(ra, "assignee", Fga.Subject(e.SubjectType, e.TenantId, e.SubjectId),
                  e.ValidUntil is null ? null : Fga.ActiveWindow(e.ValidFrom, e.ValidUntil));
        foreach (var p in roles.PermissionsOf(e.RoleId))
            set.Write(node, $"{p}_{suffix}", $"{ra}#assignee");
        return set;
    }
}
```

- The dispatcher reads the outbox in order per tenant, applies changes with OpenFGA `Write` in batches, records the processed revision (the **sync watermark**), and retries with backoff. Duplicate-write errors are treated as success, so delivery is idempotent.
- A dead-letter table and alert catch poison messages.
- Reconciliation (nightly, and on demand per tenant) rebuilds expected tuples from PostgreSQL and diffs them with OpenFGA `Read`.

### 7.7 SDK for applications

```csharp
builder.Services.AddPlatformAuthentication(builder.Configuration);   // multi-issuer JWT validation
builder.Services.AddPlatformAuthorization(o => o.DecisionCacheTtl = TimeSpan.FromSeconds(5));

app.MapPost("/invoices/{id:guid}/approve", ApproveInvoice)
   .RequirePermission("can_approve", r => Resource.Of("invoice", r.RouteGuid("id")),
                      ctx => new { amount = ctx.Body<ApproveRequest>().Amount });

[RequirePermission("users_manage", ResourceType = "org_node", RouteKey = "nodeId")]
public Task<IActionResult> InviteUser(Guid nodeId, InviteRequest body) { … }
```

The SDK adds an `IAuthorizationService` handler, batch checks for lists (`FilterAsync`), OpenTelemetry spans per decision, and fail-closed behaviour.

### 7.8 Angular portals

| Area | Implementation |
|---|---|
| Workspace | Angular CLI workspace with `admin-portal` , `account-portal` and `shared` libraries; standalone components, signals, NgRx SignalStore per feature |
| Auth | BFF pattern: the Angular app talks only to `Bff.Host` with an HttpOnly, SameSite=strict cookie; YARP attaches access tokens server-side. Fallback for third-party SPAs: `angular-auth-oidc-client` with code + PKCE and refresh-token rotation |
| Tenant context | Route prefix `/t/:tenantSlug/...` ; tenant switcher triggers a silent re-authorization for the new `tid` |
| Permission-aware UI | `PermissionService` loads `/v1/me/permissions?scope=` per node and caches it; structural directive `*appCan="'users_manage'; on: nodeId"` ; functional route guards. The server always enforces |
| API clients | Generated from the AdminApi OpenAPI document (NSwag or openapi-generator), never hand-written |
| Admin portal features | Platform console (tenants, plans, impersonation); tenant console (settings, domains, branding); org tree (CDK tree, drag-and-drop move with impact preview); users, groups, invitations; role builder (permission matrix by application); assignments (scope picker + inheritance mode + validity); SSO and SCIM wizards; clients and apps; effective-access explorer (PDP `expand` ); audit viewer with filters and export |
| Account portal features | Profile; password, passkeys, TOTP, recovery codes; sessions and devices with remote sign-out; connected apps; tenant and unit switcher; data export and account deletion |
| Quality | ESLint, strict TypeScript, i18n with Arabic RTL support, WCAG 2.2 AA, Playwright e2e per feature |

### 7.9 Security checklist
- OAuth 2.0 Security BCP: PKCE everywhere, exact redirect URI match, no implicit flow, refresh rotation with reuse detection, sender-constrained tokens (DPoP) as Phase 3 option.
- Brute-force protection and lockout per account and per IP, breached-password check (k-anonymity range API or offline list), CAPTCHA only on risk.
- Signing keys in OpenBao or KMS, JWKS rotation with overlap, Data Protection keys persisted and encrypted.
- Strict CSP, HSTS, SameSite cookies, CSRF tokens on Razor forms, no tokens in local storage.
- Audit every admin action; tamper-evident audit (hash chain per tenant per day).
- Threat model (STRIDE) per context; external penetration test before GA.

## 8. Implementation plan

The plan has five phases over about 24 weeks. Phase 0 freezes the contracts in 2 weeks; after that, up to 12 agents work in parallel, one per lane, each owning its module folder and coding against the other lanes' fakes.

### 8.1 Phases and gates

| Phase | Weeks | Goal | Exit gate |
|---|---|---|---|
| P0 Foundations | 1–2 | Skeleton, building blocks, contracts, CI, dev environment | **G0 Contracts frozen:** event schemas, AdminApi OpenAPI v1, PDP API, OpenFGA model template merged |
| P1 MVP core | 3–8 | Tenants, org tree, users, login, roles, assignments, PDP, portals | **G1 End-to-end:** a user logs in, an admin moves a branch, the PDP reflects it within 2 s; C1–C16 scenario tests green |
| P2 Enterprise | 9–14 | SSO, SCIM, custom domains, policies, webhooks, delegation | **G2 Pilot:** one enterprise tenant live in staging with SAML + SCIM |
| P3 Advanced authz & scale | 15–20 | ABAC, sharing, partner tenants, impersonation, performance | **G3 Feature complete:** all 28 cases green; NFR load targets met |
| P4 Production readiness | 21–24 | HA, DR, security testing, conformance, docs | **G4 GA:** pen test closed, OIDC conformance passed, runbooks signed off |

G0 is the gate that matters most: once contracts freeze, every lane can build against fakes without waiting for the others.

### 8.2 Lanes (one agent each)

| Lane | Owns | Folder(s) |
|---|---|---|
| L0 Architecture | Building blocks, ADRs, contracts review, docs | `src/BuildingBlocks` , `docs/adr` |
| L1 Tenancy & Org | Tenancy and Organization modules | `src/Modules/Tenancy` , `src/Modules/Organization` |
| L2 Identity | Users, credentials, MFA, passkeys, GDPR | `src/Modules/Identity` |
| L3 Protocol | OpenIddict host, login UI, sessions, tokens | `src/Hosts/IdentityServer.Host` |
| L4 Access | Memberships, roles, groups, assignments, delegations | `src/Modules/Access` |
| L5 Authorization | OpenFGA model + generator, PDP, sync worker, SDK | `authz/` , `src/Modules/Authorization` , `src/Sdk` , `src/Hosts/Authorization.Host` |
| L6 Federation & Provisioning | Upstream IdPs, HRD, JIT, SCIM | `src/Modules/Federation` , `src/Modules/Provisioning` |
| L7 Audit & Events | Audit, notifications, webhooks | `src/Modules/Audit` , `src/Hosts/Workers.Host` (jobs) |
| L8 Admin portal | Angular admin portal + shared libs | `web/projects/admin-portal` , `web/projects/shared` |
| L9 Account portal & BFF | Angular account portal, BFF host | `web/projects/account-portal` , `src/Hosts/Bff.Host` |
| L10 Platform & Security | CI/CD, infra, observability, security testing | `deploy/` , `.github/` or CI folder |
| L11 Quality | Test fixtures, scenario suites, e2e, contract and load tests | `tests/` (shared fixtures, E2E, Load) |

### 8.3 Rules for agents working in parallel
1. **Contracts first.** After G0, a change to any `Contracts` project, OpenAPI document, PDP API or the model template needs a contract-change PR approved by L0 and every consuming lane.
1. **Ownership.** A lane edits only its own folders (enforced by CODEOWNERS). Cross-module needs go through `Contracts` and integration events.
1. **Fakes, not waiting.** Each module ships an in-memory fake of its contracts in Phase 0 or its first task (`FakePdpClient`, `FakeTenantDirectory`, `FakeUserDirectory`). In the dependency column below, **(c)** means “only the contract is needed”, so work can start against the fake.
1. **Small units.** One task = one branch = one PR, ideally under 600 changed lines, trunk-based with feature flags for unfinished features.
1. **Claiming.** An agent claims a task by setting its owner on the board; a task may start only when its dependencies are done or marked (c) and frozen.
1. **Migrations.** Each module has its own EF migrations folder and schema; migration names carry the module prefix to avoid merge conflicts.
1. **Integration rhythm.** Main is built and deployed to the shared dev environment on every merge; consumer-driven contract tests (PactNet) run between portals and AdminApi, and between SDK and PDP.
1. **Global Definition of Done.** Domain invariants unit-tested; integration test with Testcontainers; architecture and license checks pass; OpenAPI and events documented; audit events emitted for privileged actions; reviewed by another lane; ADR updated if a decision changed.

### 8.4 Task breakdown

**P0 Foundations (weeks 1–2)**

| ID | Task | Lane | Depends on | Done when |
|---|---|---|---|---|
| F-01 | Repo, solution skeleton, module template, central package management | L0 | — | `dotnet new` template creates a module with 5 projects; builds in CI |
| F-02 | Domain and application building blocks: entities, value objects, strongly typed IDs, Result, dispatcher, pipeline behaviours | L0 | F-01 | Unit tests; sample module uses them |
| F-03 | Infrastructure blocks: base DbContext, outbox and inbox, RLS interceptor, `ITenantContext` | L0 | F-02 | Integration test proves RLS blocks cross-tenant reads |
| F-04 | ADRs 001–010 (D1–D6, isolation, issuer strategy, UUIDv7, Problem Details errors, API versioning) | L0 | — | Merged and reviewed by all lanes |
| F-05 | Permission catalog format, OpenFGA model template, generator, model tests | L5 | F-04 | `fga model test` covers C1, C2, C6, C11 |
| F-06 | Contracts freeze: integration events, AdminApi OpenAPI v1, PDP API (REST + proto), module fakes | L0 + all | F-02, F-05 | **G0** signed off |
| F-07 | CI pipeline (build, tests, arch tests, license check, SAST), docker-compose dev stack (PostgreSQL, OpenFGA, Valkey, RabbitMQ, Mailpit, OpenBao) | L10 | F-01 | One command starts the stack; CI green |
| F-08 | Observability baseline (OpenTelemetry, Serilog, Grafana dashboards) | L10 | F-07 | Traces from a request through all hosts |
| F-09 | Test fixtures: Testcontainers for PostgreSQL and OpenFGA, architecture test suite | L11 | F-03 | Fixtures used by one test per module |
| F-10 | Angular workspace, shared libs, design tokens, i18n with RTL, API client generation | L8 | F-06 | Empty portals build, lint and run e2e smoke test |

**P1 MVP core (weeks 3–8)**

| ID | Task | Lane | Depends on | Done when |
|---|---|---|---|---|
| T-01 | `Tenant` aggregate: create, suspend, reactivate, plans and limits | L1 | F-03, F-06 | API + events; BR-14 enforced |
| T-02 | Tenant domains and DNS TXT verification | L1 | T-01 | Verified domain flag drives HRD |
| O-01 | `OrgNodeType` and `OrgNode` : create, rename, closure table | L1 | T-01 (c) | Sibling-name, type and depth invariants tested |
| O-02 | Move with cycle guard, closure update, impact-preview query | L1 | O-01 | C11 scenarios pass |
| O-03 | Block/unblock inheritance, suspend, archive/restore, merge | L1 | O-02 | C6, C12–C14 pass |
| O-04 | CSV import with limits and dry run | L1 | O-01 | 10,000-node import < 30 s |
| I-01 | `User` aggregate: registration, email verification, password hashing, breach check | L2 | F-03 | Events published; lockout tested |
| I-02 | TOTP and recovery codes | L2 | I-01 | Enrol, verify, reset flows tested |
| I-03 | Passkeys (WebAuthn) | L2 | I-01 | Register and sign in with passkey in e2e |
| I-04 | Account recovery, lockout, security stamp | L2 | I-01 | Credential change revokes other sessions |
| P-01 | OpenIddict setup, key store (OpenBao), discovery, JWKS, client registry | L3 | F-03 | Discovery document valid; keys rotate in test |
| P-02 | Authorize and token passthrough, tenant resolution, Razor login, MFA and tenant-picker pages | L3 | P-01, I-01 (c), T-01 (c) | UC-04 passes end to end |
| P-03 | Sessions, refresh rotation with reuse detection, revocation, back-channel logout | L3 | P-02 | BR-13 within 60 s |
| P-04 | Client credentials and service accounts | L3 | P-01 | Machine token carries `tid` |
| A-01 | `Membership` and invitations | L4 | T-01 (c), I-01 (c) | UC-03 flow; BR-02 last-owner rule |
| A-02 | Permission catalog registration API; `Role` aggregate (system + custom) | L4 | F-05 | BR-08, BR-09 enforced |
| A-03 | `RoleAssignment` and `AssignmentPolicy` (ceiling, no self-assignment) | L4 | A-01, A-02, O-01 (c), Z-01 (c) | C15 and BR-06/07 tests |
| A-04 | Groups, nested up to depth 3 | L4 | A-01 | C8 passes |
| Z-01 | PDP host: check, batch-check, list-objects, tenant guard | L5 | F-05 | p95 < 20 ms cached in local load test |
| Z-02 | Outbox dispatcher, translators for Org and Access events, sync watermark | L5 | F-03, F-06 | Change visible in PDP < 2 s p99 |
| Z-03 | SDK: middleware, `RequirePermission` , decision cache, multi-issuer validation | L5 | Z-01 | Sample API protected; fail-closed tested |
| Z-04 | `me/permissions` and `expand` endpoints | L5 | Z-01 | UC-11 path returned |
| U-01 | Audit consumer, partitioned store, query API | L7 | F-06 | Every privileged event audited (BR-12) |
| U-02 | Email notifications with per-tenant templates | L7 | F-06 | Invite and verification emails in Mailpit |
| W-01 | BFF host (YARP, cookie session, token handling) | L9 | P-03 | Portals call APIs without tokens in the browser |
| W-02 | Admin: tenant console and org tree with drag-and-drop move and impact preview | L8 | F-10, O-02 | UC-02, UC-09 in e2e |
| W-03 | Admin: users, invitations, groups | L8 | A-01, A-04 | UC-03 in e2e |
| W-04 | Admin: role builder and assignment UI (scope, mode, validity) | L8 | A-03 | UC-08 in e2e |
| W-05 | Account portal: profile, password, MFA, passkeys, sessions | L9 | I-03, P-03, W-01 | Self-service flows in e2e |
| W-06 | Platform console: tenants and plans | L8 | T-01 | UC-01 in e2e |
| Q-01 | E2E MVP journeys (UC-01 to UC-05, UC-08 to UC-10) | L11 | W-02 to W-06 | **G1** suite green |
| Q-02 | Authorization scenario suite C1–C16 as executable tests against the real PDP | L11 | Z-02, A-03, O-03 | **G1** suite green |

**P2 Enterprise (weeks 9–14)**

| ID | Task | Lane | Depends on | Done when |
|---|---|---|---|---|
| E-01 | Upstream OIDC IdPs per tenant and home-realm discovery | L6 | P-02 | Entra ID and Google Workspace test logins pass |
| E-02 | SAML 2.0 service provider per tenant (ITfoxtec) | L6 | P-02 | Okta and AD FS test logins pass; cert expiry warning |
| E-03 | JIT provisioning with claim-to-node and claim-to-role mapping | L6 | E-01, E-02, A-03 | C26 passes |
| E-04 | SSO enforcement for verified domains | L6 | T-02, E-01 | BR-10 passes |
| E-05 | SCIM 2.0 server (Users, Groups), tokens, group-to-role mapping | L6 | A-04 | Passes a SCIM validator; UC-07 |
| E-06 | Custom domains, per-tenant branding, per-host issuer | L1 + L3 | P-02, T-02 | Branded login on custom domain; SDK validates its tokens |
| E-07 | Tenant security policies and step-up via `acr_values` | L3 | P-02, I-02 | BR-11 passes |
| E-08 | Webhooks and per-tenant event stream with signing and retries | L7 | U-01 | Delivery with HMAC; retries and dead letters |
| E-09 | Admin: SSO and SCIM wizards, domains, branding UI | L8 | E-01 to E-06 | UC-06 in e2e |
| E-10 | Time-bound assignments and deputy delegation | L4 | A-03, Z-02 | C18, C19 pass |
| E-11 | Reconciliation job, drift metric, `minRevision` consistency | L5 | Z-02 | Injected drift repaired; metric alerts |

**P3 Advanced authorization and scale (weeks 15–20)**

| ID | Task | Lane | Depends on | Done when |
|---|---|---|---|---|
| X-01 | ABAC conditions (amount, time, IP) and context in the SDK | L5 | Z-03 | C23 passes; missing context denies |
| X-02 | Resource sharing API and model template for business resources | L5 + L4 | Z-01 | C17 passes |
| X-03 | Tenant hierarchy and partner tenant grants | L1 + L4 | T-01, A-03 | C22, UC-14 pass |
| X-04 | Token exchange and impersonation with `act` and policy | L3 | P-03 | C20, UC-13 pass; audited |
| X-05 | Admin: effective-access explorer and impact previews everywhere | L8 | Z-04 | UC-11 in e2e |
| X-06 | Device flow, PAR, DPoP (optional) | L3 | P-03 | Conformance tests for each |
| X-07 | GDPR export and erasure across modules | L2 + all | module events | Export bundle and erasure verified |
| X-08 | Role edit fan-out with impact preview; guest users | L4 + L5 | A-02, Z-02 | C21, C25 pass |
| X-09 | Load and performance testing to NFRs, cache and OpenFGA tuning | L10 + L11 | Z-03 | **G3** NFR targets met |
| Q-03 | Authorization scenario suite C17–C28 | L11 | X-01 to X-08 | **G3** all 28 cases green |

**P4 Production readiness (weeks 21–24)**

| ID | Task | Lane | Depends on | Done when |
|---|---|---|---|---|
| R-01 | Helm charts, HA PostgreSQL (CloudNativePG), OpenFGA HA, backups with PITR, DR drill | L10 | X-09 | Restore drill under 1 hour RTO, 5 minutes RPO |
| R-02 | Threat model review, DAST (OWASP ZAP), dependency scanning, external pen test and fixes | L10 | G3 | No open high or critical findings |
| R-03 | SLOs, alerts, runbooks, on-call rota | L10 | R-01 | Runbooks tested in game day |
| R-04 | OpenID Foundation conformance suite for the OP profiles we support | L3 + L11 | X-06 | Conformance passed |
| R-05 | Tamper-evident audit and retention policies | L7 | U-01 | Hash chain verified by a job |
| R-06 | Developer portal, SDK docs, admin and IT-admin guides | L0 | G3 | Reviewed by a pilot customer |
| R-07 | Pilot tenant migration and go-live checklist | All | R-01 to R-06 | **G4** GA sign-off |

### 8.5 Critical path and risks
- **Critical path:** F-05 → F-06 → Z-01 → Z-02 → A-03 → Q-02 → (P2) E-03 → (P3) X-01 → Q-03 → R-02. Keep the strongest agent on L5.

| Risk | Mitigation |
|---|---|
| OpenFGA model grows large (5 relations per permission) | Generator plus model tests; group permissions per application; benchmark at 500 permissions in P1 |
| Sync lag makes the UI look wrong after changes | `minRevision` read-your-writes; UI waits on returned revision |
| Contract churn blocks parallel lanes | G0 freeze; contract-change PR process; fakes |
| OpenIddict lacks a needed feature (for example token exchange in the pinned version) | Spike in P0; upgrade path or own handler on OpenIddict's event model |
| Cross-tenant data leak | Query filters + RLS + tenant-prefixed OpenFGA IDs + dedicated leak tests in CI |
| Licence drift in dependencies | Automated allow-list check on every PR |

## 9. Endpoint catalog and machine-to-machine flows

The platform exposes four API surfaces: the identity server (OAuth/OIDC protocol and login UI), the Admin API, the SCIM server and the PDP. Token exchange and all machine-to-machine grants go through the single token endpoint, controlled by per-client policies.

### 9.1 Identity server: protocol endpoints

| Method | Path | Purpose | Standard | Phase |
|---|---|---|---|---|
| GET | `/.well-known/openid-configuration` | OIDC discovery | OIDC Discovery | P1 |
| GET | `/.well-known/oauth-authorization-server` | OAuth metadata | RFC 8414 | P1 |
| GET | `/.well-known/jwks` | Public signing keys (current + next + previous) | RFC 7517 | P1 |
| GET/POST | `/connect/authorize` | Start interactive login | OAuth 2.1, OIDC | P1 |
| POST | `/connect/par` | Pushed authorization requests | RFC 9126 | P2 |
| POST | `/connect/token` | All grants (table 9.2) | OAuth 2.1 | P1 |
| POST | `/connect/introspect` | Token status for opaque tokens and API keys | RFC 7662 | P1 |

**Grants accepted at ****/connect/token**

| Grant type | Used for | Client authentication |
|---|---|---|
| `authorization_code` (+ PKCE) | Users in web, SPA (through BFF), mobile | Confidential: `private_key_jwt` or secret; public: PKCE only |
| `refresh_token` | Session continuation; rotated on every use | Same as original client |
| `client_credentials` | Machine-to-machine | `private_key_jwt` (default), `client_secret_basic` , mTLS (P3) |
| `urn:ietf:params:oauth:grant-type:device_code` | TVs, CLIs | Public or confidential |
| `urn:ietf:params:oauth:grant-type:token-exchange` | Delegation, impersonation, cross-audience calls | Confidential only |
| `urn:ietf:params:oauth:grant-type:jwt-bearer` | Workload identity federation (Kubernetes, CI, cloud) | The assertion itself (RFC 7523) |

Every token request may carry `resource` (RFC 8707) to name the target API; the access token `aud` is restricted to it, and scopes are limited to those the API defines.

**Login UI and federation routes (Razor Pages):** `/account/login`, `/account/mfa`, `/account/select-tenant`, `/account/consent`, `/account/logout`, `/account/forgot-password`, `/account/reset-password`, `/account/verify-email`, `/account/invitations/{token}`, `/federation/oidc/{connectionId}/callback`, `/federation/saml/{connectionId}/acs`, `/federation/saml/{connectionId}/metadata`, `/federation/saml/{connectionId}/slo`.

### 9.2 Machine-to-machine scenarios

| Scenario | Grant | Resulting token | Rules |
|---|---|---|---|
| M1 Tenant integration (a customer's backend calls our APIs) | `client_credentials` | `sub` = service account, `tid` fixed to its tenant, `aud` = requested API | Service account gets roles at nodes like a user; the PDP checks it the same way |
| M2 Internal platform service acting across tenants | `client_credentials` with `tenant` parameter | `tid` = requested tenant, `azp` = platform client | Only clients flagged *platform* with an allow-list of tenants or `*` ; every token audited |
| M3 API A calls API B for a user | `token-exchange` , `subject_token` = user's access token, `resource` = B | `sub` = user, `act` = {client A}, `aud` = B, scopes narrowed, same `tid` | Allowed only by an exchange policy; chain depth ≤ 2; no refresh token |
| M4 Support impersonation (C20) | `token-exchange` with `subject_token` issued by the impersonation service and `actor_token` = support user | `sub` = target user, `act` = {support user}, 15-minute lifetime | Tenant policy may require consent; sensitive permissions denied by the PDP when `act` is present |
| M5 Workload federation (Kubernetes, GitHub Actions, cloud functions) | `jwt-bearer` , or `token-exchange` with an external OIDC token as `subject_token` | Token for the mapped service account | No stored secrets; trust is per federated issuer + subject pattern + audience |
| M6 API keys and personal access tokens | Opaque key sent as bearer; validated by introspection or exchanged for a short JWT at the gateway | `sub` = owner user or service account | Hashed at rest, scoped, expiring, revocable, shown once |

### 9.3 How token exchange is processed
1. Authenticate the client (confidential clients only, `private_key_jwt` recommended).
1. Validate `subject_token`: signature, issuer (ours or a trusted federated issuer), expiry, not revoked, session still active.
1. Validate `actor_token` if present (impersonation and delegation chains).
1. Look up an **exchange policy** in `protocol.token_exchange_policies` (client, source audience, target resource, allowed scopes, mode = delegation or impersonation, max chain depth). No matching policy = `invalid_grant`.
1. Keep the tenant: the new token's `tid` equals the subject token's `tid`. Changing tenant is never allowed through exchange.
1. Build the new principal: `sub` from the subject, nested `act` for each actor, scopes = requested ∩ policy ∩ subject scopes, `aud` = target resource, lifetime = min(remaining subject lifetime, 10 minutes), no refresh token.
1. Emit `TokenExchanged` to the audit log (client, subject, actor, audiences, scopes).

### 9.4 Admin API (REST, `/api/v1`, tenant always from the token)

| Area | Endpoints |
|---|---|
| Platform (operators only) | `/platform/tenants` CRUD, `/platform/tenants/{id}/suspend` , `/reactivate` , `/platform/plans` , `/platform/impersonation-requests` |
| Tenant settings | `/tenant` , `/tenant/domains` , `/tenant/domains/{id}/verify` , `/tenant/branding` , `/tenant/policies` (password, MFA, session, IP), `/tenant/grants` (partners) |
| Org structure | `/org/node-types` , `/org/nodes` (tree, paged by depth), `/org/nodes/{id}` PATCH, `/org/nodes/{id}/move` and `/move/preview` , `/block-inheritance` , `/unblock-inheritance` , `/suspend` , `/archive` , `/restore` , `/merge` , `/org/import` (with `dryRun` ) |
| Members and groups | `/members` , `/members/{id}` , `/members/{id}/suspend` , `/invitations` , `/invitations/{id}/resend` , `/groups` , `/groups/{id}/members` |
| Roles and access | `/applications/{appId}/permissions` (catalog), `/roles` , `/roles/{id}/impact-preview` , `/assignments` , `/assignments/{id}` , `/delegations` , `/access/explain` (proxy to PDP expand) |
| Clients and machines | `/clients` , `/clients/{id}/secrets` (rotate), `/clients/{id}/keys` (JWKS for `private_key_jwt` ), `/resources` (APIs and scopes), `/token-exchange-policies` , `/service-accounts` , `/service-accounts/{id}/federated-credentials` , `/api-keys` |
| Federation and provisioning | `/idp-connections` , `/idp-connections/{id}/test` , `/idp-connections/{id}/enforce` , `/idp-connections/{id}/claim-mappings` , `/scim/tokens` |
| Audit and events | `/audit` (filter, export), `/webhooks` , `/webhooks/{id}/deliveries` , `/webhooks/{id}/redeliver` |
| Self-service (account portal) | `/me` , `/me/email` (change with verification), `/me/password` , `/me/mfa` , `/me/passkeys` , `/me/sessions` , `/me/tenants` , `/me/apps` (consents), `/me/export` , `DELETE /me` |

### 9.5 SCIM 2.0 server

`/scim/v2/{tenantId}/Users`, `/Groups`, `/ServiceProviderConfig`, `/ResourceTypes`, `/Schemas`, and optionally `/Bulk`. Supports `PATCH`, filters (`eq`, `co`, `sw`, `and`), pagination and ETags. Authenticated with a per-tenant bearer token.

### 9.6 PDP

See section 5.4: `check`, `batch-check`, `list-objects`, `list-subjects`, `expand`, `me/permissions`, over REST and gRPC.

## 10. Gap analysis: features not yet in the design

A review against competitors (section 2) and current OAuth/OIDC standards found 41 gaps. Eight are **Must** before GA and are added to phases P0–P3 below; 26 are **Should** (P2–P3 or early after GA); 7 are **Could** (post-GA backlog).

### 10.1 Protocol and machine access

| ID | Missing feature | Seen in / why it matters | Priority | Phase | Lane |
|---|---|---|---|---|---|
| G-03 | API resource registry and resource indicators ( `aud` restricted per API, scopes per API) | Auth0 APIs, Zitadel projects; without it every token is valid for every API | Must | P1 | L3 + L4 |
| G-08 | ADR: JWT access tokens (RFC 9068 profile) vs reference tokens; reference tokens for third-party clients | Revocation speed vs PDP load; must be fixed before contracts freeze | Must | P0 | L0 |
| G-01 | Token exchange policies and delegation (scenario M3), separate from impersonation in X-04 | RFC 8693; Zitadel, Auth0 token exchange | Must | P2 | L3 |
| G-02 | `private_key_jwt` client authentication, client key registry, client secret rotation | OAuth Security BCP; removes shared secrets for M2M | Must | P2 | L3 |
| G-05 | API keys and personal access tokens (scenario M6) | Zitadel PATs, WorkOS API keys; needed for scripts and simple integrations | Should | P2 | L3 + L2 |
| G-09 | Consent screen and consent management for third-party apps (first-party apps skip consent) | Keycloak, Auth0; required once partners build apps | Should | P2 | L3 + L9 |
| G-04 | Workload identity federation ( `jwt-bearer` , scenario M5) | Entra workload identity, cloud providers; no secrets in CI/Kubernetes | Should | P3 | L3 |
| G-07 | Sender-constrained tokens: mTLS (RFC 8705) and DPoP (RFC 9449) | Financial-grade and high-security customers | Should | P3 | L3 |
| G-06 | Dynamic client registration with admin approval (RFC 7591/7592) | Self-service app onboarding, MCP and agent clients | Could | Post-GA | L3 |
| G-10 | Authorization for AI agents and MCP clients (scoped, short-lived, user-delegated) | WorkOS Agent Auth; growing demand in 2026 | Could | Post-GA | L3 + L5 |

### 10.2 Authentication and identity

| ID | Missing feature | Seen in / why it matters | Priority | Phase | Lane |
|---|---|---|---|---|---|
| G-18 | Self-signup policy per tenant: open, verified-domain only, invite-only, admin approval | Auth0 auto-membership, Clerk verified domains | Must | P1 | L1 |
| G-15 | User import with password hash migration (bcrypt, PBKDF2, scrypt) and lazy migration from a legacy store | FusionAuth, Logto bulk import; blocks adoption without it | Must | P3 | L2 |
| G-11 | Passwordless: email magic link and email OTP | Auth0, Clerk, WorkOS Magic Link | Should | P2 | L2 |
| G-12 | Social login (Google, Microsoft, Apple, GitHub) enabled per tenant | All competitors | Should | P2 | L6 |
| G-13 | Account linking and duplicate-account merge | Keycloak, Auth0 | Should | P2 | L2 |
| G-16 | Email change with re-verification; phone number as optional identifier | Standard CIAM flows | Should | P2 | L2 |
| G-17 | Terms and privacy acceptance with versions and re-acceptance | Compliance; Auth0, Keycloak required actions | Should | P2 | L2 |
| G-14 | Custom user attribute schema per tenant, progressive profiling | Zitadel metadata, FusionAuth progressive profiling | Should | P3 | L2 |

### 10.3 Security

| ID | Missing feature | Seen in / why it matters | Priority | Phase | Lane |
|---|---|---|---|---|---|
| G-21 | Rate limiting and quotas per tenant, client and IP (login, token, PDP) | NFRs need it; noisy-neighbour protection | Must | P1 | L10 |
| G-22 | Concurrent session limits, idle and absolute timeout per tenant | Entra, Keycloak session policies | Should | P2 | L3 |
| G-19 | Risk-based authentication: new-device and new-location alerts, impossible travel, bot and credential-stuffing detection | Auth0 Attack Protection, WorkOS Radar | Should | P3 | L3 |
| G-20 | Conditional access per tenant: IP allow-lists, geo rules, MFA required for admin roles | Entra Conditional Access | Should | P3 | L3 + L5 |
| G-23 | Just-in-time admin elevation: eligible roles activated for a few hours, with approval | Entra PIM | Should | P3 | L4 |
| G-24 | Per-tenant signing keys and bring-your-own-key | Enterprise and regulated tenants | Could | Post-GA | L3 + L10 |

### 10.4 Tenancy, operations and compliance

| ID | Missing feature | Seen in / why it matters | Priority | Phase | Lane |
|---|---|---|---|---|---|
| G-26 | Usage metering (monthly active users, M2M tokens, PDP checks, SSO connections) for plans and billing | Every commercial competitor prices on these | Must | P3 | L7 |
| G-28 | Per-tenant email sender and SMTP, localized email and login templates (Arabic and English, RTL) | Auth0, Keycloak themes | Should | P2 | L7 + L3 |
| G-27 | Tenant configuration export/import and sandbox (staging) tenants | Auth0 deploy CLI, Zitadel instances | Should | P3 | L1 |
| G-29 | Audit log streaming to SIEM (Splunk, Datadog, S3) | WorkOS Log Streams | Should | P3 | L7 |
| G-30 | Analytics dashboard: logins, failures, MFA adoption, active users per tenant | Auth0, Logto dashboards | Should | P3 | L8 |
| G-25 | Data residency and multi-region deployment | EU and GCC customers | Could | Post-GA | L10 |

### 10.5 Access and governance

| ID | Missing feature | Seen in / why it matters | Priority | Phase | Lane |
|---|---|---|---|---|---|
| G-34 | Default roles per node type (automatic assignment when a user is placed in a node) | Reduces manual work in large trees | Should | P2 | L4 |
| G-36 | Bulk operations: bulk invite by CSV, bulk assign and revoke | Auth0 bulk invitations | Should | P2 | L4 + L8 |
| G-31 | Access requests with approval workflow (user requests a role, scope admin approves) | Entra entitlement management | Should | P3 | L4 |
| G-32 | Dynamic groups from rules on attributes (department, node type, location) | Entra dynamic groups | Should | P3 | L4 |
| G-33 | Separation-of-duties rules (two roles that cannot be held together) | Finance and audit requirements | Could | Post-GA | L4 + L5 |
| G-35 | Access reviews and certification campaigns | Entra access reviews (already out of scope for v1) | Could | Post-GA | L4 |

### 10.6 Extensibility and developer experience

| ID | Missing feature | Seen in / why it matters | Priority | Phase | Lane |
|---|---|---|---|---|---|
| G-38 | Custom claims mapping per client (profile data only, never permissions) | Keycloak mappers, Auth0 Actions | Should | P2 | L3 |
| G-41 | Hosted login customization beyond branding (per-tenant templates and CSS) | Keycloak themes, Zitadel Login V2 | Should | P2 | L3 |
| G-37 | Extensibility hooks: pre-registration, post-login, pre-token, as signed webhooks with timeouts | Auth0 Actions, Zitadel Actions | Should | P3 | L3 |
| G-39 | SDKs beyond .NET: Angular, Node.js, Java, Python (token validation + PDP client) | All competitors ship many SDKs | Should | P3 | L5 |
| G-40 | Terraform provider, configuration as code and CLI | Auth0, Zitadel, Ory Terraform providers | Could | Post-GA | L10 |

### 10.7 Effect on the plan
- The eight **Must** items become tasks with the same IDs: G-08 in P0; G-03, G-18, G-21 in P1; G-01, G-02 in P2; G-15, G-26 in P3. G-08 must be decided before **G0**, and G-03 changes the AdminApi contract, so it goes into the contract freeze.
- The 26 **Should** items add about 4 weeks if all ship before GA. Recommended: ship the P2 ones (G-05, G-09, G-11, G-12, G-13, G-16, G-17, G-22, G-28, G-34, G-36, G-38, G-41) inside P2 by spreading them across lanes L2, L3, L4, L6 and L7, and move the P3 ones to a GA+1 release if the timeline is fixed.
- The 7 **Could** items go to the post-GA backlog and are revisited with customer demand.

## Sources
- Keycloak: Organization Groups (Apr 2026)
- Keycloak Organizations vs Realms
- Zitadel: Organizations
- Zitadel: Multi-tenancy and delegated access
- Auth0: How Organizations work
- Auth0 changelog: My Organization member management
- WorkOS: Model your B2B SaaS with organizations
- Ory Kratos: multi-tenancy in open source
- Start with Identity: Ory overview
- Microsoft Entra: multitenant organization
- CIAM Compass: Logto
- CIAM Compass: FusionAuth vs Authentik
- Clerk vs Auth0, part 2
- Duende: OpenIddict vs Duende IdentityServer
- .NET authentication options in 2026
- Authorization tools compared (OpenFGA, SpiceDB, OPA)
- MediatR and MassTransit going commercial
- NuGet licensing guardrails (AutoMapper, MediatR, MassTransit status)

