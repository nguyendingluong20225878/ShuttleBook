import { CustomerIdentity } from './features/auth/CustomerIdentity';
import { VenueDetailPage } from './features/venues/VenueDetailPage';
import { VenueSearchPage } from './features/venues/VenueSearchPage';
import { useLayoutEffect, useState } from 'react';
import { BookingDetail, BookingReview, MyBookings } from './features/bookings/BookingPages';
import { navigate } from './routes/navigation';
import { CustomerNotificationsPage } from './features/notifications/CustomerNotifications';
import { SeriesReview } from './features/bookings/SeriesReview';

export function App() {
  const [url, setUrl] = useState(location.pathname + location.search);
  useLayoutEffect(() => {
    const restore = () => setUrl(location.pathname + location.search);
    const links = (event: MouseEvent) => {
      if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
      const link = (event.target as Element).closest<HTMLAnchorElement>('a[href]');
      if (!link || link.target || link.hasAttribute('download') || link.origin !== location.origin) return;
      event.preventDefault(); navigate(link.pathname + link.search + link.hash);
    };
    addEventListener('popstate', restore); document.addEventListener('click', links);
    return () => { removeEventListener('popstate', restore); document.removeEventListener('click', links); };
  }, []);
  const path = url.split('?')[0];
  if (path === '/booking-review') return <BookingReview key={url} />;
  if (path === '/series-review') return <SeriesReview key={url} />;
  if (path === '/me/bookings') return <MyBookings />;
  if (path === '/me/notifications') return <CustomerNotificationsPage />;
  const booking = /^\/bookings\/([^/]+)\/?$/.exec(path);
  if (booking) return <BookingDetail key={booking[1]} id={booking[1]} />;
  if (path === '/venues' || path === '/venues/') return <VenueSearchPage />;
  const detail = /^\/venues\/([^/]+)\/?$/.exec(path);
  if (detail) return <VenueDetailPage venueId={decodeURIComponent(detail[1])} />;
  return <CustomerIdentity />;
}
