type Name = 'brand' | 'overview' | 'approvals' | 'notifications' | 'logout' | 'menu' | 'close' | 'arrow' | 'refresh';

const paths: Record<Name, string> = {
  brand: 'M5 3h14v4H5zM5 11h6v10H5zM15 11h4v10h-4z',
  overview: 'M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z',
  approvals: 'M9 3H5a2 2 0 0 0-2 2v15h16V5a2 2 0 0 0-2-2h-4M9 2h6v4H9zM7 11l2 2 4-4M7 17h8',
  notifications: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4',
  logout: 'M9 3H4v18h5M13 8l5 4-5 4M8 12h13',
  menu: 'M4 6h16M4 12h16M4 18h16',
  close: 'M6 6l12 12M18 6 6 18',
  arrow: 'M5 12h14M13 6l6 6-6 6',
  refresh: 'M20 7v5h-5M4 17v-5h5M6 6a8 8 0 0 1 14 6M18 18A8 8 0 0 1 4 12',
};

export function AdminIcon({ name, className = '' }: { name: Name; className?: string }) {
  return <svg className={`admin-icon ${className}`} width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
    <path d={paths[name]} />
  </svg>;
}
