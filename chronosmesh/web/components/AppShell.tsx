'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useI18n } from '@/lib/i18n/I18nProvider';
import { useTheme, THEME_NAMES, ThemeName } from '@/components/Theme/ThemeProvider';
import { Locale } from '@/lib/i18n/dictionaries';

const NAV_ITEMS: { href: string; key: string }[] = [
  { href: '/dashboard', key: 'nav.dashboard' },
  { href: '/calendar', key: 'nav.calendar' },
  { href: '/tasks', key: 'nav.tasks' },
  { href: '/availability', key: 'nav.availability' },
  { href: '/analytics', key: 'nav.analytics' },
  { href: '/settings', key: 'nav.settings' },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const { t, locale, setLocale } = useI18n();
  const { theme, setTheme } = useTheme();
  const pathname = usePathname();

  return (
    <div className="cm-shell">
      <aside className="cm-sidebar">
        <div style={{ fontWeight: 700, fontSize: 18, padding: '8px 14px 20px' }}>{t('app.title')}</div>
        <nav>
          {NAV_ITEMS.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={`cm-nav-item ${pathname?.startsWith(item.href) ? 'active' : ''}`}
            >
              {t(item.key)}
            </Link>
          ))}
        </nav>

        <div style={{ marginTop: 32, padding: '0 14px' }}>
          <label className="cm-muted" style={{ fontSize: 12 }}>Theme</label>
          <select
            className="cm-input"
            style={{ width: '100%', marginTop: 4 }}
            value={theme}
            onChange={(e) => setTheme(e.target.value as ThemeName)}
          >
            {THEME_NAMES.map((name) => (
              <option key={name} value={name}>{t(`theme.${name}`)}</option>
            ))}
          </select>
        </div>

        <div style={{ marginTop: 16, padding: '0 14px' }}>
          <label className="cm-muted" style={{ fontSize: 12 }}>Language / زبان / 语言</label>
          <select
            className="cm-input"
            style={{ width: '100%', marginTop: 4 }}
            value={locale}
            onChange={(e) => setLocale(e.target.value as Locale)}
          >
            <option value="en">English</option>
            <option value="fa">فارسی</option>
            <option value="zh">中文</option>
          </select>
        </div>
      </aside>
      <main className="cm-main">{children}</main>
    </div>
  );
}
