import { useEffect, useRef, useState } from 'react';

/** One request at a time; pause in a hidden tab and revalidate on focus. */
export function useVisiblePolling<T>(enabled: boolean, resource: string,
  read: (signal: AbortSignal) => Promise<T>, onValue: (value: T) => void, onError: (reason: unknown) => void) {
  const callbacks = useRef({ read, onValue, onError }); callbacks.current = { read, onValue, onError };
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!enabled) return;
    let active = true; let busy = false; let failures = 0; let timer: ReturnType<typeof setTimeout> | undefined;
    let controller: AbortController | undefined;
    const load = async () => {
      if (!active || busy || document.visibilityState === 'hidden') return;
      if (timer) clearTimeout(timer);
      busy = true; controller = new AbortController();
      try { const value = await callbacks.current.read(controller.signal); if (active) { failures = 0; callbacks.current.onValue(value); } }
      catch (reason) { if (active && !controller.signal.aborted) { failures++; callbacks.current.onError(reason); } }
      finally { busy = false; if (active) timer = setTimeout(() => { void load(); }, Math.min(30000, 5000 * 2 ** Math.min(failures, 3))); }
    };
    const wake = () => { void load(); };
    void load(); window.addEventListener('focus', wake); document.addEventListener('visibilitychange', wake);
    return () => { active = false; controller?.abort(); if (timer) clearTimeout(timer);
      window.removeEventListener('focus', wake); document.removeEventListener('visibilitychange', wake); };
  }, [enabled, resource, revision]);
  return () => setRevision(value => value + 1);
}
