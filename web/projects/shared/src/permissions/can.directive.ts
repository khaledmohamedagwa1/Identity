import { Directive, effect, inject, input, TemplateRef, ViewContainerRef } from '@angular/core';
import { PermissionService } from './permission.service';

/**
 * Renders its template only when the user has a permission at a node (spec 7.8):
 *
 * ```html
 * <button *appCan="'users_manage'; on: nodeId">Invite</button>
 * <section *appCan="'roles_manage'; else readOnly">...</section>
 * ```
 *
 * Without `on` the check runs at the tenant root. The server always enforces; this only hides UI.
 */
@Directive({ selector: '[appCan]' })
export class CanDirective {
  private readonly permissions = inject(PermissionService);
  private readonly template = inject<TemplateRef<unknown>>(TemplateRef);
  private readonly container = inject(ViewContainerRef);

  readonly appCan = input.required<string>();
  readonly appCanOn = input<string | null | undefined>(undefined);
  readonly appCanElse = input<TemplateRef<unknown> | null>(null);

  private shown: 'main' | 'else' | 'none' = 'none';

  constructor() {
    effect(() => {
      const allowed = this.permissions.can(this.appCan(), this.appCanOn() ?? undefined)();
      const elseTemplate = this.appCanElse();
      const want = allowed ? 'main' : elseTemplate ? 'else' : 'none';
      if (want === this.shown) {
        return;
      }
      this.container.clear();
      if (want === 'main') {
        this.container.createEmbeddedView(this.template);
      } else if (want === 'else' && elseTemplate) {
        this.container.createEmbeddedView(elseTemplate);
      }
      this.shown = want;
    });
  }
}
