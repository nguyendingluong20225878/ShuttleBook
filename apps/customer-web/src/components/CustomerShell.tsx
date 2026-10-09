import { useEffect, useRef, type ReactNode } from 'react';
import { CustomerNav, useCustomerSession } from '../features/auth/CustomerSession';

type Props = { children: ReactNode; title?: string; eyebrow?: string; description?: string; className?: string; layout?: 'workspace' | 'account' };

/** Shared public/customer chrome; authentication and routing remain in their existing providers. */
export function CustomerShell({ children, title, eyebrow, description, className = '', layout = 'workspace' }: Props) {
  const header = useRef<HTMLElement>(null);
  const { session } = useCustomerSession();
  const path = location.pathname;
  useEffect(() => {
    const nav = header.current?.querySelector('nav');
    nav?.setAttribute('aria-label', 'Điều hướng khách đặt sân');
    nav?.querySelectorAll<HTMLAnchorElement>('a[href]').forEach(link => {
      const active = link.pathname === '/venues' ? path.startsWith('/venues')
        : link.pathname === '/me/bookings' ? path === '/me/bookings' || path.startsWith('/bookings/') || path.startsWith('/booking-series/')
          : link.pathname === path;
      if (active) link.setAttribute('aria-current', 'page'); else link.removeAttribute('aria-current');
    });
  }, [path, session]);
  const pageLabel = path === '/venues' ? 'Tìm sân' : path.startsWith('/venues/') ? 'Lịch sân'
    : path === '/series-review' ? 'Đặt lịch cố định' : path === '/booking-review' ? 'Đặt vãng lai'
      : path === '/me/notifications' ? 'Thông báo' : path === '/me/bookings' || path.startsWith('/bookings/') ? 'Đơn của tôi' : 'Tài khoản';
  return <div className={`customer-shell ${layout === 'account' ? 'customer-account-shell' : ''} ${className}`}>
    <a className="skip-link" href="#customer-content" onClick={event => {
      event.preventDefault(); document.getElementById('customer-content')?.focus();
    }}>Chuyển đến nội dung</a>
    <header className="site-header" ref={header}>
      <a className="brand" href="/venues" aria-label="ShuttleBook — Tìm sân">
        <svg viewBox="0 0 32 32" aria-hidden="true" focusable="false"><rect x="4" y="5" width="24" height="22" rx="5" />
          <path d="M16 5v22M4 16h24M10 5v22M22 5v22" /></svg>
        <span>ShuttleBook<small>Khách đặt sân</small></span>
      </a>
      {layout !== 'account' && <p className="customer-nav-caption">KHÔNG GIAN ĐẶT SÂN</p>}
      <CustomerNav />
      {layout !== 'account' && <div className="customer-sidebar-account"><span className="customer-avatar" aria-hidden="true">KH</span>
        <div><strong>{session ? 'Khách đặt sân' : 'Chào mừng bạn'}</strong><small>{session ? 'Tài khoản khách hàng' : 'Tìm sân và chọn lịch trống'}</small></div></div>}
    </header>
    <div className="customer-workspace">
    {layout !== 'account' && <div className="customer-toolbar"><div><span className="customer-toolbar-caption">ShuttleBook / Khách đặt sân</span><strong>{pageLabel}</strong></div>
      <span className="customer-session-label"><span aria-hidden="true" />{session ? 'Đã đăng nhập' : 'Khám phá sân thể thao'}</span></div>}
    <main id="customer-content" tabIndex={-1}>
      {title && <section className="customer-page-heading">{eyebrow && <p className="eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>{description && <p>{description}</p>}</section>}
      {children}
    </main>
    <footer className="customer-footer"><span>ShuttleBook · Đặt sân theo lịch của bạn</span><a href="/venues">Tìm cơ sở &amp; lịch trống</a></footer>
    </div>
  </div>;
}
