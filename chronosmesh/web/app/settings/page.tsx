'use client';
import { AppShell } from '@/components/AppShell';
import { useI18n } from '@/lib/i18n/I18nProvider';

export default function SettingsPage() {
  const { t } = useI18n();
  return (
    <AppShell>
      <h1>{t('nav.settings')}</h1>
      <div className="cm-card">
        <p className="cm-muted">Theme and language are controlled from the sidebar and persist per-browser.</p>
      </div>
    </AppShell>
  );
}
