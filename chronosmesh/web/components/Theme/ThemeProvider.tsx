'use client';

import { createContext, useContext, useEffect, useMemo, useState, ReactNode } from 'react';

export type ThemeName = 'windows11' | 'light' | 'dark' | 'blue' | 'red';

// Each theme is a flat map of CSS custom properties applied to
// document.documentElement. Components never hard-code colors — they read
// var(--cm-*) so adding a sixth theme later is a pure data change here,
// with zero component edits (mirrors the extensibility of the Desktop
// Theme Engine in C++).
const THEMES: Record<ThemeName, Record<string, string>> = {
  windows11: {
    '--cm-bg': '#F3F3F3', '--cm-surface': '#FFFFFF', '--cm-sidebar': '#FBFBFB',
    '--cm-text': '#1B1B1B', '--cm-muted': '#6B6B6B', '--cm-border': '#E5E5E5',
    '--cm-accent': '#2B4FD8', '--cm-accent-contrast': '#FFFFFF', '--cm-accent-soft': '#E8ECFB',
  },
  light: {
    '--cm-bg': '#FFFFFF', '--cm-surface': '#FFFFFF', '--cm-sidebar': '#F7F7F8',
    '--cm-text': '#111111', '--cm-muted': '#6B7280', '--cm-border': '#EAEAEA',
    '--cm-accent': '#4338CA', '--cm-accent-contrast': '#FFFFFF', '--cm-accent-soft': '#EEF1FF',
  },
  dark: {
    '--cm-bg': '#1E1E20', '--cm-surface': '#232326', '--cm-sidebar': '#18181A',
    '--cm-text': '#E7E7E9', '--cm-muted': '#A1A1AA', '--cm-border': '#2C2C2E',
    '--cm-accent': '#6366F1', '--cm-accent-contrast': '#FFFFFF', '--cm-accent-soft': '#33345A',
  },
  blue: {
    '--cm-bg': '#F0F5FF', '--cm-surface': '#FFFFFF', '--cm-sidebar': '#E4ECFC',
    '--cm-text': '#0B1F3A', '--cm-muted': '#3E5A85', '--cm-border': '#C7D7F5',
    '--cm-accent': '#2B5FE6', '--cm-accent-contrast': '#FFFFFF', '--cm-accent-soft': '#DCE7FC',
  },
  red: {
    '--cm-bg': '#FFF3F2', '--cm-surface': '#FFFFFF', '--cm-sidebar': '#FCE4E2',
    '--cm-text': '#3A0B0B', '--cm-muted': '#8A4A46', '--cm-border': '#F5C7C3',
    '--cm-accent': '#DC2626', '--cm-accent-contrast': '#FFFFFF', '--cm-accent-soft': '#FBD9D6',
  },
};

export const THEME_NAMES: ThemeName[] = ['windows11', 'light', 'dark', 'blue', 'red'];

interface ThemeContextValue {
  theme: ThemeName;
  setTheme: (t: ThemeName) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);
const STORAGE_KEY = 'chronosmesh.theme';

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<ThemeName>('windows11');

  useEffect(() => {
    const stored = typeof window !== 'undefined' ? (window.localStorage.getItem(STORAGE_KEY) as ThemeName | null) : null;
    if (stored && THEME_NAMES.includes(stored)) setThemeState(stored);
  }, []);

  useEffect(() => {
    const vars = THEMES[theme];
    for (const [key, value] of Object.entries(vars)) {
      document.documentElement.style.setProperty(key, value);
    }
    document.documentElement.dataset.cmTheme = theme;
  }, [theme]);

  const setTheme = (t: ThemeName) => {
    setThemeState(t);
    if (typeof window !== 'undefined') window.localStorage.setItem(STORAGE_KEY, t);
  };

  const value = useMemo(() => ({ theme, setTheme }), [theme]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error('useTheme must be used within ThemeProvider');
  return ctx;
}
