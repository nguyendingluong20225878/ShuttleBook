import { ReactNode, useEffect, useRef, useState } from 'react';
import { AdminIcon } from '../components/AdminIcon';
import { AdminPage, adminPages } from '../features/workspace/navigation';

export function AdminShell({ page, busy, onLogout, children }: {
  page: AdminPage; busy: boolean; onLogout: () => void; children: ReactNode;
}) {
  const [menuOpen, setMenuOpen] = useState(false);
  const menuButton = useRef<HTMLButtonElement>(null);
  const active = adminPages.find(item => item.id === page)!;
  useEffect(() => { setMenuOpen(false); }, [page]);
  useEffect(() => {
    if (!menuOpen) return;
    const close = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setMenuOpen(false); menuButton.current?.focus(); }
    };
    window.addEventListener('keydown', close);
    return () => window.removeEventListener('keydown', close);
  }, [menuOpen]);

  return <div className="admin-workspace">
    <a className="admin-skip" href="#admin-main" onClick={event => {
      event.preventDefault(); document.getElementById('admin-main')?.focus();
    }}>Đến nội dung chính</a>
    <header className="admin-mobile-header">
      <span className="admin-brand"><AdminIcon name="brand" />ShuttleBook <small>Quản trị</small></span>
      <div className="admin-mobile-actions"><button type="button" className="admin-button-secondary" onClick={onLogout} disabled={busy}>{busy ? 'Đang đăng xuất…' : 'Đăng xuất'}</button>
      <button type="button" className="admin-icon-button" ref={menuButton} aria-label={menuOpen ? 'Đóng menu quản trị' : 'Mở menu quản trị'}
        aria-controls="admin-navigation" aria-expanded={menuOpen} onClick={() => setMenuOpen(open => !open)}>
        <AdminIcon name={menuOpen ? 'close' : 'menu'} />
      </button></div>
    </header>
    <aside className={`admin-sidebar ${menuOpen ? 'is-open' : ''}`}>
      <a className="admin-brand admin-sidebar-brand" href="#overview"><AdminIcon name="brand" />ShuttleBook <small>Quản trị</small></a>
      <p className="admin-nav-label">VẬN HÀNH NỀN TẢNG</p>
      <nav id="admin-navigation" aria-label="Điều hướng quản trị">
        {adminPages.map(item => <a key={item.id} href={`#${item.id}`} aria-current={page === item.id ? 'page' : undefined}
          onClick={() => setMenuOpen(false)}><AdminIcon name={item.id} /><span>{item.title}</span></a>)}
      </nav>
      <div className="admin-sidebar-footer">
        <div className="admin-account"><span className="admin-avatar" aria-hidden="true">AD</span><div><strong>Quản trị viên</strong><small>Cổng quản trị nền tảng</small></div></div>
        <button type="button" className="admin-button-secondary" onClick={onLogout} disabled={busy}>
          <AdminIcon name="logout" />{busy ? 'Đang đăng xuất…' : 'Đăng xuất'}
        </button>
      </div>
    </aside>
    <div className="admin-workspace-content">
      <header className="admin-page-heading"><p className="admin-eyebrow">SHUTTLEBOOK / QUẢN TRỊ</p><h1>{active.title}</h1><p>{active.description}</p></header>
      <main id="admin-main" tabIndex={-1}>{children}</main>
      <footer>ShuttleBook · Cổng quản trị</footer>
    </div>
  </div>;
}
