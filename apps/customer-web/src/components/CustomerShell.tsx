import { useEffect, useRef, type ReactNode } from 'react';
import { CustomerNav, useCustomerSession } from '../features/auth/CustomerSession';

type Props = { children: ReactNode; title?: string; eyebrow?: string; description?: string;
  className?: string; layout?: 'workspace' | 'account' };

export function CustomerShell({ children, title, eyebrow, description, className = '', layout = 'workspace' }: Props) {
  const header = useRef<HTMLElement>(null);
  const { session } = useCustomerSession();
  const path = location.pathname;
  useEffect(() => {
    const nav = header.current?.querySelector('nav');
    nav?.setAttribute('aria-label', 'Điều hướng khách đặt sân');
    nav?.querySelectorAll<HTMLAnchorElement>('a[href]').forEach(link => {
      const active = link.pathname === '/venues' ? path === '/' || path.startsWith('/venues')
        : link.pathname === '/teams' ? path.startsWith('/teams') || path === '/me/team-requests'
        : link.pathname === '/me/play-schedule' ? path === '/me/play-schedule'
        : link.pathname === '/me/bookings' ? path === '/me/bookings' || path.startsWith('/bookings/') || path.startsWith('/booking-series/')
          : link.pathname === path;
      if (active) link.setAttribute('aria-current', 'page'); else link.removeAttribute('aria-current');
    });
  }, [path, session]);

  return <div className={`customer-shell customer-app-shell ${layout === 'account' ? 'customer-account-shell' : ''} ${className}`}>
    <a className="skip-link" href="#customer-content" onClick={event => {
      event.preventDefault(); document.getElementById('customer-content')?.focus();
    }}>Chuyển đến nội dung</a>
    <header className="site-header" ref={header}>
      <a className="brand" href="/venues" aria-label="ShuttleBook — Tìm sân">
        <svg viewBox="0 0 32 32" aria-hidden="true" focusable="false"><rect x="4" y="5" width="24" height="22" rx="5" />
          <path d="M16 5v22M4 16h24M10 5v22M22 5v22" /></svg>
        <span>ShuttleBook<small>Đặt sân cầu lông</small></span>
      </a>
      <CustomerNav />
    </header>
    <div className="customer-workspace">
      <main id="customer-content" tabIndex={-1}>
        {title && <section className="customer-page-heading">{eyebrow && <p className="eyebrow">{eyebrow}</p>}
          <h1>{title}</h1>{description && <p>{description}</p>}</section>}
        {children}
      </main>
      <footer className="customer-footer"><span>ShuttleBook · Chọn sân, chọn giờ, đặt lịch.</span><a href="/venues">Tìm sân</a></footer>
    </div>
  </div>;
}
