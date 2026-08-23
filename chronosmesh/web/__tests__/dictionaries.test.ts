import { describe, it, expect } from 'vitest';
import { dictionaries, locales, isRtl, translate } from '../lib/i18n/dictionaries';

describe('i18n dictionaries', () => {
  it('has all three locales with matching key sets', () => {
    const enKeys = Object.keys(dictionaries.en).sort();
    for (const locale of locales) {
      const keys = Object.keys(dictionaries[locale]).sort();
      expect(keys).toEqual(enKeys);
    }
  });

  it('marks only Persian as RTL', () => {
    expect(isRtl('fa')).toBe(true);
    expect(isRtl('en')).toBe(false);
    expect(isRtl('zh')).toBe(false);
  });

  it('falls back to English for unknown keys', () => {
    expect(translate('fa', 'nonexistent.key')).toBe('nonexistent.key');
  });

  it('translates a known key into each language', () => {
    expect(translate('en', 'nav.dashboard')).toBe('Dashboard');
    expect(translate('fa', 'nav.dashboard')).toBe('داشبورد');
    expect(translate('zh', 'nav.dashboard')).toBe('仪表盘');
  });
});
