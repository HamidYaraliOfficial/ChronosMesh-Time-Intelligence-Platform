'use client';
import { AppShell } from '@/components/AppShell';
import { useI18n } from '@/lib/i18n/I18nProvider';

export default function DashboardPage() {
  const { t } = useI18n();
  return (
    <AppShell>
      <h1>{t('nav.dashboard')}</h1>
      <div className="cm-card">
        <p className="cm-muted">
          Summary cards (working hours, meetings, free time, tasks due) are populated at runtime from
          <code> GET /api/v1/analytics/summary</code> and <code>GET /api/v1/availability/me/summary</code>.
        </p>
      </div>
    </AppShell>
  );
}
