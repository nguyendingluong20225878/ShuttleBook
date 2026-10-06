import { FormEvent, useEffect, useRef, useState } from 'react';
import { PartnerOperations } from './PartnerOperations';
import { MapTilerPlacePicker } from './MapTilerPlacePicker';

type Session = { accessToken: string; refreshToken: string };
type Row = { id: string; name: string; status: string };
type Court = { id: string; name: string; status: string; hours: object[]; prices: object[] };
type Venue = { id: string; name: string; status: string; version: number; address: string; contact: string;
  imageUploadId?: string;
  latitude: number; longitude: number; timezone: string;
  paymentAccount?: { maskedAccountNumber: string }; courts: Court[] };
type Detail = Row & { version: number; legalName: string; contact: string;
  approval?: { status: string; reason?: string }; venues: Venue[] };
type Envelope<T> = { data: T };
type Notice = { id: string; title: string; body: string; readAt?: string };
type ScheduleDraft = { dayOfWeek: number; startsAt: string; endsAt: string; pricePerSlot: number };
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
const days = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];

export function PartnerOnboarding({ session, onSession, onExpired }: {
  session: Session; onSession: (next: Session) => void; onExpired: () => void }) {
  const tokens = useRef(session);
  const refreshing = useRef<Promise<Session> | null>(null);
  const [businesses, setBusinesses] = useState<Row[]>([]);
  const [selected, setSelected] = useState('');
  const [detail, setDetail] = useState<Detail | null>(null);
  const [message, setMessage] = useState('');
  const [notices, setNotices] = useState<Notice[]>([]);
  const [busy, setBusy] = useState(false);
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

  async function request<T>(path: string, method = 'GET', body?: object, version?: number): Promise<T> {
    const send = (bearer: string) => fetch(`${base}/api/v1${path}`, { method,
      headers: { Authorization: `Bearer ${bearer}`, ...(body ? { 'Content-Type': 'application/json' } : {}),
        ...(version !== undefined ? { 'If-Match': `"${version}"` } : {}) },
      ...(body ? { body: JSON.stringify(body) } : {}) });
    const accessToken = tokens.current.accessToken;
    let response = await send(accessToken);
    if (response.status === 401) {
      if (tokens.current.accessToken !== accessToken) response = await send(tokens.current.accessToken);
      else {
        if (!refreshing.current) refreshing.current = (async () => {
          const refreshed = await fetch(`${base}/api/v1/auth/refresh`, { method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken: tokens.current.refreshToken }) });
          if (!refreshed.ok) throw new Error('Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.');
          const next = (await refreshed.json() as Envelope<Session>).data;
          if (!next?.accessToken || !next.refreshToken) throw new Error('Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.');
          tokens.current = next; onSession(next);
          return next;
        })().catch(error => { onExpired(); throw error; }).finally(() => { refreshing.current = null; });
        response = await send((await refreshing.current).accessToken);
      }
    }
    if (!response.ok) {
      const error = await response.json().catch(() => ({})) as { code?: string };
      throw new Error(error.code ? `Yêu cầu không thành công (${error.code}).` : 'Không thể kết nối API.');
    }
    return (await response.json() as Envelope<T>).data;
  }
  async function load(id?: string) {
    const list = await request<Row[]>('/partner-onboarding/businesses');
    setBusinesses(list);
    const target = id || selected || list[0]?.id;
    if (target) { setSelected(target); setDetail(await request<Detail>(`/partner-onboarding/businesses/${target}`)); }
    else setDetail(null);
  }
  useEffect(() => {
    void load().catch(error => setMessage(String(error)));
    void request<Notice[]>('/me/notifications/').then(setNotices).catch(() => {});
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

  return <section>
    <h2>Hồ sơ chủ sân</h2>
    {message && <p role="status">{message}</p>}
    {notices.length > 0 && <aside><h3>Thông báo</h3><ul>{notices.map(notice => <li key={notice.id}>
      <strong>{notice.title}</strong>: {notice.body}
      {!notice.readAt && <button type="button" onClick={() => void run(async () => {
        await request(`/me/notifications/${notice.id}/read`, 'POST');
        setNotices(await request<Notice[]>('/me/notifications/'));
      })}>Đã đọc</button>}
    </li>)}</ul></aside>}
    <label>Doanh nghiệp <select value={selected} onChange={event => void run(() => load(event.target.value))}>
      <option value="">Chọn hồ sơ</option>{businesses.map(b => <option key={b.id} value={b.id}>{b.name} · {b.status}</option>)}
    </select></label>
    {!detail && <form onSubmit={event => submit(event, async () => {
      const result = await request<{ id: string }>('/partner-onboarding/businesses', 'POST',
        { name: businessName, legalName, contact }); await load(result.id);
    })}>
      <h3>Tạo doanh nghiệp</h3>
      <label>Tên hiển thị <input required value={businessName} onChange={e => setBusinessName(e.target.value)} /></label>
      <label>Tên pháp lý <input required value={legalName} onChange={e => setLegalName(e.target.value)} /></label>
      <label>Liên hệ <input required value={contact} onChange={e => setContact(e.target.value)} /></label>
      <button disabled={busy}>Tạo hồ sơ nháp</button>
    </form>}
    {detail && <>
      <p><strong>{detail.name}</strong> · {detail.status}</p>
      {detail.approval?.status === 'CHANGES_REQUESTED' && <p>Admin yêu cầu bổ sung: {detail.approval.reason}</p>}
      {detail.status === 'DRAFT' && <form key={detail.id} onSubmit={event => submit(event, async () => {
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
      {detail.status === 'DRAFT' && <form onSubmit={event => submit(event, async () => {
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
      {detail.venues.map(v => <article key={v.id}>
        <h3>{v.name} · {v.status}</h3><p>{v.address} · {v.contact}</p>
        <p>Ảnh: {v.imageUploadId ? 'Đã tải' : 'Chưa có'} · QR: {v.paymentAccount ? 'Đã khai báo' : 'Chưa có'}</p>
        {detail.status === 'DRAFT' && <form key={v.version} onSubmit={event => submit(event, async () => {
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
        {v.courts.map(c => <div key={c.id}>
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
        <form onSubmit={event => submit(event, async () => {
          await request(`/partner-onboarding/venues/${courtVenue}/courts`, 'POST', { name: courtName });
          setCourtName(''); await load(detail.id);
        })}>
          <h3>Thêm sân</h3>
          <label>Cơ sở <select required value={courtVenue} onChange={e => setCourtVenue(e.target.value)}><option value="">Chọn</option>
            {detail.venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
          <label>Tên sân <input required value={courtName} onChange={e => setCourtName(e.target.value)} /></label>
          <button disabled={busy}>Lưu sân</button>
        </form>
        <form onSubmit={event => submit(event, async () => {
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
        <form onSubmit={event => submit(event, async () => {
          if (!imageFile) throw new Error('Chọn ảnh cơ sở.');
          const uploadId = await upload(imageFile, imageVenue, 'VENUE_IMAGE');
          await request(`/partner-onboarding/venues/${imageVenue}/image`, 'PUT', { uploadId });
          setImageFile(null); await load(detail.id);
        })}>
          <h3>Ảnh cơ sở</h3>
          <label>Cơ sở <select required value={imageVenue} onChange={e => setImageVenue(e.target.value)}><option value="">Chọn</option>
            {detail.venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
          <input aria-label="Ảnh cơ sở" required type="file" accept="image/png,image/jpeg,image/webp" onChange={e => setImageFile(e.target.files?.[0] ?? null)} />
          <button disabled={busy}>Tải ảnh</button>
        </form>
        <form onSubmit={event => submit(event, async () => {
          if (!qrFile) throw new Error('Chọn ảnh QR.');
          const qrUploadId = await upload(qrFile, paymentVenue, 'QR');
          await request(`/partner-onboarding/venues/${paymentVenue}/payment-account`, 'PUT',
            { bankCode, accountName, accountNumber, qrUploadId });
          setAccountNumber(''); setQrFile(null); await load(detail.id);
        })}>
          <h3>Tài khoản nhận tiền</h3>
          <label>Cơ sở <select required value={paymentVenue} onChange={e => setPaymentVenue(e.target.value)}><option value="">Chọn</option>
            {detail.venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
          <label>Mã ngân hàng <input required value={bankCode} onChange={e => setBankCode(e.target.value)} /></label>
          <label>Tên tài khoản <input required value={accountName} onChange={e => setAccountName(e.target.value)} /></label>
          <label>Số tài khoản <input required inputMode="numeric" value={accountNumber} onChange={e => setAccountNumber(e.target.value)} /></label>
          <input aria-label="Ảnh QR" required type="file" accept="image/png,image/jpeg,image/webp" onChange={e => setQrFile(e.target.files?.[0] ?? null)} />
          <button disabled={busy}>Lưu QR và tài khoản</button>
        </form>
        <button type="button" disabled={busy} onClick={() => void run(async () => {
          await request(`/partner-onboarding/businesses/${detail.id}/submit`, 'POST'); await load(detail.id);
        })}>Gửi hồ sơ duyệt</button>
      </>}
      {detail.status === 'ACTIVE' && detail.approval?.status !== 'PENDING' && <form onSubmit={event => submit(event, async () => {
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
      {detail.status === 'ACTIVE' && <PartnerOperations key={detail.id} venues={detail.venues} request={request} />}
    </>}
  </section>;
}
