import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { BidiModule } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { BffAuthService } from '@ip/shared/auth';
import { PermissionService } from '@ip/shared/permissions';
import { TranslatePipe } from '../i18n/translate.pipe';
import { TranslationService } from '../i18n/translation.service';

export interface NavItem {
  /** Router link, relative to the shell's route. */
  readonly link: string;
  readonly labelKey: string;
  /** Hide the item unless the user has this permission at the tenant root. The route must still guard itself. */
  readonly permission?: string;
  /** Match the link exactly (use for the shell's index route). */
  readonly exact?: boolean;
}

/** Application frame shared by both portals: toolbar, side navigation, language and account menus. */
@Component({
  selector: 'ip-shell',
  imports: [
    BidiModule,
    MatButtonModule,
    MatListModule,
    MatMenuModule,
    MatSidenavModule,
    MatToolbarModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    TranslatePipe,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {
  protected readonly i18n = inject(TranslationService);
  protected readonly auth = inject(BffAuthService);
  private readonly permissions = inject(PermissionService);

  readonly titleKey = input.required<string>();
  readonly navItems = input<readonly NavItem[]>([]);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe([Breakpoints.XSmall, Breakpoints.Small])
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly visibleNav = computed(() =>
    this.navItems().filter((item) => !item.permission || this.permissions.can(item.permission)()),
  );

  protected readonly otherTenants = computed(() => {
    const user = this.auth.user();
    return user ? user.tenants.filter((t) => t.id !== user.tenant?.id) : [];
  });
}
