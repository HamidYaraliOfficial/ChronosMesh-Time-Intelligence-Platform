'use client';

import { useMemo, useState } from 'react';
import { AppShell } from '@/components/AppShell';
import { useI18n } from '@/lib/i18n/I18nProvider';

interface CalEvent {
  id: string;
  title: string;
  startMinute: number; // minutes from midnight
  durationMinutes: number;
  day: number; // 0=Mon..6=Sun within the displayed week
  color: string;
}

const seedEvents: CalEvent[] = [
  { id: 'e1', title: 'Team standup', startMinute: 9 * 60, durationMinutes: 15, day: 0, color: '#4F46E5' },
  { id: 'e2', title: 'Client call', startMinute: 11 * 60, durationMinutes: 60, day: 1, color: '#0EA5E9' },
  { id: 'e3', title: 'Deep work', startMinute: 14 * 60, durationMinutes: 120, day: 2, color: '#10B981' },
];

const HOUR_HEIGHT = 48;

export default function CalendarPage() {
  const { t } = useI18n();
  const [view, setView] = useState<'day' | 'week' | 'month'>('week');
  const [events, setEvents] = useState<CalEvent[]>(seedEvents);
  const [dragId, setDragId] = useState<string | null>(null);

  const dayLabels = useMemo(
    () => ['day.mon', 'day.tue', 'day.wed', 'day.thu', 'day.fri', 'day.sat', 'day.sun'].map(t),
    [t],
  );

  const daysShown = view === 'day' ? 1 : 7;

  const onDragStart = (id: string) => setDragId(id);

  const onDropOnCell = (day: number, hour: number) => {
    if (!dragId) return;
    setEvents((prev) =>
      prev.map((ev) => (ev.id === dragId ? { ...ev, day, startMinute: hour * 60 } : ev)),
    );
    setDragId(null);
  };

  return (
    <AppShell>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h1>{t('nav.calendar')}</h1>
        <div style={{ display: 'flex', gap: 8 }}>
          {(['day', 'week', 'month'] as const).map((mode) => (
            <button
              key={mode}
              className={view === mode ? 'cm-button-primary' : 'cm-button-secondary'}
              onClick={() => setView(mode)}
            >
              {t(`calendar.${mode}`)}
            </button>
          ))}
        </div>
      </div>

      <div className="cm-card" style={{ overflowX: 'auto' }}>
        <div style={{ display: 'grid', gridTemplateColumns: `60px repeat(${daysShown}, 1fr)`, minWidth: 700 }}>
          <div />
          {dayLabels.slice(0, daysShown).map((label) => (
            <div key={label} style={{ fontWeight: 600, textAlign: 'center', padding: 8 }}>{label}</div>
          ))}

          {Array.from({ length: 24 }).map((_, hour) => (
            <>
              <div key={`h-${hour}`} className="cm-muted" style={{ height: HOUR_HEIGHT, textAlign: 'right', paddingInlineEnd: 6, fontSize: 12 }}>
                {String(hour).padStart(2, '0')}:00
              </div>
              {Array.from({ length: daysShown }).map((_, day) => (
                <div
                  key={`c-${hour}-${day}`}
                  onDragOver={(e) => e.preventDefault()}
                  onDrop={() => onDropOnCell(day, hour)}
                  style={{ height: HOUR_HEIGHT, borderTop: '1px solid var(--cm-border)', position: 'relative' }}
                >
                  {events
                    .filter((ev) => ev.day === day && Math.floor(ev.startMinute / 60) === hour)
                    .map((ev) => (
                      <div
                        key={ev.id}
                        draggable
                        onDragStart={() => onDragStart(ev.id)}
                        style={{
                          position: 'absolute',
                          insetInlineStart: 2,
                          insetInlineEnd: 2,
                          top: 2,
                          height: Math.max(18, (ev.durationMinutes / 60) * HOUR_HEIGHT - 4),
                          background: ev.color,
                          color: 'white',
                          borderRadius: 6,
                          padding: '2px 6px',
                          fontSize: 12,
                          cursor: 'grab',
                        }}
                      >
                        {ev.title}
                      </div>
                    ))}
                </div>
              ))}
            </>
          ))}
        </div>
      </div>
      <p className="cm-muted">Drag any event to a new day/hour cell to reschedule it (calls PATCH /api/v1/events/:id in production).</p>
    </AppShell>
  );
}
