'use client';

import { useEffect, useRef, useState } from 'react';

export interface RealtimeEvent {
  type: string;
  workspace_id: string;
  actor_user_id: string;
  entity_id: string;
  payload?: unknown;
  timestamp: string;
}

/**
 * Connects to the Go Scheduler's WebSocket hub for a given workspace and
 * surfaces every broadcast Real-Time event (calendar changes, task moves,
 * booking confirmations, notifications) as they arrive — no polling.
 * Reconnects automatically with capped exponential backoff if the
 * connection drops.
 */
export function useRealtime(workspaceId: string | null, userId: string | null) {
  const [lastEvent, setLastEvent] = useState<RealtimeEvent | null>(null);
  const [connected, setConnected] = useState(false);
  const socketRef = useRef<WebSocket | null>(null);
  const attemptRef = useRef(0);

  useEffect(() => {
    if (!workspaceId || !userId) return;
    let cancelled = false;

    function connect() {
      const base = process.env.NEXT_PUBLIC_SCHEDULER_WS_URL || 'wss://localhost:8443/v1/ws';
      const url = `${base}?workspace_id=${encodeURIComponent(workspaceId!)}&user_id=${encodeURIComponent(userId!)}`;
      const socket = new WebSocket(url);
      socketRef.current = socket;

      socket.onopen = () => {
        if (cancelled) return;
        attemptRef.current = 0;
        setConnected(true);
      };
      socket.onmessage = (msg) => {
        if (cancelled) return;
        try {
          setLastEvent(JSON.parse(msg.data));
        } catch {
          // ignore malformed frames
        }
      };
      socket.onclose = () => {
        if (cancelled) return;
        setConnected(false);
        const delay = Math.min(30000, 1000 * 2 ** attemptRef.current);
        attemptRef.current += 1;
        setTimeout(connect, delay);
      };
      socket.onerror = () => socket.close();
    }

    connect();
    return () => {
      cancelled = true;
      socketRef.current?.close();
    };
  }, [workspaceId, userId]);

  return { lastEvent, connected };
}
