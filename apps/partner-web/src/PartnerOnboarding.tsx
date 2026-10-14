import { FormEvent, useEffect, useRef, useState } from 'react';
import { PartnerOperations } from './PartnerOperations';
import { MapTilerPlacePicker } from './MapTilerPlacePicker';
import { PartnerShell } from './layouts/PartnerShell';
import { PartnerOverview } from './features/workspace/PartnerOverview';
import { statusLabel, usePartnerNavigation } from './features/workspace/navigation';
import { PartnerApiError, type RequestOptions } from './features/bookings/types';
import { PartnerBookings } from './features/bookings/PartnerBookings';
import { PartnerNotifications, usePartnerNotifications } from './features/notifications/PartnerNotifications';
import { usePartnerToast } from './components/PartnerToast';
import { VenuePaymentSetup } from './components/VenuePaymentSetup';
import { AccountProfile } from '@shuttlebook/ui/account-profile';

type Row = { id: string; name: string; status: string };
type Court = { id: string; name: string; status: string; version: number; hours: object[]; prices: object[] };
type Venue = { id: string; name: string; status: string; version: number; address: string; contact: string;
  imageUploadId?: string;
  latitude: number; longitude: number; timezone: string;
  paymentAccount?: { accountName?: string; maskedAccountNumber: string }; courts: Court[] };
type Approval = { id?: string; kind?: string; status: string; reason?: string; venueId?: string; courtId?: string };
type Detail = Row & { version: number; legalName: string; contact: string;
  approval?: Approval; approvals?: Approval[]; venues: Venue[] };
type Envelope<T> = { data: T };
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
const defaultTimezone = 'Asia/Ho_Chi_Minh';
const days = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];

function courtNumberOf(name: string): string {
  const match = /^(?:Sân\s+)?([0-9]+)$/i.exec(name.trim());
  const number = match ? Number(match[1]) : 0;
  return Number.isInteger(number) && number > 0 && number <= 2147483647 ? String(number) : '';
}

function nextCourtNumber(courts: Array<{ name: string }>): string {
  const highest = Math.max(0, ...courts.map(court => Number(courtNumberOf(court.name)) || 0));
  return highest < 2147483647 ? String(highest + 1) : '';
}

function VenueImagePreview({ uploadId, name, request }: {
  uploadId: string; name: string; request: <T>(path: string, method?: string, body?: object, version?: number, options?: RequestOptions) => Promise<T>;
}) {
  const [src, setSrc] = useState('');
  const requestRef = useRef(request);
  requestRef.current = request;
  useEffect(() => {
    let current = true;
    let objectUrl = '';
    setSrc('');
    void requestRef.current<Blob>(`/uploads/${uploadId}/view`, 'GET', undefined, undefined, { blob: true })
      .then(blob => {
        if (!current || !blob.type.startsWith('image/')) return;
        objectUrl = URL.createObjectURL(blob);
        setSrc(objectUrl);
      }).catch(() => { if (current) setSrc(''); });
    return () => { current = false; if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [uploadId]);
  return src ? <img className="venue-image-preview" src={src} alt={`Ảnh cơ sở ${name}`} />
    : <p className="muted">Ảnh đã lưu nhưng chưa xem được bản xem trước.</p>;
}

export function PartnerOnboarding({ sendRequest, onLogout, logoutBusy }: {
  sendRequest: (path: string, options?: RequestInit) => Promise<Response>;
  onLogout: () => void; logoutBusy: boolean }) {
  const scheduleDirty = useRef(false);
  const [scheduleBusy, setScheduleBusy] = useState(false);
  const allowScheduleLeave = () => !scheduleBusy && (!scheduleDirty.current || window.confirm('Bạn có thay đổi chưa lưu. Bỏ thay đổi để chuyển mục?'));
  const { page, bookingId, visited, navigate, openBooking } = usePartnerNavigation(allowScheduleLeave);
  const [businesses, setBusinesses] = useState<Row[]>([]);
  const [selected, setSelected] = useState('');
  const [detail, setDetail] = useState<Detail | null>(null);
  const { showToast, clearToast } = usePartnerToast();
  const [busy, setBusy] = useState(false);
  const [bookingBusy, setBookingBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [stale, setStale] = useState(false);
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
  const [courtNumbers, setCourtNumbers] = useState<Record<string, string>>({});
  const [accountName, setAccountName] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [qrFile, setQrFile] = useState<File | null>(null);
  const [imageVenue, setImageVenue] = useState('');
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [revisionVenue, setRevisionVenue] = useState('');

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
      const friendly = ({ VALIDATION_FAILED: 'Thông tin chưa hợp lệ. Kiểm tra ngày, giờ và giá đã nhập.',
        PRICE_UNAVAILABLE: 'Khoảng giờ này chưa có giá đầy đủ hoặc nằm ngoài giờ mở. Kiểm tra lịch và bảng giá đã lưu.',
        SLOT_CONFLICT: 'Khoảng giờ bị trùng với đơn, báo giá đang giữ hoặc bảo trì. Chọn khoảng khác và tải lại lịch.',
        FORBIDDEN: 'Bạn không có quyền thực hiện thao tác này.', NOT_FOUND: 'Không tìm thấy mục này trong quyền quản lý của bạn.',
        UNAUTHORIZED: 'Phiên đăng nhập đã hết hiệu lực. Đăng nhập lại để tiếp tục.',
      } as Record<string, string>)[error.code ?? ''];
      failure.message = response.status === 412 ? 'Thông tin đã thay đổi. Tải lại hồ sơ trước khi lưu lại.'
        : error.code === 'STATE_CONFLICT' ? 'Mục này đang chờ duyệt hoặc doanh nghiệp đã có một đề nghị chờ duyệt. Tải lại hồ sơ để kiểm tra trạng thái.'
        : error.code === 'INCOMPLETE_PROFILE' ? 'Chưa đủ thông tin: kiểm tra cơ sở, QR và lịch/giá của sân trước khi xác nhận hoặc gửi duyệt.'
        : error.code === 'COURT_NUMBER_EXISTS' ? 'Số sân đã có trong cơ sở này. Chọn số khác hoặc tải lại danh sách.'
        : friendly ?? (error.code ? `Yêu cầu không thành công (${error.code}).` : 'Không thể kết nối API.');
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
      const routeBusiness = new URLSearchParams(window.location.hash.split('?')[1] ?? '').get('businessId');
      const target = id || selected || list.find(item => item.id === routeBusiness)?.id || list[0]?.id;
      const next = target ? await request<Detail>(`/partner-onboarding/businesses/${target}`) : null;
      if (target !== loadedBusiness.current) {
        setImageVenue('');
        setImageFile(null); setQrFile(null); setAccountName(''); setAccountNumber('');
        setVenueName(''); setVenueContact(''); setAddress(''); setLatitude(''); setLongitude('');
        setCreatePickerKey(value => value + 1);
        setRevisionVenue('');
        loadedBusiness.current = target ?? '';
      }
      setBusinesses(list); setSelected(target ?? ''); setDetail(next); setStale(false);
      setCourtNumbers(Object.fromEntries(next?.venues.flatMap(venue => venue.courts.map(court =>
        [court.id, courtNumberOf(court.name)])) ?? []));
    } catch (error) {
      if (error instanceof PartnerApiError && [401, 403, 404].includes(error.status)) {
        setDetail(null); setBusinesses([]); loadedBusiness.current = ''; scheduleDirty.current = false;
      }
      setLoadError(error instanceof Error ? error.message : 'Không tải được hồ sơ.'); throw error;
    }
    finally { setLoading(false); }
  }
  useEffect(() => {
    if (initialLoadStarted.current) return;
    initialLoadStarted.current = true;
    void load().catch(() => {});
  }, []);
  useEffect(() => {
    if (page !== 'venues' || !detail) return;
    const venueId = new URLSearchParams(window.location.hash.split('?')[1] ?? '').get('venueId');
    if (venueId && detail.venues.some(venue => venue.id === venueId)) {
      const frame = requestAnimationFrame(() => document.getElementById(`partner-venue-${venueId}`)?.scrollIntoView({ block: 'start' }));
      return () => cancelAnimationFrame(frame);
    }
  }, [page, detail?.id]);
  async function run(work: () => Promise<void>, successMessage = 'Đã lưu.') {
    setBusy(true); clearToast();
    try { await work(); showToast(successMessage, 'success'); return true; }
    catch (error) {
      if (error instanceof PartnerApiError && error.status === 412) setStale(true);
      showToast(error instanceof Error ? error.message : 'Có lỗi xảy ra.', 'error');
      return false;
    }
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

  const editableBusiness = detail?.status === 'DRAFT' || detail?.status === 'ACTIVE';
  const draftVenue = (venue: Venue) => editableBusiness && venue.status === 'DRAFT';
  const publishedVenue = (venue: Venue) => detail?.status === 'ACTIVE' && venue.status === 'PUBLISHED';
  const draftCourt = (venue: Venue, court: Court) => editableBusiness && court.status === 'INACTIVE'
    && (draftVenue(venue) || publishedVenue(venue));
  const editableCourt = (venue: Venue, court: Court) => draftCourt(venue, court)
    || (publishedVenue(venue) && court.status === 'ACTIVE');
  const approvals = detail?.approvals ?? (detail?.approval ? [detail.approval] : []);
  const pendingApproval = approvals.some(approval => approval.status === 'PENDING');
  const itemApproval = (venueId: string, courtId?: string) => approvals.find(approval => courtId
    ? approval.courtId === courtId : approval.venueId === venueId && !approval.courtId);
  const courtNumbersChanged = (venue: Venue) => venue.courts.some(court =>
    (courtNumbers[court.id] ?? courtNumberOf(court.name)) !== courtNumberOf(court.name));

  return <PartnerShell page={page} navigate={navigate} unread={notifications.unread}
    onLogout={() => { if (allowScheduleLeave()) onLogout(); }} logoutBusy={logoutBusy || scheduleBusy} businessControl={
      <label className="business-switcher">Doanh nghiệp <select value={selected} disabled={busy || loading || bookingBusy || scheduleBusy}
        onChange={event => { if (!allowScheduleLeave()) return; if (page === 'bookings') openBooking(null); void run(() => load(event.target.value)); }}>
        {businesses.length === 0 && <option value="">Chưa có hồ sơ</option>}
        {businesses.map(b => <option key={b.id} value={b.id}>{b.name} · {statusLabel(b.status)}</option>)}
      </select></label>}>
    <h2 className="sr-only">Hồ sơ chủ sân</h2>
    {busy && <p className="feedback" role="status">Đang xử lý…</p>}
    {loading && <p className="feedback" role="status">Đang tải hồ sơ…</p>}
    {loadError && <div className="feedback error" role="alert">{loadError}<button type="button" disabled={loading}
      onClick={() => void load().catch(() => {})}>Thử tải lại hồ sơ</button></div>}
    {stale && <div className="feedback error" role="alert">Thông tin đã thay đổi. Tải lại hồ sơ trước khi tiếp tục.
      <button type="button" disabled={busy || loading} onClick={() => void load().catch(() => {})}>Tải lại để tiếp tục</button></div>}
    {page === 'notifications' && <PartnerNotifications model={notifications} openBooking={openBooking} openProfile={businessId => {
      if (businessId && businesses.some(item => item.id === businessId) && businessId !== selected) void load(businessId).catch(() => {});
      navigate('profile');
    }} />}
    {page === 'account' && <AccountProfile request={sendRequest} />}
    {page === 'bookings' && !loading && detail && (detail.status === 'ACTIVE'
      ? <PartnerBookings key={`bookings-${detail.id}`} businessId={detail.id} venues={detail.venues} request={request} bookingId={bookingId}
          openBooking={openBooking} onActionBusy={setBookingBusy} selectBusiness={id => {
            if (!businesses.some(item => item.id === id)) return false;
            void load(id).catch(() => {}); return true;
          }} />
      : <section className="panel empty-state"><h3>Chưa thể xử lý đơn đặt sân</h3><p>Doanh nghiệp cần được duyệt và đang hoạt động để đối chiếu thanh toán.</p></section>)}
    <fieldset className="partner-onboarding" hidden={page === 'account'} disabled={busy || loading || stale} key={detail?.id ?? 'new'}>
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
        <button type="button" disabled={busy || loading || scheduleBusy} onClick={() => {
          if (allowScheduleLeave()) void load(detail.id).catch(() => {});
        }}>Tải lại hồ sơ</button>
      </div>
      {detail.approval?.status === 'CHANGES_REQUESTED' && !detail.approval.venueId && !detail.approval.courtId && <p className="feedback error">Admin yêu cầu bổ sung: {detail.approval.reason}</p>}
      {detail.status === 'PENDING_APPROVAL' && <p className="feedback">Hồ sơ đang chờ Admin duyệt. Bạn có thể xem thông tin và theo dõi phản hồi trong mục Thông báo.</p>}
      {detail.status === 'ACTIVE' && detail.approval?.status === 'PENDING' && detail.approval.kind !== 'COURT_ADDITION' && <p className="feedback">Đề nghị thay đổi đang chờ Admin duyệt. Thông tin đang hoạt động tiếp tục được sử dụng cho đến khi thay đổi được duyệt.</p>}
      {page === 'overview' && <PartnerOverview name={detail.name} status={detail.status} venues={detail.venues} navigate={navigate} />}
      {page === 'profile' && detail.status !== 'DRAFT' && <section className="panel"><h3>Thông tin doanh nghiệp</h3><p>Tên pháp lý: {detail.legalName}</p><p>Liên hệ: {detail.contact}</p></section>}
      {page === 'venues' && detail.status === 'ACTIVE' && <section className="panel" role="note">
        <h3>Thêm cơ sở hoặc sân sau khi duyệt</h3>
        <p>Cơ sở mới cần Admin duyệt. Với cơ sở đang công bố, thêm sân nháp, thiết lập lịch và giá rồi bấm Xác nhận để khách có thể đặt ngay.</p>
        <p>Ảnh và số sân có thể đổi trực tiếp. Lịch và giá cho đơn mới được quản lý ở Lịch & giá.</p>
      </section>}
      {page === 'profile' && detail.status === 'ACTIVE' && !pendingApproval && <form key={`revision-${detail.version}`} onSubmit={event => submit(event, async () => {
        const fields = new FormData(event.currentTarget);
        await request(`/partner-onboarding/businesses/${detail.id}/revisions`, 'POST', {
          name: String(fields.get('name') ?? ''), legalName: String(fields.get('legalName') ?? ''), contact: String(fields.get('contact') ?? '') }, detail.version);
        await load(detail.id);
      })}>
        <h3>Đề nghị thay đổi thông tin doanh nghiệp</h3>
        <p>Thông tin đang công bố được giữ đến khi Admin duyệt đề nghị này.</p>
        <label>Tên <input name="name" required defaultValue={detail.name} /></label>
        <label>Tên pháp lý <input name="legalName" required defaultValue={detail.legalName} /></label>
        <label>Liên hệ <input name="contact" required defaultValue={detail.contact} /></label>
        <button disabled={busy}>Gửi duyệt</button>
      </form>}
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
      {visited.has('venues') && editableBusiness && <form hidden={page !== 'venues'} onSubmit={event => submit(event, async () => {
        if (!address || !latitude || !longitude) throw new Error('Chọn và xác nhận địa chỉ trên MapTiler.');
        await request(`/partner-onboarding/businesses/${detail.id}/venues`, 'POST',
          { name: venueName, address, contact: venueContact, timezone: defaultTimezone,
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
        <button disabled={busy}>Lưu cơ sở</button>
      </form>}
      {detail.venues.map(v => <article className="venue-detail-card" id={`partner-venue-${v.id}`} hidden={!['venues', 'courts', 'media', 'payments'].includes(page)} key={v.id}>
        <h3>{v.name} · {statusLabel(v.status)}</h3><p>{v.address} · {v.contact}</p>
        <p>Ảnh: {v.imageUploadId ? 'Đã tải' : 'Chưa có'} · QR: {v.paymentAccount ? 'Đã khai báo' : 'Chưa có'}</p>
        {page === 'venues' && v.imageUploadId && <VenueImagePreview key={`${v.id}-${v.imageUploadId}`} uploadId={v.imageUploadId} name={v.name} request={request} />}
        {page === 'venues' && v.imageUploadId && (draftVenue(v) || publishedVenue(v)) && <button type="button" disabled={busy}
          aria-label={`Gỡ ảnh cơ sở ${v.name}`} onClick={() => void run(async () => {
            await request(`/operator/venues/${v.id}/image`, 'DELETE', undefined, v.version); await load(detail.id);
          })}>Gỡ ảnh</button>}
        {page === 'payments' && v.paymentAccount && <p>Số tài khoản: {v.paymentAccount.maskedAccountNumber}</p>}
        {itemApproval(v.id)?.status === 'CHANGES_REQUESTED' && <p className="feedback error">Admin yêu cầu bổ sung cơ sở: {itemApproval(v.id)?.reason}</p>}
        {v.status === 'PENDING_APPROVAL' && <p className="feedback">Cơ sở đang chờ duyệt; khách chưa thể thấy và đặt sân tại đây.</p>}
        {visited.has('venues') && draftVenue(v) && <form hidden={page !== 'venues'} key={v.version} onSubmit={event => submit(event, async () => {
          const fields = new FormData(event.currentTarget);
          if (!fields.get('address') || !fields.get('latitude') || !fields.get('longitude'))
            throw new Error('Chọn và xác nhận địa chỉ trên MapTiler.');
          await request(`/partner-onboarding/venues/${v.id}`, 'PUT', {
            name: String(fields.get('name') ?? ''), address: String(fields.get('address') ?? ''),
            contact: String(fields.get('contact') ?? ''),
            timezone: v.timezone, latitude: Number(fields.get('latitude')),
            longitude: Number(fields.get('longitude')) }, v.version);
          await load(detail.id);
        })}>
          <label>Tên <input name="name" required defaultValue={v.name} /></label>
          <MapTilerPlacePicker initial={{ address: v.address, latitude: v.latitude, longitude: v.longitude }} />
          <label>Liên hệ cơ sở <input name="contact" required defaultValue={v.contact} /></label>
          <button disabled={busy}>Sửa cơ sở</button>
        </form>}
        {page === 'venues' && publishedVenue(v) && !pendingApproval && <form className="venue-contact-form" key={`contact-${v.id}-${v.version}`}
          onSubmit={event => submit(event, async () => {
            const fields = new FormData(event.currentTarget);
            await request(`/partner-onboarding/venues/${v.id}/contact`, 'PUT',
              { contact: String(fields.get('contact') ?? '') }, v.version);
            await load(detail.id);
          })}>
          <h4>Liên hệ cơ sở</h4>
          <label>Điện thoại hoặc thông tin liên hệ <input name="contact" required minLength={3} maxLength={160} defaultValue={v.contact} /></label>
          <button disabled={busy}>Lưu liên hệ</button>
        </form>}
        {draftVenue(v) && <div hidden={page !== 'venues'}><VenuePaymentSetup venueId={v.id} venueName={v.name}
          account={v.paymentAccount} busy={busy || loading} save={fields => run(async () => {
            const qrUploadId = await upload(fields.file, v.id, 'QR');
            await request(`/partner-onboarding/venues/${v.id}/payment-account`, 'PUT', {
              accountName: fields.accountName, accountNumber: fields.accountNumber, qrUploadId });
            await load(detail.id);
          }, 'Đã lưu QR dùng chung cho cơ sở.')} /></div>}
        <section className="venue-courts-block" hidden={page !== 'venues' && page !== 'courts'} aria-label={`Các sân tại ${v.name}`}>
        <h4>Sân trong cơ sở</h4>
        {!v.courts.length && <p className="muted">Chưa có sân. Thêm sân rồi thiết lập giờ và giá trước khi gửi duyệt.</p>}
        <div className="court-grid">{[...v.courts].sort((a, b) => a.name.localeCompare(b.name, 'vi', { numeric: true })).map(c => <div className="court-item" key={c.id}>
          <p>{c.name}: {c.hours.length} ngày mở cửa, {c.prices.length} khung giá · {statusLabel(c.status)}</p>
          {c.status === 'PENDING_APPROVAL' && <p className="feedback">{publishedVenue(v)
            ? 'Lưu lại giờ và giá để công bố sân theo quy trình mới.'
            : 'Sân thuộc hồ sơ cơ sở đang chờ duyệt; khách chưa thể đặt sân này.'}</p>}
          {publishedVenue(v) && c.status === 'INACTIVE' && <p className="muted">Lưu giờ và giá hợp lệ để sân tự công bố. QR dùng chung của cơ sở.</p>}
          {editableCourt(v, c) && <div className="court-action-form">
            {!courtNumberOf(c.name) && <p className="muted">Tên hiện tại: {c.name}. Nhập số nếu muốn đổi sang cách gọi Sân 1, Sân 2…</p>}
            <label>Số sân <input aria-label={`Số sân ${c.name} tại ${v.name}`} type="number" min="1" max="2147483647" step="1"
              value={courtNumbers[c.id] ?? courtNumberOf(c.name)} onChange={event => setCourtNumbers(previous => ({ ...previous, [c.id]: event.target.value }))} /></label>
          </div>}
          <a className="court-setup-link" href={`#/schedule?${new URLSearchParams({ tab: 'pricing', businessId: detail.id, venueId: v.id, courtId: c.id })}`}>
            {editableCourt(v, c) || publishedVenue(v) && c.status === 'PENDING_APPROVAL' ? 'Thiết lập giờ & giá' : 'Xem giờ & giá'} →</a>
        </div>)}</div>
        {page === 'venues' && (draftVenue(v) || publishedVenue(v)) && <div className="court-batch-actions">
          <button type="button" disabled={busy || !v.courts.length || !v.courts.every(c => editableCourt(v, c)) || !courtNumbersChanged(v)} onClick={() => void run(async () => {
            const changes = v.courts.map(c => ({ id: c.id, version: c.version, number: Number(courtNumbers[c.id] ?? courtNumberOf(c.name)) }));
            if (changes.some(c => !Number.isInteger(c.number) || c.number < 1 || c.number > 2147483647) ||
              new Set(changes.map(c => c.number)).size !== changes.length)
              throw new Error('Mỗi sân cần số nguyên dương riêng trong cơ sở.');
            await request(`/partner-onboarding/venues/${v.id}/court-numbers`, 'PUT', { courts: changes });
            await load(detail.id);
          }, 'Đã lưu tất cả số sân trong cơ sở.')}>Lưu tất cả số sân</button>
          <button type="button" className="secondary" disabled={busy || courtNumbersChanged(v) || !nextCourtNumber(v.courts)}
            aria-label={`Thêm sân tại ${v.name}`} onClick={() => void run(async () => {
              const number = nextCourtNumber(v.courts);
              if (!number) throw new Error('Số sân đã đạt giới hạn.');
              await request(`/partner-onboarding/venues/${v.id}/courts`, 'POST', { name: `Sân ${number}` });
              await load(detail.id);
            }, draftVenue(v) ? 'Đã thêm sân. Thiết lập giờ và giá trước khi gửi duyệt.'
              : 'Đã thêm sân. Lưu giờ và giá để sân tự công bố.')}>Thêm sân</button>
          {courtNumbersChanged(v) && <small>Lưu tất cả số sân trước khi thêm sân hoặc gửi duyệt.</small>}
          {v.courts.some(c => !editableCourt(v, c)) && <small>Sân đang chờ duyệt chỉ đổi số sau khi được công bố.</small>}
        </div>}
        </section>
        {page === 'venues' && draftVenue(v) && <div className="venue-submit-row">
          <p>Trước khi gửi duyệt: lưu QR nhận tiền, thêm sân và thiết lập giờ/giá. Ảnh cơ sở là tùy chọn.</p>
          {detail.status === 'ACTIVE' ? <button type="button" disabled={busy || pendingApproval || courtNumbersChanged(v)}
            aria-label={`Gửi duyệt cơ sở ${v.name}`} onClick={() => void run(async () => {
              await request(`/partner-onboarding/venues/${v.id}/submit`, 'POST'); await load(detail.id);
            })}>Gửi duyệt cơ sở</button> : <span className="muted">Nút gửi hồ sơ doanh nghiệp ở cuối trang.</span>}
        </div>}
      </article>)}
      {editableBusiness && detail.venues.length > 0 && <>
        <form hidden={page !== 'venues' && page !== 'media'} onSubmit={event => submit(event, async () => {
          if (!imageFile) throw new Error('Chọn ảnh cơ sở.');
          const venue = detail.venues.find(v => v.id === imageVenue);
          if (!venue) throw new Error('Chọn cơ sở.');
          const uploadId = await upload(imageFile, imageVenue, 'VENUE_IMAGE');
          await request(`/operator/venues/${imageVenue}/image`, 'PUT', { uploadId }, venue.version);
          setImageFile(null); await load(detail.id);
        })}>
          <h3>Ảnh cơ sở</h3>
          <p>Ảnh cơ sở là tùy chọn. PNG, JPEG hoặc WebP, tối đa 5 MB; thêm, thay hoặc gỡ ảnh không cần Admin duyệt.</p>
          <label>Cơ sở <select required value={imageVenue} onChange={e => setImageVenue(e.target.value)}><option value="">Chọn</option>
            {detail.venues.filter(v => draftVenue(v) || publishedVenue(v)).map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
          <input aria-label="Ảnh cơ sở" required type="file" accept="image/png,image/jpeg,image/webp" onChange={e => setImageFile(e.target.files?.[0] ?? null)} />
          <button disabled={busy}>Tải ảnh</button>
        </form>
      </>}
      {page === 'venues' && detail.status === 'DRAFT' && <section className="panel venue-final-submit">
        <h3>Hoàn tất hồ sơ doanh nghiệp</h3>
        <p>Kiểm tra từng cơ sở: thông tin liên hệ, QR nhận tiền, số sân, lịch mở cửa và giá. Ảnh cơ sở là tùy chọn.</p>
        <button type="button" className="primary-action" disabled={busy || detail.venues.some(courtNumbersChanged)}
          onClick={() => void run(async () => {
            await request(`/partner-onboarding/businesses/${detail.id}/submit`, 'POST'); await load(detail.id);
          })}>Gửi hồ sơ duyệt</button>
      </section>}
      {detail.venues.length === 0 && ['courts', 'schedule', 'media', 'payments'].includes(page) && <section className="panel empty-state">
        <h3>Thêm cơ sở trước khi cấu hình</h3><p>Sân, ảnh và tài khoản nhận tiền đều thuộc một cơ sở.</p>
        <a className="button secondary" href="#/venues" onClick={() => navigate('venues')}>Đến mục Cơ sở</a>
      </section>}
      {page === 'payments' && <div className="panel"><h3>Thay đổi tài khoản & QR</h3>
        <p>Mỗi cơ sở dùng một QR chung cho tất cả sân. QR ban đầu được thiết lập tại Cơ sở & sân trước khi gửi duyệt.</p>
        <a className="button secondary" href="#/venues">Thiết lập cơ sở</a>
        {pendingApproval && <p className="feedback">Đang có hồ sơ chờ Admin duyệt. Tài khoản/QR hiện hành tiếp tục được dùng; chờ kết quả trước khi gửi thay đổi mới.</p>}
      </div>}
      {visited.has('payments') && detail.status === 'ACTIVE' && detail.venues.some(publishedVenue) && !pendingApproval && <form className="payment-revision-form" hidden={page !== 'payments'} onSubmit={event => submit(event, async () => {
        if (!detail.venues.some(venue => venue.id === revisionVenue && publishedVenue(venue)))
          throw new Error('Chọn cơ sở đã công bố.');
        if (!qrFile) throw new Error('Chọn QR mới.');
        const qrUploadId = await upload(qrFile, revisionVenue, 'QR');
        await request(`/partner-onboarding/venues/${revisionVenue}/payment-account/revisions`, 'POST', {
          accountName, accountNumber, qrUploadId });
        setAccountNumber(''); setQrFile(null); await load(detail.id);
      })}>
        <h3>Đề nghị đổi tài khoản hoặc QR</h3>
        <p>Tải QR mới và khai báo tài khoản nhận tiền. Gửi đề nghị để Admin duyệt; đơn đã tạo vẫn giữ QR của đơn.</p>
        <label>Cơ sở <select required value={revisionVenue} onChange={e => {
          const venue = detail.venues.find(v => v.id === e.target.value);
          setRevisionVenue(e.target.value);
          setAccountName(venue?.paymentAccount?.accountName ?? ''); setAccountNumber(''); setQrFile(null);
        }}><option value="">Chọn</option>{detail.venues.filter(publishedVenue).map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
        {revisionVenue && <p className="muted">QR đề nghị áp dụng cho mọi sân tại {detail.venues.find(v => v.id === revisionVenue)?.name}.</p>}
        <label>Tên tài khoản <input required value={accountName} onChange={e => setAccountName(e.target.value)} /></label>
        <label>Số tài khoản <input required inputMode="numeric" value={accountNumber} onChange={e => setAccountNumber(e.target.value)} /></label>
        <input key={revisionVenue} aria-label="QR mới" required type="file" accept="image/png,image/jpeg,image/webp" onChange={e => setQrFile(e.target.files?.[0] ?? null)} />
        <button disabled={busy}>Gửi Admin duyệt</button>
      </form>}
      {page === 'schedule' && <PartnerOperations key={detail.id} venues={detail.venues} request={request}
        active={detail.status === 'ACTIVE'} canEditDraft={detail.status === 'DRAFT' || detail.status === 'ACTIVE'} refresh={() => load(detail.id)} openBooking={openBooking}
        onDirtyChange={value => { scheduleDirty.current = value; }} onBusyChange={setScheduleBusy} />}

    </>}
    </fieldset>
  </PartnerShell>;
}
