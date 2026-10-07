import { ReactNode, useEffect, useRef, useState } from 'react';
import { PartnerIcon } from '../components/PartnerIcon';
import { PartnerPage, partnerPages } from '../features/workspace/navigation';

export function PartnerShell({ page, navigate, businessControl, unread, onLogout, logoutBusy, children }: {
  page: PartnerPage; navigate: (page: PartnerPage) => void; businessControl: ReactNode; unread: number;
  onLogout: () => void; logoutBusy: boolean; children: ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const toggle = useRef<HTMLButtonElement>(null);
  const title = useRef<HTMLHeadingElement>(null);
  const current = partnerPages.find(item => item.id === page)!;
  useEffect(() => { title.current?.focus(); }, [page]);
  useEffect(() => {
    if (!open) return;
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setOpen(false); toggle.current?.focus(); }
    };
    window.addEventListener('keydown', escape);
    return () => window.removeEventListener('keydown', escape);
  }, [open]);
  function go(next: PartnerPage) { setOpen(false); navigate(next); title.current?.focus(); }
  return <div className="partner-shell">
    <a className="skip-link" href="#partner-content" onClick={event => {
      event.preventDefault(); document.getElementById('partner-content')?.focus();
    }}>Đến nội dung chính</a>
    <header className="mobile-bar">
      <a className="brand" href="#/overview" onClick={() => go('overview')}><span className="brand-mark"><PartnerIcon name="courts" /></span>ShuttleBook</a>
      <button className="secondary menu-toggle" ref={toggle} type="button" aria-expanded={open}
        aria-controls="partner-navigation" onClick={() => setOpen(!open)}><PartnerIcon name="menu" />Menu</button>
    </header>
    <aside className={`partner-sidebar ${open ? 'is-open' : ''}`} id="partner-navigation">
      <a className="brand desktop-brand" href="#/overview" onClick={() => go('overview')}><span className="brand-mark"><PartnerIcon name="courts" /></span><span>ShuttleBook<small>Dành cho đối tác</small></span></a>
      <p className="nav-caption">KHÔNG GIAN QUẢN LÝ</p>
      <nav aria-label="Quản lý đối tác">{partnerPages.map(item => <a key={item.id} href={`#/${item.id}`}
        aria-label={item.label}
        aria-current={page === item.id ? 'page' : undefined} onClick={() => go(item.id)}>
        <PartnerIcon name={item.id} /><span>{item.label}</span>
        {item.id === 'notifications' && unread > 0 && <span className="notice-count" aria-label={`${unread} chưa đọc`}>{unread}</span>}
      </a>)}</nav>
      <div className="sidebar-footer"><span className="owner-avatar">CS</span><div><strong>Chủ sân</strong><small>Tài khoản đối tác</small></div>
        <button type="button" className="icon-button" aria-label="Đăng xuất" disabled={logoutBusy} onClick={onLogout}><PartnerIcon name="logout" /></button>
      </div>
    </aside>
    <div className="partner-workspace">
      <div className="workspace-toolbar"><span className="toolbar-caption">Quản lý cơ sở thể thao</span>{businessControl}</div>
      <main id="partner-content" tabIndex={-1} className="workspace-content">
        <header className="page-heading"><p className="eyebrow">SHUTTLEBOOK PARTNER</p><h1 ref={title} tabIndex={-1}>{current.label}</h1><p>{current.description}</p></header>
        {children}
      </main>
    </div>
  </div>;
}
