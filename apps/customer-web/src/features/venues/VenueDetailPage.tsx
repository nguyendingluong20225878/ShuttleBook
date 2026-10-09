import { useEffect, useState } from 'react';
import { CustomerShell } from '../../components/CustomerShell';
import { getPublic } from './api';
import { ScheduleGrid } from './components/ScheduleGrid';
import { VenuePhoto } from './components/VenuePhoto';
import type { VenueDetail, VenueSchedule } from './types';

function todayInZone(zone: string) {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' })
    .formatToParts(new Date());
  const part = (name: string) => parts.find(item => item.type === name)?.value ?? '';
  return `${part('year')}-${part('month')}-${part('day')}`;
}

function locationDate(venue: VenueDetail) {
  const url = new URL(window.location.href);
  if (url.searchParams.has('courtId')) {
    url.searchParams.delete('courtId');
    window.history.replaceState(window.history.state, '', url);
  }
  const requestedDate = url.searchParams.get('date') ?? '';
  const today = todayInZone(venue.timezone);
  return /^\d{4}-\d{2}-\d{2}$/.test(requestedDate) && requestedDate >= today ? requestedDate : today;
}

function rememberDate(date: string) {
  const url = new URL(window.location.href);
  url.searchParams.set('date', date);
  url.searchParams.delete('courtId');
  window.history.pushState(null, '', url);
}

export function VenueDetailPage({ venueId }: { venueId: string }) {
  const [venue, setVenue] = useState<VenueDetail | null>(null);
  const [date, setDate] = useState('');
  const [schedule, setSchedule] = useState<VenueSchedule | null>(null);
  const [loading, setLoading] = useState(true);
  const [scheduleLoading, setScheduleLoading] = useState(false);
  const [error, setError] = useState('');
  const [scheduleError, setScheduleError] = useState('');
  const [retry, setRetry] = useState(0);
  const [bookingMode, setBookingMode] = useState<'casual' | 'fixed'>(() => new URLSearchParams(location.search).get('mode') === 'fixed' ? 'fixed' : 'casual');

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    void getPublic<VenueDetail>(`/venues/${encodeURIComponent(venueId)}`, controller.signal)
      .then(data => { setVenue(data); setDate(locationDate(data)); })
      .catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'Không tải được cơ sở.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [venueId]);

  useEffect(() => {
    if (!venue) return;
    const restore = () => {
      if (location.pathname !== `/venues/${venueId}` && location.pathname !== `/venues/${venueId}/`) return;
      setDate(locationDate(venue)); setBookingMode(new URLSearchParams(location.search).get('mode') === 'fixed' ? 'fixed' : 'casual'); setSchedule(null);
      setRetry(value => value + 1); };
    window.addEventListener('popstate', restore);
    return () => window.removeEventListener('popstate', restore);
  }, [venue, venueId]);

  useEffect(() => {
    if (!venue || !date) return;
    let current: AbortController | null = null;
    let disposed = false;
    const refresh = () => {
      current?.abort(); current = new AbortController();
      const signal = current.signal;
      setScheduleLoading(true); setScheduleError('');
      void getPublic<VenueSchedule>(`/venues/${encodeURIComponent(venueId)}/availability?date=${encodeURIComponent(date)}`,
        signal)
        .then(data => { if (!disposed && !signal.aborted) setSchedule(data); })
        .catch(reason => { if (!disposed && !signal.aborted)
          setScheduleError(reason instanceof Error ? reason.message : 'Không tải được lịch.'); })
        .finally(() => { if (!disposed && !signal.aborted) setScheduleLoading(false); });
    };
    const onFocus = () => { if (document.visibilityState === 'visible') refresh(); };
    refresh();
    const timer = window.setInterval(() => { if (document.visibilityState === 'visible') refresh(); }, 30_000);
    window.addEventListener('focus', onFocus);
    document.addEventListener('visibilitychange', onFocus);
    return () => { disposed = true; current?.abort(); window.clearInterval(timer);
      window.removeEventListener('focus', onFocus); document.removeEventListener('visibilitychange', onFocus); };
  }, [venue, date, venueId, retry]);

  return <CustomerShell>
    <p className="breadcrumbs"><a href="/venues">Danh sách cơ sở</a> / Chi tiết cơ sở</p>
    {loading && <p role="status">Đang tải cơ sở…</p>}
    {error && <p role="alert">{error} <a href="/venues">Quay lại danh sách</a></p>}
    {venue && <><section className="venue-hero">
      <VenuePhoto imageUrl={venue.imageUrl} name={venue.name} />
      <div><p className="eyebrow">Cơ sở ShuttleBook</p><h1>{venue.name}</h1>
        <p>{venue.address}</p><p>Liên hệ cơ sở: {venue.contact}</p>
        <p>Múi giờ lịch: {venue.timezone} · {venue.courts.length} sân đang hoạt động</p></div>
    </section><div className="booking-mode" role="group" aria-label="Hình thức đặt sân"><span>Hình thức đặt</span>
      <button type="button" aria-pressed={bookingMode === 'casual'} onClick={() => { setBookingMode('casual');
        const url = new URL(location.href); url.searchParams.set('mode', 'casual'); history.pushState(null, '', url); }}>Vãng lai</button>
      <button type="button" aria-pressed={bookingMode === 'fixed'} onClick={() => { setBookingMode('fixed');
        const url = new URL(location.href); url.searchParams.set('mode', 'fixed'); history.pushState(null, '', url); }}>Cố định hằng tuần</button>
      {bookingMode === 'fixed' && <p>Mỗi buổi tối thiểu 2 giờ, kỳ tối thiểu 1 tháng. Chọn khung giờ ở bảng rồi kiểm tra toàn kỳ.</p>}
    </div><section className="schedule-controls" aria-label="Chọn lịch">
      <label htmlFor="schedule-date">Ngày chơi</label><input id="schedule-date" type="date" value={date}
        min={todayInZone(venue.timezone)} onChange={event => { setDate(event.target.value);
          setSchedule(null); rememberDate(event.target.value); }} />
      <button type="button" onClick={() => setRetry(value => value + 1)}>Làm mới lịch</button>
      {scheduleLoading && <span role="status">Đang cập nhật lịch…</span>}
    </section>
    {scheduleError && <p role="alert">{scheduleError}</p>}
    {schedule && schedule.date === date && <ScheduleGrid schedule={schedule} bookingMode={bookingMode} />}
    </>}
  </CustomerShell>;
}
