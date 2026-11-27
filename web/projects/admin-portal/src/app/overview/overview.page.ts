import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { BffAuthService } from '@ip/shared/auth';
import { TranslatePipe } from '@ip/shared/ui';

/** Landing page of a tenant in the admin portal. Feature tiles arrive with W-02..W-06. */
@Component({
  selector: 'adm-overview',
  imports: [MatCardModule, TranslatePipe],
  template: `
    <h1 data-testid="page-title">{{ 'overview.title' | t }}</h1>
    @if (auth.user(); as user) {
      <mat-card appearance="outlined">
        <mat-card-content>
          <p>{{ 'overview.welcome' | t: { name: user.name ?? user.email ?? '' } }}</p>
          @if (user.tenant; as tenant) {
            <p>{{ 'overview.tenant' | t: { tenant: tenant.name } }}</p>
          }
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-medium);
      margin-block: 0 24px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverviewPage {
  protected readonly auth = inject(BffAuthService);
}
