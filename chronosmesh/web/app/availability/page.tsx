'use client';

import { useState } from 'react';
import { AppShell } from '@/components/AppShell';
import { useI18n } from '@/lib/i18n/I18nProvider';

interface BreakRange { start: string; end: string; }
interface DayConfig {
  weekday: number; // 0=Mon..6=Sun
  enabled: boolean;
  start: string;
  end: string;
  breaks: BreakRange[];
}

const DAY_KEYS = ['day.mon', 'day.tue', 'day.wed', 'day.thu', 'day.fri', 'day.sat', 'day.sun'];

function defaultDays(): DayConfig[] {
  return DAY_KEYS.map((_, i) => ({
    weekday: i,
    enabled: i < 5,
    start: '09:00',
    end: '17:00',
    breaks: i < 5 ? [{ start: '12:30', end: '13:30' }] : [],
  }));
}

interface AvailabilitySummary {
  nextAvailableSlot?: { startUtc: string; endUtc: string };
  totalFreeMinutesToday: number;
  totalFreeMinutesWeek: number;
  remainingWorkingMinutesToday: number;
  minutesUntilNextAvailable?: number;
}

export default function AvailabilityPage() {
  const { t } = useI18n();
  const [days, setDays] = useState<DayConfig[]>(defaultDays());
  const [saving, setSaving] = useState(false);
  const [savedMessage, setSavedMessage] = useState<string | null>(null);
  const [summary, setSummary] = useState<AvailabilitySummary | null>(null);

  const updateDay = (weekday: number, patch: Partial<DayConfig>) => {
    setDays((prev) => prev.map((d) => (d.weekday === weekday ? { ...d, ...patch } : d)));
  };

  const addBreak = (weekday: number) => {
    updateDay(weekday, {
      breaks: [...days.find((d) => d.weekday === weekday)!.breaks, { start: '12:00', end: '13:00' }],
    });
  };

  const removeBreak = (weekday: number, index: number) => {
    const day = days.find((d) => d.weekday === weekday)!;
    updateDay(weekday, { breaks: day.breaks.filter((_, i) => i !== index) });
  };

  const toMinutes = (hhmm: string) => {
    const [h, m] = hhmm.split(':').map(Number);
    return h * 60 + m;
  };

  const save = async () => {
    setSaving(true);
    setSavedMessage(null);
    try {
      const payload = {
        timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
        workingDays: days
          .filter((d) => d.enabled)
          .map((d) => ({
            weekday: d.weekday,
            startMinute: toMinutes(d.start),
            endMinute: toMinutes(d.end),
            breaks: d.breaks.map((b) => [toMinutes(b.start), toMinutes(b.end)]),
          })),
      };

      const apiUrl = process.env.NEXT_PUBLIC_API_URL;
      const token = typeof window !== 'undefined' ? window.localStorage.getItem('chronosmesh.accessToken') : null;

      const res = await fetch(`${apiUrl}/api/v1/schedules/me`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error(`Save failed (${res.status})`);
      setSavedMessage(t('availability.saved'));

      const summaryRes = await fetch(`${apiUrl}/api/v1/availability/me/summary`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (summaryRes.ok) {
        setSummary(await summaryRes.json());
      }
    } catch (err) {
      setSavedMessage(err instanceof Error ? err.message : 'Error saving working hours.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <AppShell>
      <h1>{t('availability.title')}</h1>
      <p className="cm-muted">{t('availability.subtitle')}</p>

      <div className="cm-card">
        {days.map((day, i) => (
          <div key={day.weekday} className="cm-day-row">
            <label className="cm-day-label">
              <input
                type="checkbox"
                checked={day.enabled}
                onChange={(e) => updateDay(day.weekday, { enabled: e.target.checked })}
              />{' '}
              {t(DAY_KEYS[i])}
            </label>

            {day.enabled && (
              <>
                <span className="cm-muted">{t('availability.start')}</span>
                <input
                  type="time"
                  className="cm-input"
                  value={day.start}
                  onChange={(e) => updateDay(day.weekday, { start: e.target.value })}
                />
                <span className="cm-muted">{t('availability.end')}</span>
                <input
                  type="time"
                  className="cm-input"
                  value={day.end}
                  onChange={(e) => updateDay(day.weekday, { end: e.target.value })}
                />

                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  {day.breaks.map((brk, bi) => (
                    <span key={bi} style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                      <input
                        type="time"
                        className="cm-input"
                        value={brk.start}
                        onChange={(e) => {
                          const updated = [...day.breaks];
                          updated[bi] = { ...updated[bi], start: e.target.value };
                          updateDay(day.weekday, { breaks: updated });
                        }}
                      />
                      –
                      <input
                        type="time"
                        className="cm-input"
                        value={brk.end}
                        onChange={(e) => {
                          const updated = [...day.breaks];
                          updated[bi] = { ...updated[bi], end: e.target.value };
                          updateDay(day.weekday, { breaks: updated });
                        }}
                      />
                      <button className="cm-button-secondary" onClick={() => removeBreak(day.weekday, bi)}>×</button>
                    </span>
                  ))}
                  <button className="cm-button-secondary" onClick={() => addBreak(day.weekday)}>
                    {t('availability.addBreak')}
                  </button>
                </div>
              </>
            )}
          </div>
        ))}

        <div style={{ marginTop: 16, display: 'flex', justifyContent: 'flex-end', gap: 12, alignItems: 'center' }}>
          {savedMessage && <span className="cm-muted">{savedMessage}</span>}
          <button className="cm-button-primary" onClick={save} disabled={saving}>
            {t('availability.save')}
          </button>
        </div>
      </div>

      {summary && (
        <div className="cm-card">
          <h3>{t('availability.summary')}</h3>
          <p>{t('availability.freeToday')}: {summary.totalFreeMinutesToday} min</p>
          <p>{t('availability.freeWeek')}: {summary.totalFreeMinutesWeek} min</p>
          <p>{t('availability.remainingToday')}: {summary.remainingWorkingMinutesToday} min</p>
          {summary.nextAvailableSlot && (
            <p>{t('availability.nextSlot')}: {new Date(summary.nextAvailableSlot.startUtc).toLocaleString()}</p>
          )}
        </div>
      )}
    </AppShell>
  );
}
