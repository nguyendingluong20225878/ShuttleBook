import { FormEvent, useEffect, useRef, useState } from 'react';
import { statusLabel } from './features/workspace/navigation';

type ApiRequest = <T>(path: string, method?: string, body?: object, version?: number) => Promise<T>;
type CourtChoice = { id: string; name: string; status: string };
type VenueChoice = { id: string; name: string; courts: CourtChoice[] };
type Hour = { dayOfWeek: number; opensAt: string; closesAt: string };
type Price = { dayOfWeek: number; startsAt: string; endsAt: string; pricePerSlot: number };
type Rule = Price & { startsOn: string; endsOn: string; priority: number };
type Operations = { id: string; name: string; status: string; version: number; timezone: string;
  bookingBlockMinutes: number; minimumBookingMinutes: number; holdMinutes: number;
  hours: Hour[]; basePrices: Price[]; rules: Rule[] };
type Maintenance = { id: string; reason: string; status: string; startsAt: string; endsAt: string };
type Preview = { totalPrice: number; slots: Array<{
  startsAt: string; endsAt: string; pricePerSlot: number; priority: number }> };
const days = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];
const today = () => new Date().toISOString().slice(0, 10);
const hhmm = (value: string) => value.slice(0, 5);

export function PartnerOperations({ venues, request }: { venues: VenueChoice[]; request: ApiRequest }) {
  const choices = venues.flatMap(venue => venue.courts.map(court =>
    ({ ...court, label: `${venue.name} / ${court.name}` })));
  const [courtId, setCourtId] = useState(choices[0]?.id ?? '');
  const selectedCourtId = useRef(courtId);
  selectedCourtId.current = courtId;
  const [operations, setOperations] = useState<Operations | null>(null);
  const [maintenance, setMaintenance] = useState<Maintenance[]>([]);
  const [hours, setHours] = useState<Hour[]>([]);
  const [prices, setPrices] = useState<Price[]>([]);
  const [rules, setRules] = useState<Rule[]>([]);
  const [bookingBlockMinutes, setBookingBlockMinutes] = useState(30);
  const [minimumBookingMinutes, setMinimumBookingMinutes] = useState(30);
  const [holdMinutes, setHoldMinutes] = useState(20);
  const [date, setDate] = useState(today());
  const [startsAt, setStartsAt] = useState('08:00');
  const [endsAt, setEndsAt] = useState('10:00');
  const [reason, setReason] = useState('');
  const [preview, setPreview] = useState<Preview | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');

  async function load(id: string) {
    const root = `/operator/courts/${id}`;
    const [config, blocks] = await Promise.all([
      request<Operations>(`${root}/operations`), request<Maintenance[]>(`${root}/maintenance`)
    ]);
    if (selectedCourtId.current !== id) return;
    setOperations(config);
    setHours(config.hours.map(row => ({ ...row, opensAt: hhmm(row.opensAt), closesAt: hhmm(row.closesAt) })));
    setPrices(config.basePrices.map(row => ({ ...row, startsAt: hhmm(row.startsAt), endsAt: hhmm(row.endsAt) })));
    setRules(config.rules.map(row => ({ ...row, startsAt: hhmm(row.startsAt), endsAt: hhmm(row.endsAt) })));
    setBookingBlockMinutes(config.bookingBlockMinutes);
    setMinimumBookingMinutes(config.minimumBookingMinutes);
    setHoldMinutes(config.holdMinutes);
    setMaintenance(blocks);
  }
  useEffect(() => {
    if (!courtId) return;
    let current = true;
    setLoading(true); setOperations(null); setMaintenance([]); setMessage(''); setPreview(null);
    void load(courtId).catch(error => {
      if (current) setMessage(error instanceof Error ? error.message : 'Không tải được cấu hình sân.');
    }).finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [courtId]);
  async function run(work: () => Promise<void>) {
    setBusy(true); setMessage('');
    try { await work(); setMessage('Đã lưu.'); }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Có lỗi xảy ra.'); }
    finally { setBusy(false); }
  }
  function submit(event: FormEvent, work: () => Promise<void>) { event.preventDefault(); void run(work); }
  const root = `/operator/courts/${courtId}`;

  return <section className="operations-page" aria-label="Vận hành sân">
    <h3>Vận hành sân</h3>
    <p>Giờ và giá cơ bản áp dụng theo tuần. Quy tắc theo ngày có độ ưu tiên cao hơn. Thay đổi QR ở mục Thanh toán & QR để gửi Admin duyệt.</p>
    <label>Sân vận hành <select value={courtId} disabled={busy} onChange={event => setCourtId(event.target.value)}>
      {choices.map(choice => <option key={choice.id} value={choice.id}>{choice.label}</option>)}
    </select></label>
    {message && <p className={`feedback ${message === 'Đã lưu.' ? 'success' : 'error'}`} role="status">{message}</p>}
    {busy && <p className="feedback" role="status">Đang xử lý…</p>}
    {!loading && !operations && courtId && <button type="button" onClick={() => void run(() => load(courtId))}>Thử tải lại cấu hình</button>}
    {loading && <p>Đang tải cấu hình sân…</p>}
    {!loading && operations && <>
      <p>{operations.name} · {statusLabel(operations.status)} · múi giờ {operations.timezone}</p>
      <nav className="section-links" aria-label="Các phần cấu hình sân">{[
        ['policy', 'Quy định đặt'], ['weekly', 'Lịch tuần'], ['pricing', 'Giá theo ngày'], ['preview', 'Xem thử giá'], ['maintenance', 'Bảo trì'],
      ].map(([id, label]) => <a key={id} href="#/schedule" onClick={event => {
        event.preventDefault(); const section = document.getElementById(`operator-${id}`);
        section?.scrollIntoView({ block: 'start' }); section?.focus();
      }}>{label}</a>)}</nav>
      <fieldset disabled={busy}>
      <form id="operator-policy" tabIndex={-1} onSubmit={event => submit(event, async () => {
        await request(`${root}/booking-policy`, 'PUT',
          { bookingBlockMinutes, minimumBookingMinutes, holdMinutes }, operations.version);
        await load(courtId);
      })}>
        <h4>Quy định đặt sân</h4>
        <p>Mỗi block gồm các ca 30 phút liên tiếp. Giữ chỗ chỉ có hiệu lực trước khi khách báo đã chuyển khoản.</p>
        <label>Block hiển thị <select value={bookingBlockMinutes} onChange={event => {
          const block = Number(event.target.value);
          setBookingBlockMinutes(block); setMinimumBookingMinutes(Math.max(block, minimumBookingMinutes));
        }}><option value={30}>30 phút</option><option value={60}>60 phút</option>
          <option value={90}>90 phút</option></select></label>
        <label>Thời lượng đặt tối thiểu <input type="number" min={bookingBlockMinutes}
          max={480} step={bookingBlockMinutes} required value={minimumBookingMinutes}
          onChange={event => setMinimumBookingMinutes(Number(event.target.value))} /> phút</label>
        <label>Giữ chỗ trước khi báo chuyển khoản <input type="number" min={5} max={60} required
          value={holdMinutes} onChange={event => setHoldMinutes(Number(event.target.value))} /> phút</label>
        <button disabled={busy}>Lưu quy định</button>
      </form>
      <form id="operator-weekly" tabIndex={-1} onSubmit={event => submit(event, async () => {
        await request(`${root}/schedule`, 'PUT', { hours, prices }, operations.version);
        await load(courtId);
      })}>
        <h4>Giờ mở cửa hàng tuần</h4>
        {hours.map((row, index) => <div key={`hour-${index}`}>
          <label>Ngày <select value={row.dayOfWeek} onChange={event => setHours(hours.map((item, n) =>
            n === index ? { ...item, dayOfWeek: Number(event.target.value) } : item))}>
            {days.map((day, n) => <option key={day} value={n}>{day}</option>)}</select></label>
          <label>Mở <input type="time" step="1800" required value={row.opensAt} onChange={event => setHours(hours.map((item, n) =>
            n === index ? { ...item, opensAt: event.target.value } : item))} /></label>
          <label>Đóng <input type="time" step="1800" required value={row.closesAt} onChange={event => setHours(hours.map((item, n) =>
            n === index ? { ...item, closesAt: event.target.value } : item))} /></label>
          <button type="button" onClick={() => setHours(hours.filter((_, n) => n !== index))}>Xóa ngày</button>
        </div>)}
        <button type="button" onClick={() => setHours([...hours,
          { dayOfWeek: 1, opensAt: '08:00', closesAt: '10:00' }])}>Thêm ngày mở</button>
        <h4>Giá cơ bản theo ca 30 phút</h4>
        {prices.map((row, index) => <div key={`price-${index}`}>
          <label>Ngày <select value={row.dayOfWeek} onChange={event => setPrices(prices.map((item, n) =>
            n === index ? { ...item, dayOfWeek: Number(event.target.value) } : item))}>
            {days.map((day, n) => <option key={day} value={n}>{day}</option>)}</select></label>
          <label>Từ <input type="time" step="1800" required value={row.startsAt} onChange={event => setPrices(prices.map((item, n) =>
            n === index ? { ...item, startsAt: event.target.value } : item))} /></label>
          <label>Đến <input type="time" step="1800" required value={row.endsAt} onChange={event => setPrices(prices.map((item, n) =>
            n === index ? { ...item, endsAt: event.target.value } : item))} /></label>
          <label>Giá/30 phút <input type="number" min="1" required value={row.pricePerSlot} onChange={event => setPrices(prices.map((item, n) =>
            n === index ? { ...item, pricePerSlot: Number(event.target.value) } : item))} /></label>
          <span>≈ {(row.pricePerSlot * 2).toLocaleString('vi-VN')} VND/giờ</span>
          <button type="button" onClick={() => setPrices(prices.filter((_, n) => n !== index))}>Xóa giá</button>
        </div>)}
        <button type="button" onClick={() => setPrices([...prices,
          { dayOfWeek: 1, startsAt: '08:00', endsAt: '10:00', pricePerSlot: 100000 }])}>Thêm khung giá</button>
        <button disabled={busy}>Lưu lịch tuần</button>
      </form>

      <form id="operator-pricing" tabIndex={-1} onSubmit={event => submit(event, async () => {
        await request(`${root}/pricing-rules`, 'PUT', { rules }, operations.version);
        await load(courtId);
      })}>
        <h4>Giá ưu tiên theo khoảng ngày</h4>
        {rules.map((row, index) => <div key={`rule-${index}`}>
          <label>Hiệu lực từ <input type="date" required value={row.startsOn} onChange={event => setRules(rules.map((item, n) =>
            n === index ? { ...item, startsOn: event.target.value } : item))} /></label>
          <label>Đến ngày <input type="date" required value={row.endsOn} onChange={event => setRules(rules.map((item, n) =>
            n === index ? { ...item, endsOn: event.target.value } : item))} /></label>
          <label>Thứ <select value={row.dayOfWeek} onChange={event => setRules(rules.map((item, n) =>
            n === index ? { ...item, dayOfWeek: Number(event.target.value) } : item))}>
            {days.map((day, n) => <option key={day} value={n}>{day}</option>)}</select></label>
          <label>Từ <input type="time" step="1800" required value={row.startsAt} onChange={event => setRules(rules.map((item, n) =>
            n === index ? { ...item, startsAt: event.target.value } : item))} /></label>
          <label>Đến <input type="time" step="1800" required value={row.endsAt} onChange={event => setRules(rules.map((item, n) =>
            n === index ? { ...item, endsAt: event.target.value } : item))} /></label>
          <label>Giá/30 phút <input type="number" min="1" required value={row.pricePerSlot} onChange={event => setRules(rules.map((item, n) =>
            n === index ? { ...item, pricePerSlot: Number(event.target.value) } : item))} /></label>
          <span>≈ {(row.pricePerSlot * 2).toLocaleString('vi-VN')} VND/giờ</span>
          <label>Ưu tiên <input type="number" min="1" max="1000" required value={row.priority} onChange={event => setRules(rules.map((item, n) =>
            n === index ? { ...item, priority: Number(event.target.value) } : item))} /></label>
          <button type="button" onClick={() => setRules(rules.filter((_, n) => n !== index))}>Xóa quy tắc</button>
        </div>)}
        <button type="button" onClick={() => setRules([...rules, { startsOn: today(), endsOn: today(),
          dayOfWeek: 1, startsAt: '08:00', endsAt: '10:00', pricePerSlot: 120000, priority: 1 }])}>
          Thêm quy tắc giá</button>
        <p>Ngày lễ có thể đặt khoảng hiệu lực một ngày và chọn đúng thứ của ngày đó. Quy tắc ưu tiên cao hơn giá tuần.</p>
        <button disabled={busy}>Lưu giá theo ngày</button>
      </form>

      <form id="operator-preview" tabIndex={-1} onSubmit={event => submit(event, async () => {
        const result = await request<Preview>(`${root}/price-preview?date=${encodeURIComponent(date)}&startsAt=${encodeURIComponent(startsAt)}&endsAt=${encodeURIComponent(endsAt)}`);
        setPreview(result);
      })}>
        <h4>Xem thử giá</h4>
        <label>Ngày <input type="date" required value={date} onChange={event => setDate(event.target.value)} /></label>
        <label>Từ <input type="time" step="1800" required value={startsAt} onChange={event => setStartsAt(event.target.value)} /></label>
        <label>Đến <input type="time" step="1800" required value={endsAt} onChange={event => setEndsAt(event.target.value)} /></label>
        <button disabled={busy}>Xem giá</button>
        {preview && <p>Tổng giá: {preview.totalPrice.toLocaleString('vi-VN')} VND · {preview.slots.length} ca</p>}
      </form>

      <form id="operator-maintenance" tabIndex={-1} onSubmit={event => submit(event, async () => {
        await request(`${root}/maintenance`, 'POST', { date, startsAt, endsAt, reason });
        setReason(''); await load(courtId);
      })}>
        <h4>Bảo trì sân</h4>
        <p>Ngày và giờ dùng múi giờ của cơ sở. Bảo trì sẽ khóa ca tương ứng cho booking sau này.</p>
        <label>Ngày <input type="date" required value={date} onChange={event => setDate(event.target.value)} /></label>
        <label>Từ <input type="time" step="1800" required value={startsAt} onChange={event => setStartsAt(event.target.value)} /></label>
        <label>Đến <input type="time" step="1800" required value={endsAt} onChange={event => setEndsAt(event.target.value)} /></label>
        <label>Lý do <input required minLength={5} maxLength={500} value={reason} onChange={event => setReason(event.target.value)} /></label>
        <button disabled={busy}>Khóa ca bảo trì</button>
      </form>
      <h4>Ca bảo trì đang giữ</h4>
      {maintenance.length === 0 ? <p>Chưa có ca bảo trì.</p> : <ul className="maintenance-list">{maintenance.map(item =>
        <li key={item.id}>{new Date(item.startsAt).toLocaleString('vi-VN', { timeZone: operations.timezone })}
          {' – '}{new Date(item.endsAt).toLocaleString('vi-VN', { timeZone: operations.timezone })}
          {' · '}{item.reason}{' '}
          <button type="button" disabled={busy} onClick={() => void run(async () => {
            await request(`${root}/maintenance/${item.id}/cancel`, 'POST'); await load(courtId);
          })}>Hủy bảo trì</button>
        </li>)}</ul>}
      </fieldset>
    </>}
    {choices.length === 0 && <p>Chưa có sân đã duyệt để vận hành.</p>}
  </section>;
}
