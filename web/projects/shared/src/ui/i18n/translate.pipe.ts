import { inject, Pipe, type PipeTransform } from '@angular/core';
import { TranslationService } from './translation.service';

/** `{{ 'nav.overview' | t }}` or `{{ 'greeting' | t: { name: user.name } }}`. Impure so it follows locale changes. */
@Pipe({ name: 't', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(key: string, params?: Readonly<Record<string, string | number>>): string {
    return this.i18n.translate(key, params);
  }
}
