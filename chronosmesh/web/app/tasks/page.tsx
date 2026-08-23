'use client';
import { useEffect, useState } from 'react';
import { AppShell } from '@/components/AppShell';
import { useI18n } from '@/lib/i18n/I18nProvider';

interface TaskDto {
  id: string;
  title: string;
  durationMinutes: number;
  deadlineUtc?: string;
  priority: number;
  status: string;
  splittable: boolean;
}

export default function TasksPage() {
  const { t } = useI18n();
  const [tasks, setTasks] = useState<TaskDto[]>([]);

  useEffect(() => {
    const apiUrl = process.env.NEXT_PUBLIC_API_URL;
    const token = typeof window !== 'undefined' ? window.localStorage.getItem('chronosmesh.accessToken') : null;
    fetch(`${apiUrl}/api/v1/tasks`, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
      .then((r) => (r.ok ? r.json() : []))
      .then(setTasks)
      .catch(() => setTasks([]));
  }, []);

  return (
    <AppShell>
      <h1>{t('nav.tasks')}</h1>
      <div className="cm-card">
        {tasks.length === 0 && <p className="cm-muted">No tasks yet — create one via POST /api/v1/tasks.</p>}
        {tasks.map((task) => (
          <div key={task.id} className="cm-day-row">
            <strong>{task.title}</strong>
            <span className="cm-muted">{task.durationMinutes} min</span>
            <span className="cm-muted">{task.status}</span>
          </div>
        ))}
      </div>
    </AppShell>
  );
}
