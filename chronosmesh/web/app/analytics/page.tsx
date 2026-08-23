'use client';
import { AppShell } from '@/components/AppShell';
import { useI18n } from '@/lib/i18n/I18nProvider';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';

const sampleData = [
  { name: 'Mon', working: 8, meetings: 3, free: 1 },
  { name: 'Tue', working: 8, meetings: 2, free: 2 },
  { name: 'Wed', working: 8, meetings: 4, free: 0 },
  { name: 'Thu', working: 8, meetings: 1, free: 3 },
  { name: 'Fri', working: 8, meetings: 2, free: 2 },
];

export default function AnalyticsPage() {
  const { t } = useI18n();
  return (
    <AppShell>
      <h1>{t('nav.analytics')}</h1>
      <div className="cm-card" style={{ height: 320 }}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={sampleData}>
            <XAxis dataKey="name" />
            <YAxis />
            <Tooltip />
            <Bar dataKey="working" fill="var(--cm-accent)" />
            <Bar dataKey="meetings" fill="var(--cm-muted)" />
            <Bar dataKey="free" fill="var(--cm-border)" />
          </BarChart>
        </ResponsiveContainer>
      </div>
      <p className="cm-muted">Sample chart — real data comes from GET /api/v1/analytics.</p>
    </AppShell>
  );
}
