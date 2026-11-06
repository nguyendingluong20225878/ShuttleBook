import { ReactNode, useEffect, useRef, useState } from 'react';
import { AdminIcon } from '../components/AdminIcon';
import { AdminPage, adminPages } from '../features/workspace/navigation';
import { ConfirmLogoutDialog } from '@shuttlebook/ui/browser-session';

export function AdminShell({ page, busy, onLogout, children, unreadCount }: {
  page: AdminPage; busy: boolean; onLogout: () => void; children: ReactNode; unreadCount?: number | null;
}) {
  const [menuOpen, setMenuOpen] = useState(false);
  const [confirmingLogout, setConfirmingLogout] = useState(false);
  const logoutTrigger = useRef<HTMLButtonElement | null>(null);
  const menuButton = useRef<HTMLButtonElement>(null);
  const more = useRef<HTMLDetailsElement>(null);
  const moreTrigger = useRef<HTMLElement>(null);
  const active = adminPages.find(item => item.id === page)!;
  const primaryPages = adminPages.filter(item => item.id === 'overview' || item.id === 'approvals');
  const secondaryPages = adminPages.filter(item => item.id !== 'overview' && item.id !== 'approvals');
  useEffect(() => { setMenuOpen(false); }, [page]);
  useEffect(() => {
    if (!menuOpen) return;
    const close = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setMenuOpen(false); menuButton.current?.focus(); }
    };
    window.addEventListener('keydown', close);
    return () => window.removeEventListener('keydown', close);
  }, [menuOpen]);
  useEffect(() => {
    const close = (event: PointerEvent) => {
      if (more.current?.open && !more.current.contains(event.target as Node)) more.current.open = false;
    };
    document.addEventListener('pointerdown', close);
    return () => document.removeEventListener('pointerdown', close);
  }, []);

  const navLink = (item: typeof adminPages[number]) => <a key={item.id} href={`#${item.id}`} aria-current={page === item.id ? 'page' : undefined}
    onClick={() => { setMenuOpen(false); if (more.current) more.current.open = false; }}><AdminIcon name={item.id} /><span>{item.title}</span>
    {item.id === 'notifications' && typeof unreadCount === 'number' && unreadCount > 0 && <span className="admin-notice-count" aria-label={`${unreadCount} thông báo chưa đọc`}>{unreadCount > 99 ? '99+' : unreadCount}</span>}</a>;

  return <div className="admin-workspace">
    <a className="admin-skip" href="#admin-main" onClick={event => {
      event.preventDefault(); document.getElementById('admin-main')?.focus();
    }}>Đến nội dung chính</a>
    <header className="admin-mobile-header">
      <span className="admin-brand"><AdminIcon name="brand" />ShuttleBook <small>Quản trị</small></span>
      <div className="admin-mobile-actions"><button type="button" className="admin-button-secondary" onClick={event => { logoutTrigger.current = event.currentTarget; setConfirmingLogout(true); }} disabled={busy}>{busy ? 'Đang đăng xuất…' : 'Đăng xuất'}</button>
      <button type="button" className="admin-icon-button" ref={menuButton} aria-label={menuOpen ? 'Đóng menu quản trị' : 'Mở menu quản trị'}
        aria-controls="admin-navigation" aria-expanded={menuOpen} onClick={() => setMenuOpen(open => !open)}>
        <AdminIcon name={menuOpen ? 'close' : 'menu'} />
      </button></div>
    </header>
    <aside className={`admin-sidebar ${menuOpen ? 'is-open' : ''}`}>
      <a className="admin-brand admin-sidebar-brand" href="#overview"><AdminIcon name="brand" />ShuttleBook <small>Quản trị</small></a>
      <p className="admin-nav-label">VẬN HÀNH NỀN TẢNG</p>
      <nav id="admin-navigation" aria-label="Điều hướng quản trị">
        {primaryPages.map(navLink)}
        <div className="admin-desktop-extra">{secondaryPages.map(navLink)}</div>
        <details ref={more} className={`admin-nav-more ${secondaryPages.some(item => item.id === page) ? 'is-current' : ''}`}
          onKeyDown={event => { if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); more.current!.open = false; moreTrigger.current?.focus(); } }}>
          <summary ref={moreTrigger}>Thêm{typeof unreadCount === 'number' && unreadCount > 0 && <span className="admin-notice-count" aria-label={`${unreadCount} thông báo chưa đọc`}>{unreadCount > 99 ? '99+' : unreadCount}</span>}</summary>
          <div className="admin-nav-more-menu">{secondaryPages.map(navLink)}</div>
        </details>
      </nav>
      <div className="admin-sidebar-footer">
        <div className="admin-account"><span className="admin-avatar" aria-hidden="true">AD</span><div><strong>Quản trị viên</strong><small>Cổng quản trị nền tảng</small></div></div>
        <button type="button" className="admin-button-secondary" onClick={event => { logoutTrigger.current = event.currentTarget; setConfirmingLogout(true); }} disabled={busy}>
          <AdminIcon name="logout" />{busy ? 'Đang đăng xuất…' : 'Đăng xuất'}
        </button>
      </div>
    </aside>
    <div className="admin-workspace-content">
      <header className="admin-page-heading"><p className="admin-eyebrow">SHUTTLEBOOK / QUẢN TRỊ</p><h1>{active.title}</h1><p>{active.description}</p></header>
      <main id="admin-main" tabIndex={-1}>{children}</main>
      <footer>ShuttleBook · Cổng quản trị</footer>
    </div>
    {confirmingLogout && <ConfirmLogoutDialog onCancel={() => { setConfirmingLogout(false); requestAnimationFrame(() => logoutTrigger.current?.focus()); }}
      onConfirm={() => { setConfirmingLogout(false); onLogout(); }} />}
  </div>;
}
