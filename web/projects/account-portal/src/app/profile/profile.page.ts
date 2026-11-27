import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { BffAuthService } from '@ip/shared/auth';
import { TranslatePipe } from '@ip/shared/ui';

/** The user's own profile. Editing and security settings arrive with W-05. */
@Component({
  selector: 'acc-profile',
  imports: [MatCardModule, TranslatePipe],
  template: `
    <h1 data-testid="page-title">{{ 'profile.title' | t }}</h1>
    @if (auth.user(); as user) {
      <mat-card appearance="outlined">
        <mat-card-content>
          <dl>
            <dt>{{ 'profile.name' | t }}</dt>
            <dd>{{ user.name ?? '—' }}</dd>
            <dt>{{ 'profile.email' | t }}</dt>
            <dd>{{ user.email ?? '—' }}</dd>
            <dt>{{ 'profile.tenants' | t }}</dt>
            <dd>
              @for (tenant of user.tenants; track tenant.id; let last = $last) {
                {{ tenant.name }}{{ last ? '' : '، ' }}
              } @empty {
                —
              }
            </dd>
          </dl>
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-medium);
      margin-block: 0 24px;
    }
    dl {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 8px 24px;
      margin: 0;
    }
    dt {
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage {
  protected readonly auth = inject(BffAuthService);
}
