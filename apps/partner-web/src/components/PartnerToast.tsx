import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

type ToastKind = 'success' | 'error';
type Toast = { id: number; message: string; kind: ToastKind };
type ToastActions = { showToast: (message: string, kind?: ToastKind) => void; clearToast: () => void };
const PartnerToastContext = createContext<ToastActions | null>(null);

export function PartnerToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<Toast | null>(null);
  const sequence = useRef(0);
  const clearToast = useCallback(() => setToast(null), []);
  const showToast = useCallback((message: string, kind: ToastKind = 'success') => {
    if (!message.trim()) return;
    setToast({ id: ++sequence.current, message, kind });
  }, []);
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => {
      setToast(current => current?.id === toast.id ? null : current);
    }, 3000);
    return () => window.clearTimeout(timer);
  }, [toast]);
  const actions = useMemo(() => ({ showToast, clearToast }), [showToast, clearToast]);

  return <PartnerToastContext.Provider value={actions}>
    {children}
    {toast && createPortal(<div key={toast.id} className={`partner-toast partner-toast--${toast.kind}`}
      role={toast.kind === 'error' ? 'alert' : 'status'} aria-atomic="true">
      <span className="partner-toast-icon" aria-hidden="true">{toast.kind === 'error' ? '!' : '✓'}</span>
      <span className="partner-toast-message">{toast.message}</span>
      <button type="button" className="partner-toast-close" aria-label="Đóng thông báo" onClick={clearToast}>×</button>
    </div>, document.body)}
  </PartnerToastContext.Provider>;
}

export function usePartnerToast(): ToastActions {
  const context = useContext(PartnerToastContext);
  if (!context) throw new Error('usePartnerToast requires PartnerToastProvider');
  return context;
}
