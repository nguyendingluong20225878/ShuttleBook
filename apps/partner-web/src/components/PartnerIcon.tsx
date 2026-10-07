type IconName = 'overview' | 'profile' | 'venues' | 'courts' | 'schedule' | 'bookings' | 'media' | 'payments' | 'notifications' | 'menu' | 'arrow' | 'check' | 'logout';
const paths: Record<IconName, string> = {
  overview: 'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
  profile: 'M5 21V3h14v18 M3 21h18 M9 7h1 M14 7h1 M9 11h1 M14 11h1 M10 21v-6h4v6',
  venues: 'M20 10c0 6-8 11-8 11S4 16 4 10a8 8 0 1 1 16 0z M15 10a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
  courts: 'M3 5h18v14H3z M12 5v14 M3 12h18 M7 5v14 M17 5v14',
  schedule: 'M4 5h16v16H4z M8 3v4 M16 3v4 M4 10h16 M8 14h2 M14 14h2 M8 17h2',
  bookings: 'M6 3h12v18H6z M9 7h6 M9 11h6 M9 15h3 M15 17l2 2 4-4',
  media: 'M3 3h18v18H3z M3 17l6-6 4 4 3-3 5 5 M16 7h.01',
  payments: 'M3 5h18v14H3z M3 10h18 M7 15h4',
  notifications: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9 M10 21h4',
  menu: 'M4 6h16 M4 12h16 M4 18h16',
  arrow: 'M5 12h14 M14 7l5 5-5 5',
  check: 'M5 12l4 4L19 6',
  logout: 'M9 4H4v16h5 M10 12h11 M17 8l4 4-4 4',
};
export function PartnerIcon({ name }: { name: IconName }) {
  return <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round"><path d={paths[name]} /></svg>;
}
