import { FormEvent, useEffect, useRef, useState } from 'react';
import { PartnerOperations } from './PartnerOperations';
import { MapTilerPlacePicker } from './MapTilerPlacePicker';
import { PartnerShell } from './layouts/PartnerShell';
import { PartnerOverview } from './features/workspace/PartnerOverview';
import { statusLabel, usePartnerNavigation } from './features/workspace/navigation';
import { PartnerApiError, type RequestOptions } from './features/bookings/types';
import { PartnerBookings } from './features/bookings/PartnerBookings';
import { PartnerNotifications, usePartnerNotifications } from './features/notifications/PartnerNotifications';

type Row = { id: string; name: string; status: string };
type Court = { id: string; name: string; status: string; hours: object[]; prices: object[] };
type Venue = { id: string; name: string; status: string; version: number; address: string; contact: string;
  imageUploadId?: string;
  latitude: number; longitude: number; timezone: string;
  paymentAccount?: { maskedAccountNumber: string }; courts: Court[] };
type Detail = Row & { version: number; legalName: string; contact: string;
  approval?: { status: string; reason?: string }; venues: Venue[] };
type Envelope<T> = { data: T };
type ScheduleDraft = { dayOfWeek: number; startsAt: string; endsAt: string; pricePerSlot: number };
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
const days = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];

export function PartnerOnboarding({ sendRequest, onLogout, logoutBusy }: {
  sendRequest: (path: string, options?: RequestInit) => Promise<Response>;
  onLogout: () => void; logoutBusy: boolean }) {
  const { page, bookingId, visited, navigate, openBooking } = usePartnerNavigation();
  const [businesses, setBusinesses] = useState<Row[]>([]);
  const [selected, setSelected] = useState('');
  const [detail, setDetail] = useState<Detail | null>(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [bookingBusy, setBookingBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const loadedBusiness = useRef('');
  const initialLoadStarted = useRef(false);
  const [businessName, setBusinessName] = useState('');
  const [legalName, setLegalName] = useState('');
  const [contact, setContact] = useState('');
  const [venueName, setVenueName] = useState('');
  const [address, setAddress] = useState('');
  const [venueContact, setVenueContact] = useState('');
  const [latitude, setLatitude] = useState('');
  const [longitude, setLongitude] = useState('');
  const [createPickerKey, setCreatePickerKey] = useState(0);
  const [timezone, setTimezone] = useState('Asia/Ho_Chi_Minh');
  const [courtVenue, setCourtVenue] = useState('');
  const [courtName, setCourtName] = useState('');
  const [scheduleCourt, setScheduleCourt] = useState('');
  const [scheduleRows, setScheduleRows] = useState<ScheduleDraft[]>([
    { dayOfWeek: 1, startsAt: '07:00', endsAt: '22:00', pricePerSlot: 100000 }]);
  const [paymentVenue, setPaymentVenue] = useState('');
  const [bankCode, setBankCode] = useState('');
  const [accountName, setAccountName] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [qrFile, setQrFile] = useState<File | null>(null);
  const [imageVenue, setImageVenue] = useState('');
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [revisionVenue, setRevisionVenue] = useState('');
  const [revisionAddress, setRevisionAddress] = useState('');
  const [revisionContact, setRevisionContact] = useState('');
  const [revisionLatitude, setRevisionLatitude] = useState('');
  const [revisionLongitude, setRevisionLongitude] = useState('');
  const [revisionTimezone, setRevisionTimezone] = useState('Asia/Ho_Chi_Minh');

  async function request<T>(path: string, method = 'GET', body?: object, version?: number, options?: RequestOptions): Promise<T> {
    const response = await sendRequest(`/api/v1${path}`, { method,
      signal: options?.signal,
      headers: { ...(body || options?.bodyText ? { 'Content-Type': 'application/json' } : {}),
        ...(version !== undefined ? { 'If-Match': `"${version}"` } : {}),
        ...(options?.idempotencyKey ? { 'Idempotency-Key': options.idempotencyKey } : {}) },
      ...(body || options?.bodyText ? { body: options?.bodyText ?? JSON.stringify(body) } : {}) });
    if (!response.ok) {
      const error = await response.json().catch(() => ({})) as { code?: string };
      const failure = new PartnerApiError(error.code ?? 'REQUEST_FAILED', response.status);
      failure.message = error.code ? `Yêu cầu không thành công (${error.code}).` : 'Không thể kết nối API.';
      throw failure;
    }
    if (options?.blob) return await response.blob() as T;
    const result = await response.json() as Envelope<T>;
    return options?.envelope ? result as T : result.data;
  }
  const notifications = usePartnerNotifications(request);
  async function load(id?: string) {
    setLoading(true); setLoadError('');
    try {
      const list = await request<Row[]>('/partner-onboarding/businesses');
      const target = id || selected || list[0]?.id;
      const next = target ? await request<Detail>(`/partner-onboarding/businesses/${target}`) : null;
      if (target !== loadedBusiness.current) {
        setCourtVenue(''); setCourtName(''); setScheduleCourt(''); setPaymentVenue(''); setImageVenue('');
        setImageFile(null); setQrFile(null); setBankCode(''); setAccountName(''); setAccountNumber('');
        setVenueName(''); setVenueContact(''); setAddress(''); setLatitude(''); setLongitude('');
        setTimezone('Asia/Ho_Chi_Minh'); setCreatePickerKey(value => value + 1);
        setScheduleRows([{ dayOfWeek: 1, startsAt: '07:00', endsAt: '22:00', pricePerSlot: 100000 }]);
        setRevisionVenue(''); setRevisionAddress(''); setRevisionContact('');
        setRevisionLatitude(''); setRevisionLongitude(''); setRevisionTimezone('Asia/Ho_Chi_Minh');
        loadedBusiness.current = target ?? '';
      }
      setBusinesses(list); setSelected(target ?? ''); setDetail(next);
    } catch (error) { setLoadError(error instanceof Error ? error.message : 'Không tải được hồ sơ.'); throw error; }
    finally { setLoading(false); }
  }
  useEffect(() => {
    if (initialLoadStarted.current) return;
    initialLoadStarted.current = true;
    void load().catch(() => {});
  }, []);
  async function run(work: () => Promise<void>) {
    setBusy(true); setMessage('');
    try { await work(); setMessage('Đã lưu.'); }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Có lỗi xảy ra.'); }
    finally { setBusy(false); }
  }
  function submit(event: FormEvent, work: () => Promise<void>) { event.preventDefault(); void run(work); }
  async function upload(file: File, venueId: string, purpose: 'QR' | 'VENUE_IMAGE') {
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 5_242_880)
      throw new Error('Ảnh phải là PNG, JPEG hoặc WebP và không quá 5 MB.');
    const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', await file.arrayBuffer()));
    const sha256Base64 = btoa(String.fromCharCode(...digest));
    const presigned = await request<{ id: string; uploadUrl: string; uploadHeaders: Record<string, string> }>('/uploads/presign', 'POST',
      { venueId, purpose, contentType: file.type, sizeBytes: file.size, sha256Base64 });
    const sent = await fetch(presigned.uploadUrl, { method: 'PUT', headers: presigned.uploadHeaders, body: file });
    if (!sent.ok) throw new Error('Không tải được ảnh lên kho lưu trữ.');
    await request(`/uploads/${presigned.id}/complete`, 'POST');
    return presigned.id;
  }

  return <PartnerShell page={page} navigate={navigate} unread={notifications.unread}
    onLogout={onLogout} logoutBusy={logoutBusy} businessControl={
      <label className="business-switcher">Doanh nghiệp <select value={selected} disabled={busy || loading || bookingBusy}
        onChange={event => { if (page === 'bookings') openBooking(null); void run(() => load(event.target.value)); }}>
        {businesses.length === 0 && <option value="">Chưa có hồ sơ</option>}
        {businesses.map(b => <option key={b.id} value={b.id}>{b.name} · {statusLabel(b.status)}</option>)}
      </select></label>}>
    <h2 className="sr-only">Hồ sơ chủ sân</h2>
    {message && <p className={`feedback ${message === 'Đã lưu.' ? 'success' : 'error'}`} role="status">{message}</p>}
    {busy && <p className="feedback" role="status">Đang xử lý…</p>}
    {loading && <p className="feedback" role="status">Đang tải hồ sơ…</p>}
    {loadError && <div className="feedback error" role="alert">{loadError}<button type="button" disabled={loading}
      onClick={() => void load().catch(() => {})}>Thử tải lại hồ sơ</button></div>}
    {page === 'notifications' && <PartnerNotifications model={notifications} openBooking={openBooking} />}
    {page === 'bookings' && !loading && detail && (detail.status === 'ACTIVE'
      ? <PartnerBookings key={`bookings-${detail.id}`} businessId={detail.id} venues={detail.venues} request={request} bookingId={bookingId}
          openBooking={openBooking} onActionBusy={setBookingBusy} selectBusiness={id => {
            if (!businesses.some(item => item.id === id)) return false;
            void load(id).catch(() => {}); return true;
          }} />
      : <section className="panel empty-state"><h3>Chưa thể xử lý đơn đặt sân</h3><p>Doanh nghiệp cần được duyệt và đang hoạt động để đối chiếu thanh toán.</p></section>)}
    <fieldset disabled={busy || loading} key={detail?.id ?? 'new'}>
    {!detail && !loading && !loadError && !['notifications', 'bookings'].includes(page) && <form onSubmit={event => submit(event, async () => {
      const result = await request<{ id: string }>('/partner-onboarding/businesses', 'POST',
        { name: businessName, legalName, contact }); await load(result.id); navigate('venues');
    })}>
      <h3>Tạo doanh nghiệp</h3>
      <label>Tên hiển thị <input required value={businessName} onChange={e => setBusinessName(e.target.value)} /></label>
      <label>Tên pháp lý <input required value={legalName} onChange={e => setLegalName(e.target.value)} /></label>
      <label>Liên hệ <input required value={contact} onChange={e => setContact(e.target.value)} /></label>
      <button disabled={busy}>Tạo hồ sơ nháp</button>
    </form>}
    {detail && <>
      <div className="status-banner"><p><strong>{detail.name}</strong> · <span className={`status-badge status-${detail.status.toLowerCase()}`}>{statusLabel(detail.status)}</span></p>
        {detail.status === 'DRAFT' && <button type="button" className="primary-action" disabled={busy} onClick={() => void run(async () => {
          await request(`/partner-onboarding/businesses/${detail.id}/submit`, 'POST'); await load(detail.id);
        })}>Gửi hồ sơ duyệt</button>}
        <button type="button" disabled={busy || loading} onClick={() => void load(detail.id).catch(() => {})}>Tải lại hồ sơ</button>
      </div>
      {detail.approval?.status === 'CHANGES_REQUESTED' && <p className="feedback error">Admin yêu cầu bổ sung: {detail.approval.reason}</p>}
      {detail.status === 'PENDING_APPROVAL' && <p className="feedback">Hồ sơ đang chờ Admin duyệt. Bạn có thể xem thông tin và theo dõi phản hồi trong mục Thông báo.</p>}
      {detail.status === 'ACTIVE' && detail.approval?.status === 'PENDING' && <p className="feedback">Đề nghị thay đổi đang chờ Admin duyệt. Thông tin đang hoạt động tiếp tục được sử dụng cho đến khi thay đổi được duyệt.</p>}
      {page === 'overview' && <PartnerOverview name={detail.name} status={detail.status} venues={detail.venues} navigate={navigate} />}
      {page === 'profile' && detail.status !== 'DRAFT' && <section className="panel"><h3>Thông tin doanh nghiệp</h3><p>Tên pháp lý: {detail.legalName}</p><p>Liên hệ: {detail.contact}</p></section>}
      {detail.status === 'DRAFT' && <form hidden={page !== 'profile'} key={detail.id} onSubmit={event => submit(event, async () => {
        const fields = new FormData(event.currentTarget);
        await request(`/partner-onboarding/businesses/${detail.id}`, 'PUT', {
          name: String(fields.get('name') ?? ''), legalName: String(fields.get('legalName') ?? ''),
          contact: String(fields.get('contact') ?? '') }, detail.version);
        await load(detail.id);
      })}>
        <h3>Thông tin doanh nghiệp</h3>
        <label>Tên <input name="name" required defaultValue={detail.name} /></label>
        <label>Tên pháp lý <input name="legalName" required defaultValue={detail.legalName} /></label>
        <label>Liên hệ <input name="contact" required defaultValue={detail.contact} /></label>
        <button disabled={busy}>Lưu thay đổi</button>
      </form>}
      {visited.has('venues') && detail.status === 'DRAFT' && <form hidden={page !== 'venues'} onSubmit={event => submit(event, async () => {
        if (!address || !latitude || !longitude) throw new Error('Chọn và xác nhận địa chỉ trên MapTiler.');
        await request(`/partner-onboarding/businesses/${detail.id}/venues`, 'POST',
          { name: venueName, address, contact: venueContact, timezone,
            latitude: Number(latitude), longitude: Number(longitude) });
        setVenueName(''); setAddress(''); setLatitude(''); setLongitude(''); setCreatePickerKey(value => value + 1);
        setVenueContact(''); await load(detail.id);
      })}>
        <h3>Thêm cơ sở</h3>
        <label>Tên <input required value={venueName} onChange={e => setVenueName(e.target.value)} /></label>
        <MapTilerPlacePicker key={createPickerKey} onChange={value => {
          setAddress(value?.address ?? ''); setLatitude(value ? String(value.latitude) : '');
          setLongitude(value ? String(value.longitude) : '');
        }} />
        <label>Liên hệ cơ sở <input required value={venueContact} onChange={e => setVenueContact(e.target.value)} /></label>
        <label>Múi giờ IANA <input required value={timezone} onChange={e => setTimezone(e.target.value)} /></label>
        <button disabled={busy}>Lưu cơ sở</button>
      </form>}
      {detail.venues.map(v => <article className="venue-detail-card" hidden={!['venues', 'courts', 'media', 'payments'].includes(page)} key={v.id}>
        <h3>{v.name} · {statusLabel(v.status)}</h3><p>{v.address} · {v.contact}</p>
        <p>Ảnh: {v.imageUploadId ? 'Đã tải' : 'Chưa có'} · QR: {v.paymentAccount ? 'Đã khai báo' : 'Chưa có'}</p>
        {page === 'payments' && v.paymentAccount && <p>Số tài khoản: {v.paymentAccount.maskedAccountNumber}</p>}
        {visited.has('venues') && detail.status === 'DRAFT' && <form hidden={page !== 'venues'} key={v.version} onSubmit={event => submit(event, async () => {
          const fields = new FormData(event.currentTarget);
          if (!fields.get('address') || !fields.get('latitude') || !fields.get('longitude'))
            throw new Error('Chọn và xác nhận địa chỉ trên MapTiler.');
          await request(`/partner-onboarding/venues/${v.id}`, 'PUT', {
            name: String(fields.get('name') ?? ''), address: String(fields.get('address') ?? ''),
            contact: String(fields.get('contact') ?? ''),
            timezone: String(fields.get('timezone') ?? ''), latitude: Number(fields.get('latitude')),
            longitude: Number(fields.get('longitude')) }, v.version);
          await load(detail.id);
        })}>
          <label>Tên <input name="name" required defaultValue={v.name} /></label>
          <MapTilerPlacePicker initial={{ address: v.address, latitude: v.latitude, longitude: v.longitude }} />
          <label>Liên hệ cơ sở <input name="contact" required defaultValue={v.contact} /></label>
          <label>Múi giờ <input name="timezone" required defaultValue={v.timezone} /></label>
          <button disabled={busy}>Sửa cơ sở</button>
        </form>}
        {v.courts.map(c => <div className="court-item" hidden={page !== 'courts'} key={c.id}>
          <p>Sân {c.name}: {c.hours.length} ngày mở cửa, {c.prices.length} khung giá</p>
          {detail.status === 'DRAFT' && <form onSubmit={event => submit(event, async () => {
            const fields = new FormData(event.currentTarget);
            await request(`/partner-onboarding/courts/${c.id}`, 'PUT', { name: String(fields.get('name') ?? '') });
            await load(detail.id);
          })}>
            <label>Tên sân <input name="name" required defaultValue={c.name} /></label>
            <button disabled={busy}>Sửa sân</button>
          </form>}
        </div>)}
      </article>)}
      {detail.status === 'DRAFT' && detail.venues.length > 0 && <>
        <form hidden={page !== 'courts'} onSubmit={event => submit(event, async () => {
          await request(`/partner-onboarding/venues/${courtVenue}/courts`, 'POST', { name: courtName });
          setCourtName(''); await load(detail.id);
        })}>
          <h3>Thêm sân</h3>
          <label>Cơ sở <select required value={courtVenue} onChange={e => setCourtVenue(e.target.value)}><option value="">Chọn</option>
            {detail.venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
          <label>Tên sân <input required value={courtName} onChange={e => setCourtName(e.target.value)} /></label>
          <button disabled={busy}>Lưu sân</button>
        </form>
        <form hidden={page !== 'schedule'} onSubmit={event => submit(event, async () => {
          const grouped = new Map<number, ScheduleDraft[]>();
          for (const row of scheduleRows) grouped.set(row.dayOfWeek, [...(grouped.get(row.dayOfWeek) ?? []), row]);
          const hours = [...grouped.entries()].map(([dayOfWeek, ranges]) => {
            const sorted = ranges.sort((a, b) => a.startsAt.localeCompare(b.startsAt));
            return { dayOfWeek, opensAt: sorted[0].startsAt, closesAt: sorted[sorted.length - 1].endsAt };
          });
          await request(`/partner-onboarding/courts/${scheduleCourt}/schedule`, 'PUT', {
            hours, prices: scheduleRows });
          await load(detail.id);
        })}>
          <h3>Giờ và giá theo sân</h3>
          <label>Sân <select required value={scheduleCourt} onChange={e => setScheduleCourt(e.target.value)}><option value="">Chọn</option>
            {detail.venues.flatMap(v => v.courts.map(c => <option key={c.id} value={c.id}>{v.name} / {c.name}</option>))}</select></label>
          {scheduleRows.map((row, index) => <div key={index}>
            <label>Ngày <select value={row.dayOfWeek} onChange={e => setScheduleRows(scheduleRows.map((item, n) =>
              n === index ? { ...item, dayOfWeek: Number(e.target.value) } : item))}>
              {days.map((d, n) => <option key={d} value={n}>{d}</option>)}</select></label>
            <label>Từ <input required type="time" step="1800" value={row.startsAt} onChange={e => setScheduleRows(scheduleRows.map((item, n) =>
              n === index ? { ...item, startsAt: e.target.value } : item))} /></label>
            <label>Đến <input required type="time" step="1800" value={row.endsAt} onChange={e => setScheduleRows(scheduleRows.map((item, n) =>
              n === index ? { ...item, endsAt: e.target.value } : item))} /></label>
            <label>Giá/30 phút <input required type="number" min="1" value={row.pricePerSlot} onChange={e => setScheduleRows(scheduleRows.map((item, n) =>
              n === index ? { ...item, pricePerSlot: Number(e.target.value) } : item))} /></label>
            <button type="button" onClick={() => setScheduleRows(scheduleRows.filter((_, n) => n !== index))}>Xóa khung</button>
          </div>)}
          <button type="button" onClick={() => setScheduleRows([...scheduleRows,
            { dayOfWeek: 2, startsAt: '07:00', endsAt: '22:00', pricePerSlot: 100000 }])}>Thêm khung giá</button>
          <button disabled={busy}>Lưu giờ/giá</button>
        </form>
        <form hidden={page !== 'media'} onSubmit={event => submit(event, async () => {
          if (!imageFile) throw new Error('Chọn ảnh cơ sở.');
          const uploadId = await upload(imageFile, imageVenue, 'VENUE_IMAGE');
          await request(`/partner-onboarding/venues/${imageVenue}/image`, 'PUT', { uploadId });
          setImageFile(null); await load(detail.id);
        })}>
          <h3>Ảnh cơ sở</h3>
          <p>PNG, JPEG hoặc WebP, tối đa 5 MB. Ảnh được lưu khi tải lên thành công.</p>
          <label>Cơ sở <select required value={imageVenue} onChange={e => setImageVenue(e.target.value)}><option value="">Chọn</option>
            {detail.venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
          <input aria-label="Ảnh cơ sở" required type="file" accept="image/png,image/jpeg,image/webp" onChange={e => setImageFile(e.target.files?.[0] ?? null)} />
          <button disabled={busy}>Tải ảnh</button>
        </form>
        <form hidden={page !== 'payments'} onSubmit={event => submit(event, async () => {
          if (!qrFile) throw new Error('Chọn ảnh QR.');
          const qrUploadId = await upload(qrFile, paymentVenue, 'QR');
          await request(`/partner-onboarding/venues/${paymentVenue}/payment-account`, 'PUT',
            { bankCode, accountName, accountNumber, qrUploadId });
          setAccountNumber(''); setQrFile(null); await load(detail.id);
        })}>
          <h3>Tài khoản nhận tiền</h3>
          <p>Chọn đúng cơ sở và tải QR nhận tiền. PNG, JPEG hoặc WebP, tối đa 5 MB.</p>
          <label>Cơ sở <select required value={paymentVenue} onChange={e => setPaymentVenue(e.target.value)}><option value="">Chọn</option>
            {detail.venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
          <label>Mã ngân hàng <input required value={bankCode} onChange={e => setBankCode(e.target.value)} /></label>
          <label>Tên tài khoản <input required value={accountName} onChange={e => setAccountName(e.target.value)} /></label>
          <label>Số tài khoản <input required inputMode="numeric" value={accountNumber} onChange={e => setAccountNumber(e.target.value)} /></label>
          <input aria-label="Ảnh QR" required type="file" accept="image/png,image/jpeg,image/webp" onChange={e => setQrFile(e.target.files?.[0] ?? null)} />
          <button disabled={busy}>Lưu QR và tài khoản</button>
        </form>
      </>}
      {detail.venues.length === 0 && ['courts', 'schedule', 'media', 'payments'].includes(page) && <section className="panel empty-state">
        <h3>Thêm cơ sở trước khi cấu hình</h3><p>Sân, ảnh và tài khoản nhận tiền đều thuộc một cơ sở.</p>
        <a className="button secondary" href="#/venues" onClick={() => navigate('venues')}>Đến mục Cơ sở</a>
      </section>}
      {visited.has('payments') && detail.status === 'ACTIVE' && detail.approval?.status !== 'PENDING' && <form hidden={page !== 'payments'} onSubmit={event => submit(event, async () => {
        if (!revisionVenue || !revisionAddress || !revisionLatitude || !revisionLongitude)
          throw new Error('Chọn cơ sở và xác nhận địa chỉ trên MapTiler.');
        if (!qrFile) throw new Error('Chọn QR mới.');
        const qrUploadId = await upload(qrFile, revisionVenue, 'QR');
        await request(`/partner-onboarding/venues/${revisionVenue}/revisions`, 'POST', {
          address: revisionAddress, contact: revisionContact,
          latitude: Number(revisionLatitude), longitude: Number(revisionLongitude),
          timezone: revisionTimezone, bankCode, accountName, accountNumber, qrUploadId });
        setAccountNumber(''); setQrFile(null); await load(detail.id);
      })}>
        <h3>Đề nghị thay đổi thông tin quan trọng</h3>
        <p>Địa chỉ và tài khoản đang hoạt động tiếp tục được dùng cho đến khi Admin duyệt bản mới.</p>
        <label>Cơ sở <select required value={revisionVenue} onChange={e => {
          const venue = detail.venues.find(v => v.id === e.target.value);
          setRevisionVenue(e.target.value); setRevisionAddress(venue?.address ?? '');
          setRevisionContact(venue?.contact ?? '');
          setRevisionLatitude(String(venue?.latitude ?? '')); setRevisionLongitude(String(venue?.longitude ?? ''));
          setRevisionTimezone(venue?.timezone ?? 'Asia/Ho_Chi_Minh');
        }}><option value="">Chọn</option>{detail.venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
        {revisionVenue && <MapTilerPlacePicker key={revisionVenue} initial={{ address: revisionAddress,
          latitude: Number(revisionLatitude), longitude: Number(revisionLongitude) }} onChange={value => {
          setRevisionAddress(value?.address ?? ''); setRevisionLatitude(value ? String(value.latitude) : '');
          setRevisionLongitude(value ? String(value.longitude) : '');
        }} />}
        <label>Liên hệ cơ sở <input required value={revisionContact} onChange={e => setRevisionContact(e.target.value)} /></label>
        <label>Múi giờ <input required value={revisionTimezone} onChange={e => setRevisionTimezone(e.target.value)} /></label>
        <label>Mã ngân hàng <input required value={bankCode} onChange={e => setBankCode(e.target.value)} /></label>
        <label>Tên tài khoản <input required value={accountName} onChange={e => setAccountName(e.target.value)} /></label>
        <label>Số tài khoản <input required inputMode="numeric" value={accountNumber} onChange={e => setAccountNumber(e.target.value)} /></label>
        <input aria-label="QR mới" required type="file" accept="image/png,image/jpeg,image/webp" onChange={e => setQrFile(e.target.files?.[0] ?? null)} />
        <button disabled={busy}>Gửi thay đổi để duyệt</button>
      </form>}
      {visited.has('schedule') && detail.status === 'ACTIVE' && <div hidden={page !== 'schedule'}>
        <PartnerOperations key={detail.id} venues={detail.venues} request={request} />
      </div>}
    </>}
    </fieldset>
  </PartnerShell>;
}
