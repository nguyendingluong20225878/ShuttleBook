import { FormEvent, useEffect, useRef, useState } from 'react';
import { ScheduleCourt, ScheduleTab, ScheduleVenue, venueToday } from '../../PartnerOperations';
import { usePartnerToast } from '../../components/PartnerToast';
import { PartnerApiError, PartnerRequest, money } from '../bookings/types';
import { statusLabel } from '../workspace/navigation';

type Hour = { dayOfWeek: number; opensAt: string; closesAt: string };
type Price = { dayOfWeek: number; startsAt: string; endsAt: string; pricePerSlot: number };
type SpecialPrice = { startsOn: string; endsOn: string; startsAt: string; endsAt: string; pricePerSlot: number };
type Operations = { id: string; name: string; status: string; version: number; timezone: string;
  bookingBlockMinutes: number; minimumBookingMinutes: number; holdMinutes: number;
  hours: Hour[]; basePrices: Price[]; specialPrice?: SpecialPrice | null; legacySpecialPrices?: boolean };
type Maintenance = { id: string; reason: string; status: string; startsAt: string; endsAt: string };
type Preview = { totalPrice: number; slots: Array<{ startsAt: string; endsAt: string; pricePerSlot: number; priority: number }> };
const days = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];
const dayOrder = [1, 2, 3, 4, 5, 6, 0];
const shortTime = (value: string) => value.slice(0, 5);
const copyHours = (rows: Hour[]) => rows.map(row => ({ ...row, opensAt: shortTime(row.opensAt), closesAt: shortTime(row.closesAt) }));
const copyPrices = <T extends Price>(rows: T[]) => rows.map(row => ({ ...row, startsAt: shortTime(row.startsAt), endsAt: shortTime(row.endsAt) }));
const isPrivateFailure = (error: unknown) => error instanceof PartnerApiError && [401, 403, 404].includes(error.status);

export function PartnerScheduleEditor({ venue, court, request, tab, active, canEditDraft, refresh, onDirtyChange, onBusyChange, onSaved }: {
  venue: ScheduleVenue; court: ScheduleCourt; request: PartnerRequest; tab: ScheduleTab; active: boolean; canEditDraft: boolean;
  refresh: () => Promise<void>; onDirtyChange: (dirty: boolean) => void; onBusyChange: (busy: boolean) => void; onSaved: () => void;
}) {
  const draft = court.status !== 'ACTIVE';
  const editable = draft ? canEditDraft && (court.status === 'INACTIVE' || court.status === 'PENDING_APPROVAL' && venue.status === 'PUBLISHED') && ['DRAFT', 'PUBLISHED'].includes(venue.status)
    : active && venue.status === 'PUBLISHED';
  const root = `/operator/courts/${court.id}`;
  const configRoot = draft ? `/partner-onboarding/courts/${court.id}` : root;
  const configPath = `${configRoot}/${draft ? 'schedule' : 'operations'}`;
  const requestRef = useRef(request); requestRef.current = request;
  const { showToast, clearToast } = usePartnerToast();
  const [config, setConfig] = useState<Operations | null>(null);
  const [hours, setHours] = useState<Hour[]>([]);
  const [prices, setPrices] = useState<Price[]>([]);
  const [specialPrice, setSpecialPrice] = useState<SpecialPrice | null>(null);
  const [policy, setPolicy] = useState({ bookingBlockMinutes: 30, minimumBookingMinutes: 30, holdMinutes: 20 });
  const [dirtyParts, setDirtyParts] = useState<string[]>([]);
  const [weeklyErrors, setWeeklyErrors] = useState<Record<number, string>>({});
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [stale, setStale] = useState(false);
  const [denied, setDenied] = useState(false);
  const [maintenance, setMaintenance] = useState<Maintenance[]>([]);
  const [maintenanceError, setMaintenanceError] = useState('');
  const [maintenanceRevision, setMaintenanceRevision] = useState(0);
  const [statusChanged, setStatusChanged] = useState(false);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [previewInput, setPreviewInput] = useState({ date: venueToday(venue.timezone), startsAt: '08:00', endsAt: '10:00' });
  const [block, setBlock] = useState({ date: venueToday(venue.timezone), startsAt: '08:00', endsAt: '10:00', reason: '' });
  const generation = useRef(0);
  const mounted = useRef(true);
  useEffect(() => { onDirtyChange(dirtyParts.length > 0); }, [dirtyParts, onDirtyChange]);
  useEffect(() => { onBusyChange(busy); }, [busy, onBusyChange]);
  function mark(part: string) {
    if (part === 'weekly') setWeeklyErrors({});
    setDirtyParts(previous => previous.includes(part) ? previous : [...previous, part]);
  }
  function hydrate(next: Operations) {
    setConfig(next); setHours(copyHours(next.hours)); setPrices(copyPrices(next.basePrices));
    setSpecialPrice(next.specialPrice ?? null);
    if (!next.hours.length && !next.basePrices.length && editable) {
      setHours([{ dayOfWeek: 1, opensAt: '07:00', closesAt: '22:00' }]);
      setPrices([{ dayOfWeek: 1, startsAt: '07:00', endsAt: '22:00', pricePerSlot: 100000 }]);
    }
    setPolicy({ bookingBlockMinutes: next.bookingBlockMinutes, minimumBookingMinutes: next.minimumBookingMinutes, holdMinutes: next.holdMinutes });
    setDirtyParts([]); setWeeklyErrors({}); setStatusChanged(next.status !== court.status); setStale(false); setDenied(false); setError(''); setPreview(null);
  }
  function clearPrivate() {
    generation.current++; setConfig(null); setHours([]); setPrices([]); setSpecialPrice(null); setMaintenance([]); setPreview(null);
    setDirtyParts([]); setDenied(true);
  }
  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController(); const sequence = ++generation.current;
    setLoading(true);
    void requestRef.current<Operations>(configPath, 'GET', undefined, undefined, { signal: controller.signal }).then(next => {
      if (!controller.signal.aborted && sequence === generation.current) hydrate(next);
    }).catch(failure => {
      if (controller.signal.aborted || sequence !== generation.current) return;
      if (isPrivateFailure(failure)) clearPrivate();
      setError(failure instanceof Error ? failure.message : 'Không tải được cấu hình sân.');
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => { mounted.current = false; controller.abort(); generation.current++; };
  }, [configPath]);
  useEffect(() => {
    if (tab !== 'maintenance' || draft || !config || denied) return;
    const controller = new AbortController();
    void requestRef.current<Maintenance[]>(`${root}/maintenance`, 'GET', undefined, undefined, { signal: controller.signal })
      .then(rows => { if (!controller.signal.aborted) { setMaintenance(rows); setMaintenanceError(''); } })
      .catch(failure => { if (!controller.signal.aborted) {
        if (isPrivateFailure(failure)) clearPrivate();
        setMaintenanceError(failure instanceof Error ? failure.message : 'Không tải được bảo trì.');
      } });
    return () => controller.abort();
  }, [tab, root, draft, config?.version, denied, maintenanceRevision]);
  async function run(work: () => Promise<void>, message: string) {
    if (busy || stale || denied || !config) return;
    setBusy(true); setError(''); clearToast();
    try { await work(); if (mounted.current) showToast(message); }
    catch (failure) {
      if (!mounted.current) return;
      if (isPrivateFailure(failure)) clearPrivate();
      if (failure instanceof PartnerApiError && [409, 412].includes(failure.status) &&
        ['STATE_CONFLICT', 'PRECONDITION_FAILED'].includes(failure.code)) setStale(true);
      const message = failure instanceof Error ? failure.message : 'Có lỗi xảy ra.';
      setError(message); showToast(message, 'error');
    } finally { if (mounted.current) setBusy(false); }
  }
  async function save(part: string, path: string, body: object) {
    if (part === 'weekly') {
      const errors: Record<number, string> = {};
      const validTime = (time: string) => /^(?:[01]\d|2[0-3]):(?:00|30)$/.test(time);
      for (const hour of hours) {
        const day = hour.dayOfWeek;
        if (!validTime(hour.opensAt) || !validTime(hour.closesAt) || hour.opensAt >= hour.closesAt) {
          errors[day] = 'Giờ mở phải trước giờ đóng, theo ca 30 phút.'; continue;
        }
        let cursor = hour.opensAt;
        const ranges = prices.filter(row => row.dayOfWeek === day).sort((a, b) => a.startsAt.localeCompare(b.startsAt));
        for (const row of ranges) {
          if (!Number.isSafeInteger(row.pricePerSlot) || row.pricePerSlot <= 0) { errors[day] = 'Giá phải là số nguyên dương hợp lệ.'; break; }
          if (!validTime(row.startsAt) || !validTime(row.endsAt) || row.startsAt !== cursor || row.endsAt <= row.startsAt || row.endsAt > hour.closesAt) {
            errors[day] = `Khung giá phải nối tiếp từ ${cursor}, không chồng hoặc vượt giờ đóng ${hour.closesAt}.`; break;
          }
          cursor = row.endsAt;
        }
        if (!errors[day] && cursor !== hour.closesAt) errors[day] = `Còn thiếu giá từ ${cursor} đến ${hour.closesAt}.`;
      }
      setWeeklyErrors(errors);
      if (!hours.length) throw new Error('Cần mở ít nhất một ngày và thiết lập giá trước khi lưu.');
      if (prices.length > 70) throw new Error('Tối đa 70 khung giá cho một sân.');
      if (Object.keys(errors).length) throw new Error('Lịch và giá chưa phủ đủ giờ mở. Kiểm tra thông báo tại từng ngày.');
    }
    // Only the submitted section is replaced; edits in other sections keep their values.
    const result = await requestRef.current<{ version: number }>(path, 'PUT', body, config!.version);
    setConfig(previous => previous && ({ ...previous, version: result.version,
      ...(part === 'weekly' ? { hours: copyHours(hours), basePrices: copyPrices(prices) } : {}),
      ...(part === 'rules' ? { specialPrice: (body as { specialPrice: SpecialPrice | null }).specialPrice,
        legacySpecialPrices: false } : {}), ...(part === 'policy' ? policy : {}) }));
    setDirtyParts(previous => previous.filter(value => value !== part)); onSaved();
    if (draft && part === 'weekly' && venue.status === 'PUBLISHED') showToast('Đã lưu lịch và công bố sân.');
    // Parent owns a persistent load error if this refresh fails; a successful write stays successful.
    await refresh().catch(() => {});
  }
  async function reload() {
    if (busy || dirtyParts.length && !window.confirm('Tải lại sẽ bỏ các thay đổi chưa lưu. Tiếp tục?')) return;
    setBusy(true); setLoading(true); const sequence = ++generation.current;
    try { const next = await requestRef.current<Operations>(configPath);
      if (mounted.current && sequence === generation.current) { hydrate(next); showToast('Đã tải cấu hình.'); }
    } catch (failure) { if (mounted.current) {
      if (isPrivateFailure(failure)) clearPrivate(); setError(failure instanceof Error ? failure.message : 'Không tải được cấu hình.');
    } } finally { if (mounted.current) { setBusy(false); setLoading(false); } }
  }
  const submit = (event: FormEvent, work: () => Promise<void>, message = 'Đã lưu.') => { event.preventDefault(); void run(work, message); };
  const blocked = busy || stale || denied || statusChanged || !editable;
  const previewTime = (value: string) => value.includes('T') ? new Intl.DateTimeFormat('vi-VN', {
    timeZone: venue.timezone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).format(new Date(value)) : shortTime(value);
  function updateHour(day: number, field: 'opensAt' | 'closesAt', value: string) {
    setHours(previous => previous.map(row => row.dayOfWeek === day ? { ...row, [field]: value } : row)); mark('weekly');
  }
  function updatePrice(index: number, field: keyof Price, value: string | number) {
    setPrices(previous => previous.map((row, n) => n === index ? { ...row, [field]: value } : row)); mark('weekly');
  }
  return <>
    {loading && <p role="status">Đang tải cấu hình sân…</p>}
    {error && <p className="feedback error" role="alert">{error}</p>}
    {stale && <p className="feedback error" role="alert">Cấu hình hoặc trạng thái sân đã thay đổi. Tải lại cấu hình và kiểm tra trước khi lưu tiếp.</p>}
    {statusChanged && <div className="feedback error" role="alert">Trạng thái sân đã thay đổi. Tải lại hồ sơ để dùng đúng bộ cấu hình.
      <button type="button" disabled={busy} onClick={() => void refresh()}>Tải lại hồ sơ</button></div>}
    {!loading && <div className="schedule-editor-heading"><p>{court.name} · {statusLabel(court.status)}{dirtyParts.length > 0 && ' · Có thay đổi chưa lưu'}</p>
      <button type="button" disabled={busy} onClick={() => void reload()}>{config ? 'Tải lại cấu hình' : 'Thử tải lại cấu hình'}</button></div>}
    {!loading && config && <fieldset disabled={blocked}>
      {tab === 'pricing' ? <>
        {!editable && <p className="feedback">Hồ sơ đang chờ duyệt hoặc sân ngừng hoạt động. Cấu hình chỉ để xem.</p>}
        <form id="operator-weekly" onSubmit={event => submit(event, () => save('weekly', `${configRoot}/schedule`, { hours, prices }))}>
          <h4>Giờ mở cửa và giá hàng tuần</h4><p>Giá theo ca 30 phút, phủ toàn bộ giờ mở. Tắt ngày để không nhận lịch mới vào ngày đó.</p>
          {!config.hours.length && <p className="muted">Mẫu khởi đầu chưa được lưu. Kiểm tra giờ và giá trước khi lưu.</p>}
          {dayOrder.map(day => {
            const hour = hours.find(row => row.dayOfWeek === day);
            const rows = prices.map((row, index) => ({ row, index })).filter(item => item.row.dayOfWeek === day);
            return <div className="weekly-day" key={day}>
              <label className="day-toggle"><input type="checkbox" checked={!!hour} aria-label={`Mở ${days[day]}`} onChange={event => {
                if (event.target.checked) { setHours([...hours, { dayOfWeek: day, opensAt: '07:00', closesAt: '22:00' }]);
                  setPrices([...prices, { dayOfWeek: day, startsAt: '07:00', endsAt: '22:00', pricePerSlot: 100000 }]); }
                else { setHours(hours.filter(row => row.dayOfWeek !== day)); setPrices(prices.filter(row => row.dayOfWeek !== day)); }
                mark('weekly');
              }} />{days[day]}<small>{hour ? 'Mở cửa' : 'Đóng cửa'}</small></label>
              {hour && <div className="weekly-day-content">
                <div className="schedule-fields"><label>Mở cửa <input aria-label={`Mở cửa ${days[day]}`} type="time" step="1800" required value={hour.opensAt}
                  onChange={event => updateHour(day, 'opensAt', event.target.value)} /></label>
                  <label>Đóng cửa <input aria-label={`Đóng cửa ${days[day]}`} type="time" step="1800" required value={hour.closesAt}
                    onChange={event => updateHour(day, 'closesAt', event.target.value)} /></label></div>
                {rows.map(({ row, index }, n) => <div className="schedule-fields price-row" key={index}>
                  <label>Từ <input aria-label={`Từ ${days[day]}${n ? ` ${n + 1}` : ''}`} type="time" step="1800" required value={row.startsAt}
                    onChange={event => updatePrice(index, 'startsAt', event.target.value)} /></label>
                  <label>Đến <input aria-label={`Đến ${days[day]}${n ? ` ${n + 1}` : ''}`} type="time" step="1800" required value={row.endsAt}
                    onChange={event => updatePrice(index, 'endsAt', event.target.value)} /></label>
                  <label>VND / 30 phút <input aria-label={`Giá ${days[day]}${n ? ` ${n + 1}` : ''}`} type="number" min="1" max={Number.MAX_SAFE_INTEGER} step="1" required value={row.pricePerSlot}
                    onChange={event => updatePrice(index, 'pricePerSlot', Number(event.target.value))} /><small>{money(row.pricePerSlot * 2)} / giờ</small></label>
                  <button type="button" aria-label={`Xóa giá ${days[day]} ${n + 1}`} onClick={() => { setPrices(prices.filter((_, i) => i !== index)); mark('weekly'); }}>Xóa</button>
                </div>)}
                <button type="button" onClick={() => { setPrices([...prices, { dayOfWeek: day, startsAt: rows.at(-1)?.row.endsAt ?? hour.opensAt,
                  endsAt: hour.closesAt, pricePerSlot: 100000 }]); mark('weekly'); }}>Thêm khung giá</button>
                {weeklyErrors[day] && <p className="feedback error" role="alert">{days[day]}: {weeklyErrors[day]}</p>}
              </div>}
            </div>;
          })}
          <button disabled={blocked}>Lưu lịch tuần</button>
        </form>
        {draft ? <div className="panel"><p>{venue.status === 'PUBLISHED'
          ? 'Lưu lịch và giá hợp lệ để sân tự công bố. QR nhận tiền dùng chung theo cơ sở.'
          : 'Lưu lịch và giá, sau đó gửi cơ sở cho Admin duyệt tại Cơ sở & sân. QR nhận tiền dùng chung theo cơ sở.'}</p></div>
          : <>
            <details className="schedule-details"><summary>Giá đặc biệt theo khoảng ngày</summary>
              <form id="operator-pricing" onSubmit={event => submit(event, async () => {
                if (!specialPrice) throw new Error('Thiết lập khoảng ngày và giá trước khi lưu.');
                await save('rules', `${root}/special-price`, { specialPrice });
              }, 'Đã lưu giá đặc biệt cho sân này.')}>
                <h4>Một lịch giá đặc biệt cho sân này</h4>
                <p>Áp dụng mỗi ngày mở cửa trong khoảng ngày đã chọn, tại khung giờ này. Ngoài khoảng đó dùng giá tuần. Mỗi sân có giá riêng.</p>
                {config.legacySpecialPrices && <p className="feedback" role="status">Sân này đang có nhiều quy tắc giá cũ. Lưu lịch mới sẽ thay thế toàn bộ quy tắc cũ; kiểm tra giá Customer theo ngày sau khi lưu.</p>}
                {specialPrice && <div className="special-price-fields">
                  {(['startsOn', 'endsOn', 'startsAt', 'endsAt', 'pricePerSlot'] as const).map(field => {
                    const label = ({ startsOn: 'Hiệu lực từ', endsOn: 'Đến ngày', startsAt: 'Từ giờ', endsAt: 'Đến giờ', pricePerSlot: 'VND / 30 phút' })[field];
                    const type = field === 'startsOn' || field === 'endsOn' ? 'date' : field === 'pricePerSlot' ? 'number' : 'time';
                    return <label key={field}>{label}<input type={type} required step={type === 'time' ? 1800 : type === 'number' ? 1 : undefined}
                      min={type === 'number' ? 1 : undefined} value={specialPrice[field]}
                      onChange={event => { setSpecialPrice({ ...specialPrice, [field]: type === 'number' ? Number(event.target.value) : event.target.value }); mark('rules'); }} /></label>;
                  })}
                </div>}
                <div className="special-price-actions">
                  {!specialPrice && <button type="button" onClick={() => { const date = venueToday(venue.timezone);
                    setSpecialPrice({ startsOn: date, endsOn: date, startsAt: '08:00', endsAt: '10:00', pricePerSlot: 120000 }); mark('rules');
                  }}>Thiết lập giá đặc biệt</button>}
                  {specialPrice && <button disabled={blocked}>Lưu giá đặc biệt</button>}
                  {(config.specialPrice || config.legacySpecialPrices) && <button type="button" className="secondary" disabled={blocked}
                    onClick={() => void run(async () => { await save('rules', `${root}/special-price`, { specialPrice: null });
                      setSpecialPrice(null); }, 'Đã xóa giá đặc biệt; dùng giá tuần.')}>Xóa giá đặc biệt</button>}
                </div>
              </form>
            </details>
            <form id="operator-preview" onSubmit={event => submit(event, async () => {
              setPreview(null); const params = new URLSearchParams(previewInput);
              setPreview(await requestRef.current<Preview>(`${root}/price-preview?${params}`));
            }, 'Đã tính giá.')}>
              <h4>Xem thử giá</h4><p>Tính theo cấu hình đã lưu, không giữ chỗ. Kiểm tra sân trống tại Lịch sân.</p>
              {(['date', 'startsAt', 'endsAt'] as const).map(field => <label key={field}>{field === 'date' ? 'Ngày' : field === 'startsAt' ? 'Từ' : 'Đến'}
                <input required type={field === 'date' ? 'date' : 'time'} step={field === 'date' ? undefined : 1800} value={previewInput[field]}
                  onChange={event => { setPreviewInput({ ...previewInput, [field]: event.target.value }); setPreview(null); }} /></label>)}
              <button disabled={blocked}>Xem giá</button>
              {preview && <><p>Tổng giá: {money(preview.totalPrice)} · {preview.slots.length} ca</p><details><summary>Chi tiết từng ca</summary>
                <ul>{preview.slots.map((slot, n) => <li key={n}>{previewTime(slot.startsAt)}–{previewTime(slot.endsAt)}: {money(slot.pricePerSlot)} · {slot.priority ? 'Giá đặc biệt' : 'Giá tuần'}</li>)}</ul></details></>}
            </form>
            <details className="schedule-details"><summary>Quy định đặt sân</summary>
              <form id="operator-policy" onSubmit={event => submit(event, () => save('policy', `${root}/booking-policy`, policy))}>
                <h4>Quy định đặt sân</h4><p>Khách đủ thời lượng tối thiểu có thể thêm từng ca 30 phút. Giữ chỗ áp dụng trước khi khách báo chuyển khoản.</p>
                <label>Block hiển thị <select value={policy.bookingBlockMinutes} onChange={event => {
                  const block = Number(event.target.value); setPolicy({ ...policy, bookingBlockMinutes: block,
                    minimumBookingMinutes: Math.ceil(policy.minimumBookingMinutes / block) * block }); mark('policy');
                }}>{[30, 60, 90].map(block => <option key={block} value={block}>{block} phút</option>)}</select></label>
                <label>Thời lượng đặt tối thiểu <input type="number" min={policy.bookingBlockMinutes} max="480" step={policy.bookingBlockMinutes} required
                  value={policy.minimumBookingMinutes} onChange={event => { setPolicy({ ...policy, minimumBookingMinutes: Number(event.target.value) }); mark('policy'); }} /> phút</label>
                <label>Giữ chỗ trước khi báo chuyển khoản <input type="number" min="5" max="60" required value={policy.holdMinutes}
                  onChange={event => { setPolicy({ ...policy, holdMinutes: Number(event.target.value) }); mark('policy'); }} /> phút</label>
                <button disabled={blocked}>Lưu quy định</button>
              </form>
            </details>
          </>}
      </> : draft ? <div className="panel"><p>Lưu lịch và giá để công bố sân trước khi tạo bảo trì.</p></div> : <>
        <form id="operator-maintenance" onSubmit={event => submit(event, async () => {
          await requestRef.current(`${root}/maintenance`, 'POST', block); setBlock({ ...block, reason: '' });
          setDirtyParts(previous => previous.filter(part => part !== 'maintenance')); setMaintenanceRevision(value => value + 1); onSaved();
        }, 'Đã khóa ca bảo trì.')}>
          <h4>Bảo trì sân</h4><p>Khóa khoảng sân không nhận khách; không được chồng lên đơn hoặc báo giá đang giữ.</p>
          {(['date', 'startsAt', 'endsAt', 'reason'] as const).map(field => <label key={field}>{({ date: 'Ngày', startsAt: 'Từ', endsAt: 'Đến', reason: 'Lý do' })[field]}
            <input type={field === 'date' ? 'date' : field === 'reason' ? 'text' : 'time'} required step={['startsAt', 'endsAt'].includes(field) ? 1800 : undefined}
              minLength={field === 'reason' ? 5 : undefined} maxLength={field === 'reason' ? 500 : undefined} value={block[field]}
              onChange={event => { setBlock({ ...block, [field]: event.target.value }); mark('maintenance'); }} /></label>)}
          <button disabled={blocked}>Khóa ca bảo trì</button>
        </form>
        <div className="panel"><h4>Ca bảo trì đang giữ</h4>
          {maintenanceError ? <p className="feedback error" role="alert">{maintenanceError}</p> : !maintenance.length ? <p>Chưa có ca bảo trì.</p>
            : <ul className="maintenance-list">{maintenance.map(item => <li key={item.id}>{new Date(item.startsAt).toLocaleString('vi-VN', { timeZone: venue.timezone })}
              {' – '}{new Date(item.endsAt).toLocaleString('vi-VN', { timeZone: venue.timezone })} · {item.reason}
              <button type="button" disabled={blocked} onClick={() => void run(async () => {
                await requestRef.current(`${root}/maintenance/${item.id}/cancel`, 'POST');
                setMaintenanceRevision(value => value + 1); onSaved();
              }, 'Đã hủy bảo trì.')}>Hủy bảo trì</button></li>)}</ul>}
          {maintenanceError && <button type="button" onClick={() => setMaintenanceRevision(value => value + 1)}>Tải lại bảo trì</button>}
        </div>
      </>}
    </fieldset>}
  </>;
}
