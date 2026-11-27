import { DOCUMENT } from '@angular/common';
import { computed, effect, inject, Injectable, InjectionToken, signal } from '@angular/core';
import type { Direction } from '@angular/cdk/bidi';

export type Locale = 'en' | 'ar';
export const SUPPORTED_LOCALES: readonly Locale[] = ['en', 'ar'];
const RTL_LOCALES: ReadonlySet<Locale> = new Set<Locale>(['ar']);
const STORAGE_KEY = 'ip.locale';

/** Flat key → text catalog. `{name}` placeholders are replaced by `translate(key, { name })`. */
export type Catalog = Readonly<Record<string, string>>;
export type Catalogs = Readonly<Record<Locale, Catalog>>;

export const I18N_CATALOGS = new InjectionToken<Catalogs[]>('I18N_CATALOGS');

/**
 * Runtime i18n for English and Arabic (spec 7.8, F-10). Switching language does not reload the app:
 * `<html lang dir>` and the CDK direction follow the active locale, so Material mirrors for RTL.
 */
@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly document = inject(DOCUMENT);
  private readonly catalogs = inject(I18N_CATALOGS, { optional: true }) ?? [];

  private readonly current = signal<Locale>(this.initialLocale());

  readonly locale = this.current.asReadonly();
  readonly dir = computed<Direction>(() => (RTL_LOCALES.has(this.current()) ? 'rtl' : 'ltr'));
  private readonly merged = computed<Catalog>(
    () => Object.assign({}, ...this.catalogs.map((c) => c[this.current()])) as Catalog,
  );
  private readonly fallback = computed<Catalog>(
    () => Object.assign({}, ...this.catalogs.map((c) => c.en)) as Catalog,
  );

  constructor() {
    effect(() => {
      const root = this.document.documentElement;
      root.lang = this.current();
      root.dir = this.dir();
    });
  }

  use(locale: Locale): void {
    this.current.set(locale);
    try {
      this.document.defaultView?.localStorage.setItem(STORAGE_KEY, locale);
    } catch {
      // Storage can be unavailable (private mode); the choice then lasts for this page only.
    }
  }

  toggle(): void {
    this.use(this.current() === 'ar' ? 'en' : 'ar');
  }

  /** Reactive when called in a template or computed, because it reads the locale signal. */
  translate(key: string, params?: Readonly<Record<string, string | number>>): string {
    const text = this.merged()[key] ?? this.fallback()[key] ?? key;
    if (!params) {
      return text;
    }
    return text.replace(/\{(\w+)\}/g, (match, name: string) =>
      name in params ? String(params[name]) : match,
    );
  }

  private initialLocale(): Locale {
    const win = this.document.defaultView;
    const fromQuery = win ? new URLSearchParams(win.location.search).get('lang') : null;
    if (isLocale(fromQuery)) {
      return fromQuery;
    }
    const stored = readStorage(win);
    if (isLocale(stored)) {
      return stored;
    }
    return win?.navigator.language.toLowerCase().startsWith('ar') ? 'ar' : 'en';
  }
}

function readStorage(win: Window | null): string | null {
  try {
    return win?.localStorage.getItem(STORAGE_KEY) ?? null;
  } catch {
    return null;
  }
}

function isLocale(value: string | null): value is Locale {
  return value !== null && (SUPPORTED_LOCALES as readonly string[]).includes(value);
}
