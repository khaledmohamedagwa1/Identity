import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { BffAuthService } from '@ip/shared/auth';
import { TranslatePipe } from '../i18n/translate.pipe';

/**
 * Generic message page for forbidden / not found / signed out / no tenant routes.
 * Configure through route data: `{ titleKey, bodyKey, action: 'home' | 'signIn' }` (needs withComponentInputBinding).
 */
@Component({
  selector: 'ip-status-page',
  imports: [MatButtonModule, RouterLink, TranslatePipe],
  template: `
    <section class="status-page" [attr.aria-labelledby]="'status-title'">
      <h1 id="status-title" data-testid="status-title">{{ titleKey() | t }}</h1>
      <p>{{ bodyKey() | t }}</p>
      @if (action() === 'signIn') {
        <button mat-flat-button type="button" (click)="auth.login('/')">
          {{ 'shell.signIn' | t }}
        </button>
      } @else if (action() === 'home') {
        <a mat-flat-button routerLink="/">{{ 'page.home' | t }}</a>
      }
    </section>
  `,
  styles: `
    .status-page {
      display: grid;
      gap: 16px;
      justify-items: start;
      max-width: 560px;
      margin: 64px auto;
      padding-inline: 24px;
    }
    h1 {
      font: var(--mat-sys-headline-medium);
      margin: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusPageComponent {
  protected readonly auth = inject(BffAuthService);
  readonly titleKey = input.required<string>();
  readonly bodyKey = input.required<string>();
  readonly action = input<'home' | 'signIn' | 'none'>('none');
}
