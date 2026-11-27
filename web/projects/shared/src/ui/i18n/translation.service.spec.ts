import { TestBed } from '@angular/core/testing';
import { provideI18n } from './provide-i18n';
import { TranslationService } from './translation.service';

describe('TranslationService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.lang = '';
    document.documentElement.dir = '';
    TestBed.configureTestingModule({
      providers: [
        provideI18n({
          en: { hello: 'Hello, {name}', onlyEnglish: 'Fallback' },
          ar: { hello: 'مرحبًا، {name}' },
        }),
      ],
    });
  });

  it('mirrors the document for Arabic and restores it for English', () => {
    const i18n = TestBed.inject(TranslationService);
    i18n.use('ar');
    TestBed.tick();
    expect(document.documentElement.dir).toBe('rtl');
    expect(document.documentElement.lang).toBe('ar');
    expect(i18n.dir()).toBe('rtl');

    i18n.toggle();
    TestBed.tick();
    expect(document.documentElement.dir).toBe('ltr');
    expect(document.documentElement.lang).toBe('en');
  });

  it('translates with parameters and falls back to English, then to the key', () => {
    const i18n = TestBed.inject(TranslationService);
    i18n.use('ar');
    expect(i18n.translate('hello', { name: 'منى' })).toBe('مرحبًا، منى');
    expect(i18n.translate('onlyEnglish')).toBe('Fallback');
    expect(i18n.translate('missing.key')).toBe('missing.key');
  });

  it('includes the shared shell texts', () => {
    const i18n = TestBed.inject(TranslationService);
    i18n.use('en');
    expect(i18n.translate('shell.signOut')).toBe('Sign out');
  });

  it('remembers the chosen language', () => {
    TestBed.inject(TranslationService).use('ar');
    expect(localStorage.getItem('ip.locale')).toBe('ar');
  });
});
