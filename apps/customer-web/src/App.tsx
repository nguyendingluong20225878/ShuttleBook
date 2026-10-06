import { CustomerIdentity } from './features/auth/CustomerIdentity';
import { VenueDetailPage } from './features/venues/VenueDetailPage';
import { VenueSearchPage } from './features/venues/VenueSearchPage';

export function App() {
  const path = window.location.pathname;
  if (path === '/venues' || path === '/venues/') return <VenueSearchPage />;
  const detail = /^\/venues\/([^/]+)\/?$/.exec(path);
  if (detail) return <VenueDetailPage venueId={decodeURIComponent(detail[1])} />;
  return <CustomerIdentity />;
}
