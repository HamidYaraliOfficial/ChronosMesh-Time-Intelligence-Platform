import type { Metadata } from 'next';
import { I18nProvider } from '@/lib/i18n/I18nProvider';
import { ThemeProvider } from '@/components/Theme/ThemeProvider';
import './globals.css';

export const metadata: Metadata = {
  title: 'ChronosMesh — Time Intelligence Platform',
  description: 'Smart calendar, scheduling, availability, and booking platform.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  // lang/dir are set client-side by I18nProvider based on the persisted
  // locale; server-rendered defaults to English/LTR to avoid a flash of
  // unstyled RTL content before hydration for non-Persian users.
  return (
    <html lang="en" dir="ltr">
      <body>
        <ThemeProvider>
          <I18nProvider>{children}</I18nProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
