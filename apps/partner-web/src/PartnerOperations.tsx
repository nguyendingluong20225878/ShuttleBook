import { useEffect, useRef, useState } from 'react';
import { statusLabel } from './features/workspace/navigation';
import { PartnerRequest } from './features/bookings/types';
import { PartnerScheduleEditor } from './features/schedule/PartnerScheduleEditor';
import { PartnerCalendar } from './features/schedule/PartnerCalendar';

export type ScheduleCourt = { id: string; name: string; status: string; version: number };
export type ScheduleVenue = { id: string; name: string; status: string; timezone: string; courts: ScheduleCourt[] };
export type ScheduleTab = 'calendar' | 'pricing' | 'maintenance';
export const venueToday = (timezone = 'Asia/Ho_Chi_Minh') => new Intl.DateTimeFormat('en-CA', {
  timeZone: timezone, year: 'numeric', month: '2-digit', day: '2-digit',
}).format(new Date());
const courtOrder = (a: ScheduleCourt, b: ScheduleCourt) => a.name.localeCompare(b.name, 'vi', { numeric: true });
function locationState(venues: ScheduleVenue[], active: boolean) {
  const params = new URLSearchParams(window.location.hash.split('?')[1] ?? '');
  const venue = venues.find(v => v.id === params.get('venueId')) ?? venues[0];
  const rawTab = params.get('tab');
  const tab: ScheduleTab = rawTab === 'pricing' || rawTab === 'maintenance' || rawTab === 'calendar'
    ? rawTab : active && venue?.status === 'PUBLISHED' ? 'calendar' : 'pricing';
  const courtId = venue?.courts.some(c => c.id === params.get('courtId')) ? params.get('courtId')! :
    tab === 'calendar' ? '' : [...(venue?.courts ?? [])].sort(courtOrder)[0]?.id ?? '';
  const date = params.get('date');
  const validDate = date && /^\d{4}-\d{2}-\d{2}$/.test(date) && !Number.isNaN(Date.parse(`${date}T00:00:00Z`))
    && new Date(`${date}T00:00:00Z`).toISOString().slice(0, 10) === date;
  return { venueId: venue?.id ?? '', courtId, tab, date: validDate ? date! : venueToday(venue?.timezone) };
}

export function PartnerOperations({ venues, request, active, canEditDraft, refresh, openBooking, onDirtyChange, onBusyChange }: {
  venues: ScheduleVenue[]; request: PartnerRequest; active: boolean; canEditDraft: boolean; refresh: () => Promise<void>;
  openBooking: (id: string) => void; onDirtyChange: (dirty: boolean) => void; onBusyChange: (busy: boolean) => void;
}) {
  const [selection, setSelection] = useState(() => locationState(venues, active));
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const acceptedHash = useRef(window.location.hash);
  const dirtyRef = useRef(dirty); dirtyRef.current = dirty;
  const busyRef = useRef(busy); busyRef.current = busy;
  const [revision, setRevision] = useState(0);
  const venue = venues.find(v => v.id === selection.venueId);
  const courts = [...(venue?.courts ?? [])].sort(courtOrder);
  const court = courts.find(c => c.id === selection.courtId);
  const venuesRef = useRef(venues); venuesRef.current = venues;
  const lastCourt = useRef(new Map<string, string>());
  if (court) lastCourt.current.set(venue!.id, court.id);
  useEffect(() => { onDirtyChange(dirty); }, [dirty, onDirtyChange]);
  useEffect(() => { onBusyChange(busy); }, [busy, onBusyChange]);
  useEffect(() => () => { onDirtyChange(false); onBusyChange(false); }, []);
  useEffect(() => {
    const changed = () => {
      if (!window.location.hash.startsWith('#/schedule')) return;
      if (window.location.hash === acceptedHash.current) return;
      if (window.location.hash !== acceptedHash.current && (busyRef.current || dirtyRef.current &&
        !window.confirm('Bạn có thay đổi chưa lưu. Bỏ thay đổi để chuyển mục?'))) {
        window.history.replaceState(null, '', acceptedHash.current); return;
      }
      acceptedHash.current = window.location.hash;
      setDirty(false); setSelection(locationState(venuesRef.current, active));
    };
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (dirtyRef.current || busyRef.current) { event.preventDefault(); event.returnValue = ''; }
    };
    window.addEventListener('hashchange', changed); window.addEventListener('beforeunload', beforeUnload);
    return () => { window.removeEventListener('hashchange', changed); window.removeEventListener('beforeunload', beforeUnload); };
  }, [active]);
  useEffect(() => {
    if (venue && (selection.tab === 'calendar' && !selection.courtId || court)) return;
    if (busy) return;
    if (dirty && !window.confirm('Sân đã thay đổi trong hệ thống. Bỏ bản đang sửa để tải lại?')) return;
    setDirty(false); setSelection(locationState(venues, active));
  }, [venues, active, venue, court, selection.tab, selection.courtId, dirty, busy]);
  function choose(patch: Partial<typeof selection>) {
    if (busy || dirty && !window.confirm('Bạn có thay đổi chưa lưu. Bỏ thay đổi để chuyển mục?')) return;
    const next = { ...selection, ...patch };
    const selectedVenue = venues.find(v => v.id === next.venueId);
    if ('venueId' in patch) {
      next.courtId = next.tab === 'calendar' ? '' : [...(selectedVenue?.courts ?? [])].sort(courtOrder)[0]?.id ?? '';
      next.date = venueToday(selectedVenue?.timezone);
    }
    if (next.tab !== 'calendar' && !next.courtId) {
      const remembered = lastCourt.current.get(next.venueId);
      next.courtId = selectedVenue?.courts.some(c => c.id === remembered) ? remembered! :
        [...(selectedVenue?.courts ?? [])].sort(courtOrder)[0]?.id ?? '';
    }
    setDirty(false); setSelection(next);
    const params = new URLSearchParams({ tab: next.tab, venueId: next.venueId, date: next.date });
    if (next.courtId) params.set('courtId', next.courtId);
    acceptedHash.current = `#/schedule?${params}`;
    window.location.hash = acceptedHash.current;
  }
  return <section className="operations-page" aria-label="Vận hành sân">
    <div className="schedule-editor-heading"><h3>Vận hành sân</h3><a className="button secondary" href={`#/venues?${new URLSearchParams({ venueId: selection.venueId })}`}>
      Quay lại cơ sở & sân</a></div><p>Xem lịch thực tế hoặc thiết lập giờ và giá theo từng sân. QR nhận tiền dùng chung theo cơ sở.</p>
    <div className="schedule-scope">
      <label>Cơ sở <select value={venue?.id ?? ''} disabled={busy} onChange={event => choose({ venueId: event.target.value })}>
        {!venues.length && <option value="">Chưa có cơ sở</option>}
        {venues.map(v => <option key={v.id} value={v.id}>{v.name} · {statusLabel(v.status)}</option>)}
      </select></label>
      <label>Sân <select value={court?.id ?? ''} disabled={busy || !venue} onChange={event => choose({ courtId: event.target.value })}>
        {selection.tab === 'calendar' && <option value="">Tất cả sân</option>}
        {!courts.length && selection.tab !== 'calendar' && <option value="">Chưa có sân</option>}
        {courts.map(c => <option key={c.id} value={c.id}>{c.name} · {statusLabel(c.status)}</option>)}
      </select></label>
    </div>
    <div className="schedule-tabs" role="tablist" aria-label="Lịch và cấu hình sân">{[
      ['calendar', 'Lịch sân'], ['pricing', 'Giờ & giá'], ['maintenance', 'Bảo trì'],
    ].map(([id, label], index) => <button key={id} type="button" role="tab" id={`schedule-tab-${id}`}
      aria-controls={`schedule-panel-${id}`} aria-selected={selection.tab === id} tabIndex={selection.tab === id ? 0 : -1}
      disabled={busy} onClick={() => choose({ tab: id as ScheduleTab })} onKeyDown={event => {
        if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault(); const ids: ScheduleTab[] = ['calendar', 'pricing', 'maintenance'];
        const next = event.key === 'Home' ? 0 : event.key === 'End' ? 2 : (index + (event.key === 'ArrowRight' ? 1 : 2)) % 3;
        choose({ tab: ids[next] }); document.getElementById(`schedule-tab-${ids[next]}`)?.focus();
      }}>{label}</button>)}</div>
    <div id={`schedule-panel-${selection.tab}`} role="tabpanel" aria-labelledby={`schedule-tab-${selection.tab}`}>
      {selection.tab === 'calendar' ? active && venue ? <PartnerCalendar key={venue.id} venue={venue} courtId={court?.id ?? ''}
        date={selection.date} chooseDate={date => choose({ date })} request={request} openBooking={openBooking} revision={revision} />
        : <div className="panel"><p>Hồ sơ chưa hoạt động. Thiết lập giờ và giá để hoàn thiện cơ sở trước khi nhận đặt sân.</p>
          <button type="button" onClick={() => choose({ tab: 'pricing' })}>Thiết lập giờ & giá</button></div>
        : venue && court ? <PartnerScheduleEditor key={`${venue.id}/${court.id}/${court.status}/${selection.tab}`}
          venue={venue} court={court} request={request} tab={selection.tab} active={active} canEditDraft={canEditDraft}
          onDirtyChange={setDirty} onBusyChange={setBusy} onSaved={() => setRevision(value => value + 1)} refresh={refresh} />
        : <div className="panel"><p>Chưa có sân trong cơ sở này. Thêm sân tại Cơ sở & sân rồi quay lại thiết lập.</p><a href="#/venues">Đến Cơ sở & sân</a></div>}
    </div>
  </section>;
}
