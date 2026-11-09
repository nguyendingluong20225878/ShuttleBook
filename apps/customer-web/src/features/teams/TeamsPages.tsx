import { FormEvent, useEffect, useRef, useState } from 'react';
import { CustomerShell } from '../../components/CustomerShell';
import { CustomerFeatureHero } from '../../components/CustomerFeatureHero';
import { useCustomerSession } from '../auth/CustomerSession';
import { data, type Booking } from '../bookings/bookingApi';
import { navigate } from '../../routes/navigation';
import './teams.css';

type City = 'HN' | 'HCM';
type Level = 'ANY' | 'NEWBIE' | 'Y_MINUS' | 'Y' | 'Y_PLUS' | 'TBY_MINUS' | 'TBY' | 'TBY_PLUS'
  | 'TB_MINUS' | 'TB' | 'TB_PLUS' | 'TBK_MINUS' | 'TBK' | 'TBK_PLUS'
  | 'BEGINNER' | 'INTERMEDIATE' | 'ADVANCED';
type ApplicantLevel = 'UNSPECIFIED' | 'BEGINNER' | 'INTERMEDIATE' | 'ADVANCED';
type Post = { id: string; venueId: string; venueName: string; address: string; cityCode: City;
  courtName: string; date: string; startsAt: string; endsAt: string; timezone: string; slotsNeeded: number;
  slotsOpen: number; level: Level; status: string; version: number; createdAt: string; distanceMeters: number | null;
  caption?: string; contactName?: string; contactPhone?: string | null; phoneVisibility?: 'HIDDEN' | 'ACCEPTED' | 'PUBLIC';
  contributionMode?: 'UNSPECIFIED' | 'COMMON' | 'BY_GENDER'; commonContributionVnd?: number | null;
  maleContributionVnd?: number | null; femaleContributionVnd?: number | null; imageUrl?: string | null;
  acceptedCount?: number | null; pendingCount?: number | null; shareable?: boolean };
type Request = { id: string; postId: string; level: ApplicantLevel; message?: string; status: string; version: number; createdAt: string };
type OwnerRequest = { id: string; level: ApplicantLevel; message?: string; status: string; version: number; createdAt: string; displayName: string; contactPhone?: string | null };
type PersonProfile = { name: string; phone?: string | null; level?: ApplicantLevel };
type VenueContact = { contact: string };
type Page<T> = { items: T[]; nextCursor?: string | null };
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
const cityKey = 'shuttlebook.customer.city.v1';
const levelName: Record<Level, string> = {
  ANY: 'Không yêu cầu trình độ', NEWBIE: 'Newbie', Y_MINUS: 'Y-', Y: 'Y', Y_PLUS: 'Y+',
  TBY_MINUS: 'TBY-', TBY: 'TBY', TBY_PLUS: 'TBY+', TB_MINUS: 'TB-', TB: 'TB', TB_PLUS: 'TB+',
  TBK_MINUS: 'TBK-', TBK: 'TBK', TBK_PLUS: 'TBK+',
  BEGINNER: 'Newbie', INTERMEDIATE: 'TB', ADVANCED: 'TBK'
};
const levelOptions: Level[] = ['ANY', 'NEWBIE', 'Y_MINUS', 'Y', 'Y_PLUS', 'TBY_MINUS', 'TBY', 'TBY_PLUS',
  'TB_MINUS', 'TB', 'TB_PLUS', 'TBK_MINUS', 'TBK', 'TBK_PLUS'];
const applicantLevelName: Record<ApplicantLevel, string> = {
  UNSPECIFIED: '', BEGINNER: 'Newbie', INTERMEDIATE: 'TB', ADVANCED: 'TBK'
};
const statusName: Record<string, string> = { PENDING: 'Đang chờ chủ lịch', ACCEPTED: 'Đã được nhận', DECLINED: 'Chưa được nhận',
  WITHDRAWN: 'Đã rút', OPEN: 'Đang tìm người', CLOSED: 'Đã đóng', HIDDEN: 'Đã ẩn' };
const requestRank: Record<string, number> = { ACCEPTED: 0, PENDING: 1, DECLINED: 2, WITHDRAWN: 3 };
function city(value: string | null): City | null { return value === 'HN' || value === 'HCM' ? value : null; }
function storedCity(): City | null { try { return city(localStorage.getItem(cityKey)); } catch { return null; } }
function errorText(reason: unknown) {
  const key = reason instanceof Error ? reason.message : '';
  const messages: Record<string, string> = {
    BOOKING_NOT_ELIGIBLE: 'Đơn chưa đủ điều kiện đăng bài. Cần đơn vãng lai đã xác nhận, còn trước giờ chơi và cơ sở đã công bố trong Hà Nội/TP.HCM.',
    POST_CLOSED: 'Bài đã đóng hoặc lịch chơi không còn hợp lệ. Hãy làm mới trang.',
    CAPACITY_REACHED: 'Lịch chơi vừa đủ người. Hãy tìm lịch khác.',
    CAPACITY_BELOW_ACCEPTED: 'Số người cần không thể ít hơn số người đã nhận.',
    REQUEST_EXISTS: 'Bạn đã gửi yêu cầu cho bài này hoặc bài đã tồn tại.',
    LIMIT_REACHED: 'Bạn đã đạt giới hạn bài hoặc yêu cầu đang hoạt động.',
    STATE_CONFLICT: 'Trạng thái vừa thay đổi. Hãy tải lại trước khi tiếp tục.',
    UNAUTHORIZED: 'Phiên đã hết hạn. Hãy đăng nhập lại.', FORBIDDEN: 'Bạn không có quyền thực hiện thao tác này.',
    NOT_FOUND: 'Bài hoặc yêu cầu không còn hiển thị trong quyền của bạn.',
    RATE_LIMITED: 'Thao tác quá nhanh. Vui lòng thử lại sau.',
    VALIDATION_FAILED: 'Thông tin chưa hợp lệ. Hãy kiểm tra lại.'
  };
  return messages[key] ?? 'Không thể kết nối máy chủ. Vui lòng thử lại.';
}
async function publicData<T>(path: string, signal?: AbortSignal): Promise<T> {
  return data<T>(await fetch(`${base}/api/v1${path}`, { signal }));
}
async function searchPosts(cityCode: City, date: string, level: string, term: string, sort: string,
  origin: { latitude: number; longitude: number } | null, radius: string, cursor: string | null, signal: AbortSignal) {
  if (origin) return data<Page<Post>>(await fetch(`${base}/api/v1/recruitment-posts/search`, {
    method: 'POST', signal, headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ city: cityCode, date: date || null, level, latitude: origin.latitude,
      longitude: origin.longitude, radiusMeters: Number(radius), limit: 20, cursor, q: term || null, sort })
  }));
  const params = new URLSearchParams({ city: cityCode });
  if (date) params.set('date', date);
  if (level !== 'ANY') params.set('level', level);
  if (term) params.set('q', term);
  if (sort !== 'NEWEST') params.set('sort', sort);
  if (cursor) params.set('cursor', cursor);
  return publicData<Page<Post>>(`/recruitment-posts?${params}`, signal);
}
const when = (p: Post) => `${p.date.split('-').reverse().join('/')} · ${p.startsAt}–${p.endsAt}`;
const vnd = (value: number) => `${new Intl.NumberFormat('vi-VN').format(value)}đ`;
function contributionText(post: Post) {
  if (post.contributionMode === 'COMMON' && post.commonContributionVnd != null)
    return post.commonContributionVnd === 0 ? 'Miễn phí theo bài đăng' : `Mức góp: ${vnd(post.commonContributionVnd)}/người`;
  if (post.contributionMode === 'BY_GENDER' && post.maleContributionVnd != null && post.femaleContributionVnd != null)
    return `Mức góp: Nam ${vnd(post.maleContributionVnd)} · Nữ ${vnd(post.femaleContributionVnd)}`;
  return null;
}
const postedAtFormatter = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit',
  hour: '2-digit', minute: '2-digit', hour12: false,
});
function postedAtLabel(value: string) {
  const parts = postedAtFormatter.formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? '';
  return `${part('day')}/${part('month')} · ${part('hour')}:${part('minute')}`;
}
function TeamImage({ post }: { post: Post }) {
  const [broken, setBroken] = useState(false);
  useEffect(() => setBroken(false), [post.imageUrl]);
  if (!post.imageUrl || broken) return null;
  return <img className="team-post-image" src={`${base}${post.imageUrl}`} width={960} height={540}
    alt={`Ảnh bài tìm người tại ${post.venueName}`} loading="lazy" onError={() => setBroken(true)} />;
}
function ProfileAvatar({ name, onClick }: { name: string; onClick: () => void }) {
  return <button className="team-avatar-button" type="button" aria-label={`Xem hồ sơ ${name}`} onClick={onClick}>
    <span className="team-avatar" aria-hidden="true">{name.trim().charAt(0).toUpperCase() || 'N'}</span>
  </button>;
}
function PersonProfileDialog({ person, onClose }: { person: PersonProfile; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { const node = dialog.current; node?.showModal(); return () => { if (node?.open) node.close(); }; }, []);
  return <dialog ref={dialog} className="team-dialog team-profile-dialog"
    onCancel={event => { event.preventDefault(); onClose(); }}
    onClick={event => { if (event.target === dialog.current) onClose(); }}
    aria-label={`Hồ sơ ${person.name}`}>
    <div className="team-dialog-head"><h2>Hồ sơ người chơi</h2><button type="button" onClick={onClose} aria-label="Đóng hồ sơ">✕</button></div>
    <div className="team-profile-main"><span className="team-avatar" aria-hidden="true">{person.name.trim().charAt(0).toUpperCase() || 'N'}</span>
      <strong>{person.name}</strong></div>
    {person.level && person.level !== 'UNSPECIFIED' && <p>Trình độ: {applicantLevelName[person.level]} (tự khai báo trước đây)</p>}
    <p>{person.phone ? <>Số điện thoại (tự khai báo): <a href={`tel:${person.phone}`}>{person.phone}</a></> : 'Số điện thoại chưa được chia sẻ.'}</p>
  </dialog>;
}
function AcceptedGameDialog({ post, onClose }: { post: Post; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [venue, setVenue] = useState<VenueContact | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  useEffect(() => { const node = dialog.current; node?.showModal(); return () => { if (node?.open) node.close(); }; }, []);
  useEffect(() => {
    const controller = new AbortController();
    void publicData<VenueContact>(`/venues/${encodeURIComponent(post.venueId)}`, controller.signal)
      .then(result => { if (!controller.signal.aborted) setVenue(result); })
      .catch(() => { if (!controller.signal.aborted) setError(true); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [post.venueId]);
  return <dialog ref={dialog} className="team-dialog" aria-labelledby="team-game-title"
    onCancel={event => { event.preventDefault(); onClose(); }}>
    <div className="team-dialog-head"><h2 id="team-game-title">Thông tin lịch chơi</h2>
      <button type="button" onClick={onClose} aria-label="Đóng thông tin lịch chơi">✕</button></div>
    <dl className="team-game-details"><div><dt>Cơ sở</dt><dd>{post.venueName}</dd></div>
      <div><dt>Sân và giờ chơi</dt><dd>{post.courtName} · {when(post)}</dd></div>
      <div><dt>Địa chỉ</dt><dd>{post.address}</dd></div>
      <div><dt>Người tổ chức</dt><dd>{post.contactName || 'Người tổ chức'}{post.contactPhone && <> · <a href={`tel:${post.contactPhone}`}>{post.contactPhone}</a></>}</dd></div>
      <div><dt>Liên hệ cơ sở</dt><dd>{loading ? 'Đang tải…' : error ? 'Chưa tải được thông tin cơ sở.' : venue?.contact || 'Cơ sở chưa cung cấp liên hệ công khai.'}</dd></div></dl>
    <a href={`/venues/${encodeURIComponent(post.venueId)}`}>Xem chi tiết cơ sở và sân</a>
  </dialog>;
}
function PostCard({ post }: { post: Post }) {
  const [profileOpen, setProfileOpen] = useState(false);
  const name = post.contactName || 'Người tổ chức';
  return <article className={`team-card team-listing-card${post.imageUrl ? '' : ' team-listing-card--text'}`}>
    {post.imageUrl && <div className="team-listing-visual"><TeamImage post={post} /></div>}
    <div className="team-listing-content"><div className="team-listing-meta"><span className="team-pill">Còn {post.slotsOpen} suất</span><span className="team-pill team-pill--soft">{levelName[post.level]}</span></div>
      <h2><a href={`/teams/${post.id}`}>{post.venueName}</a></h2>
      <p className="team-card-when">{when(post)}</p>
      <p className="team-card-court">{post.courtName}</p>
      <p className="team-address">{post.address}</p>
      {contributionText(post) && <p className="team-contribution">{contributionText(post)}</p>}
      {post.caption && <p className="team-caption">{post.caption}</p>}
      {post.contactPhone && <a className="team-public-phone" href={`tel:${post.contactPhone}`} aria-label={`Gọi người đăng ${post.contactPhone}`}>☎ {post.contactPhone}</a>}
      <div className="team-listing-foot"><button type="button" className="team-card-author team-author-open" onClick={() => setProfileOpen(true)} aria-label={`Xem hồ sơ ${name}`}>
        <span className="team-avatar" aria-hidden="true">{name.trim().charAt(0).toUpperCase() || 'N'}</span><span><strong>{name}</strong><small><time dateTime={post.createdAt}>Đăng {postedAtLabel(post.createdAt)}</time>{post.distanceMeters != null && ` · ${(post.distanceMeters / 1000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} km đến sân`}</small></span></button>
        <a className="team-link" href={`/teams/${post.id}`} aria-label={`Xem bài tìm người tại ${post.venueName}`}>Xem bài <span aria-hidden="true">→</span></a></div>
    </div>
    {profileOpen && <PersonProfileDialog person={{ name, phone: post.contactPhone }} onClose={() => setProfileOpen(false)} />}
  </article>;
}

export function TeamExplore() {
  const query = new URLSearchParams(location.search);
  const initialCity = city(query.get('city')) ?? storedCity();
  const [selectedCity, setCity] = useState<City | null>(initialCity);
  const [date, setDate] = useState(query.get('date') ?? '');
  const [level, setLevel] = useState<Level>((query.get('level') as Level) in levelName ? query.get('level') as Level : 'ANY');
  const [term, setTerm] = useState(query.get('q') ?? '');
  const [sort, setSort] = useState(query.get('sort') === 'SOONEST' ? 'SOONEST' : 'NEWEST');
  const [origin, setOrigin] = useState<{ latitude: number; longitude: number; label: string } | null>(null);
  const [place, setPlace] = useState(''); const [radius, setRadius] = useState('5000');
  const [locating, setLocating] = useState(false); const [items, setItems] = useState<Post[]>([]);
  const [cursor, setCursor] = useState<string | null>(null); const [loading, setLoading] = useState(false);
  const [error, setError] = useState(''); const [retry, setRetry] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const previousAppliedCity = useRef<City | null>(initialCity);
  const key = import.meta.env.VITE_MAPTILER_API_KEY as string | undefined;
  const appliedCity = city(new URLSearchParams(location.search).get('city')) ?? initialCity;
  const appliedDate = new URLSearchParams(location.search).get('date') ?? '';
  const requestedLevel = new URLSearchParams(location.search).get('level') ?? 'ANY';
  const appliedLevel: Level = requestedLevel in levelName ? requestedLevel as Level : 'ANY';
  const appliedTerm = new URLSearchParams(location.search).get('q') ?? '';
  const appliedSort = new URLSearchParams(location.search).get('sort') === 'SOONEST' ? 'SOONEST' : 'NEWEST';
  useEffect(() => {
    if (previousAppliedCity.current !== appliedCity) { setOrigin(null); setPlace(''); }
    previousAppliedCity.current = appliedCity;
  }, [appliedCity]);
  useEffect(() => {
    setCity(appliedCity); setDate(appliedDate);
    setLevel(appliedLevel in levelName ? appliedLevel as Level : 'ANY');
    setTerm(appliedTerm); setSort(appliedSort);
  }, [appliedCity, appliedDate, appliedLevel, appliedTerm, appliedSort]);
  useEffect(() => {
    controller.current?.abort(); if (!appliedCity) return;
    const active = new AbortController(); controller.current = active;
    setItems([]); setCursor(null); setError(''); setLoading(true);
    void searchPosts(appliedCity, appliedDate, appliedLevel, appliedTerm, appliedSort, origin, radius, null, active.signal)
      .then(result => { if (!active.signal.aborted) { setItems(result.items); setCursor(result.nextCursor ?? null); } })
      .catch(reason => { if (!active.signal.aborted) setError(errorText(reason)); })
      .finally(() => { if (!active.signal.aborted) setLoading(false); });
    return () => active.abort();
  }, [appliedCity, appliedDate, appliedLevel, appliedTerm, appliedSort, origin?.latitude, origin?.longitude, radius, retry]);
  function apply(event: FormEvent) {
    event.preventDefault(); if (!selectedCity) return;
    const normalizedTerm = term.trim();
    if (normalizedTerm.length === 1) { setError('Nhập ít nhất 2 ký tự để tìm kiếm.'); return; }
    try { localStorage.setItem(cityKey, selectedCity); } catch { /* URL still works. */ }
    const params = new URLSearchParams({ city: selectedCity });
    if (date) params.set('date', date);
    if (level !== 'ANY') params.set('level', level);
    if (normalizedTerm) params.set('q', normalizedTerm);
    if (sort !== 'NEWEST') params.set('sort', sort);
    const next = `/teams?${params}`;
    if (next === location.pathname + location.search) setRetry(n => n + 1);
    else navigate(next);
  }
  async function locatePlace() {
    if (!key || !place.trim()) return;
    setLocating(true); setError('');
    try {
      const response = await fetch(`https://api.maptiler.com/geocoding/${encodeURIComponent(place.trim())}.json?key=${encodeURIComponent(key)}&limit=1&country=vn`);
      const result = await response.json() as { features?: { center?: [number, number]; place_name?: string }[] };
      const point = result.features?.[0];
      if (!response.ok || !point?.center) throw new Error('Không tìm thấy điểm xuất phát. Hãy nhập địa điểm cụ thể hơn.');
      setOrigin({ longitude: point.center[0], latitude: point.center[1], label: point.place_name ?? place.trim() });
    } catch (reason) { setError(reason instanceof Error ? reason.message : errorText(reason)); }
    finally { setLocating(false); }
  }
  function locateGps() {
    if (!navigator.geolocation) { setError('Trình duyệt không hỗ trợ vị trí. Bạn vẫn có thể tìm theo thành phố.'); return; }
    setLocating(true); setError('');
    navigator.geolocation.getCurrentPosition(value => {
      setOrigin({ latitude: value.coords.latitude, longitude: value.coords.longitude, label: 'Vị trí của tôi' }); setLocating(false);
    }, () => { setError('Chưa lấy được vị trí. Bạn vẫn có thể tìm theo thành phố.'); setLocating(false); }, { enableHighAccuracy: false, timeout: 8000 });
  }
  async function more() {
    if (!cursor || loading || !appliedCity) return;
    const active = new AbortController(); controller.current = active; setLoading(true); setError('');
    try { const result = await searchPosts(appliedCity, appliedDate, appliedLevel, appliedTerm, appliedSort, origin, radius, cursor, active.signal);
      if (!active.signal.aborted) { setItems(old => [...old, ...result.items]); setCursor(result.nextCursor ?? null); }
    } catch (reason) { if (!active.signal.aborted) setError(errorText(reason)); }
    finally { if (!active.signal.aborted) setLoading(false); }
  }
  const hasFilter = Boolean(date || level !== 'ANY' || term || origin || appliedDate || appliedLevel !== 'ANY' || appliedTerm);
  const hasAppliedFilter = Boolean(appliedDate || appliedLevel !== 'ANY' || appliedTerm || origin);
  const hasDraftChanges = selectedCity !== appliedCity || date !== appliedDate || level !== appliedLevel || term.trim() !== appliedTerm || sort !== appliedSort;
  function clearExploreFilters() {
    if (!appliedCity) return;
    setCity(appliedCity); setDate(''); setLevel('ANY'); setTerm(''); setOrigin(null);
    navigate(`/teams?city=${appliedCity}`);
  }
  return <CustomerShell className="team-explore">
    <CustomerFeatureHero label="TÌM NGƯỜI" title="Tìm người chơi cùng." />
    <div className="team-layout"><section className="team-filter" aria-label="Tìm và lọc lịch chơi"><form onSubmit={apply}>
      <label className="team-search-label">Tìm lịch chơi<input type="search" value={term} maxLength={80} onChange={e => setTerm(e.target.value)} placeholder="Tìm sân, địa điểm, người đăng hoặc nội dung" /></label>
      <label>Thành phố<select id="team-city-select" value={selectedCity ?? ''} onChange={e => { setCity(city(e.target.value)); setOrigin(null); setPlace(''); }} required>
        <option value="">Chọn thành phố</option><option value="HN">Hà Nội</option><option value="HCM">TP.HCM</option></select></label>
      <label>Sắp xếp<select value={sort} onChange={e => setSort(e.target.value)}><option value="NEWEST">Bài mới nhất</option><option value="SOONEST">Giờ chơi gần nhất</option></select></label>
      <details className="team-advanced"><summary>Ngày, trình độ và khoảng cách</summary><div className="team-advanced-fields">
        <label>Ngày chơi<input type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
        <label>Trình độ mong muốn<select value={level} onChange={e => setLevel(e.target.value as Level)}>
          {levelOptions.map(value => <option key={value} value={value}>{levelName[value]}</option>)}</select></label>
        <div className="team-location"><h3>Gần một địa điểm</h3>
          {key && <><label>Điểm xuất phát<input value={place} onChange={e => setPlace(e.target.value)} placeholder="Nhập địa điểm hoặc địa chỉ" /></label>
            <button type="button" disabled={locating || !place.trim()} onClick={() => void locatePlace()}>Dùng địa điểm này</button></>}
          <button type="button" disabled={locating} onClick={locateGps}>Dùng vị trí của tôi</button>
          <label>Bán kính<select value={radius} onChange={e => setRadius(e.target.value)}><option value="3000">3 km</option>
            <option value="5000">5 km</option><option value="10000">10 km</option><option value="20000">20 km</option></select></label>
          {origin && <p role="status">Từ {origin.label} <button type="button" onClick={() => setOrigin(null)}>Bỏ vị trí</button></p>}
        </div></div></details>
      <button type="submit">{hasDraftChanges ? 'Áp dụng' : 'Tìm kiếm'}</button>
      {hasFilter && !hasAppliedFilter && <button type="button" className="team-clear" onClick={clearExploreFilters}>Xóa lọc</button>}
    </form></section><div className="team-results"><div className="team-results-head"><div><h2>Lịch đang tìm người</h2>{items.length > 0 && <small>Đã tải {items.length} bài{cursor ? ' · còn bài khác' : ''}</small>}</div><div className="team-results-links"><a href="/me/teams">Đăng tìm người</a><a href="/me/team-requests">Yêu cầu của tôi</a></div></div>
      {appliedCity && hasAppliedFilter && <div className="team-active-filters" role="group" aria-label="Bộ lọc tìm người đang áp dụng">
        {appliedTerm && <span><strong>Từ khóa</strong> {appliedTerm}</span>}
        {appliedDate && <span><strong>Ngày</strong> {appliedDate.split('-').reverse().join('/')}</span>}
        {appliedLevel !== 'ANY' && <span><strong>Trình độ</strong> {levelName[appliedLevel]}</span>}
        {origin && <span><strong>Gần</strong> {origin.label} · {Number(radius) / 1000} km</span>}
        <button type="button" onClick={clearExploreFilters}>Xóa lọc</button>
      </div>}
      {!appliedCity && <div className="team-empty"><p>Chọn Hà Nội hoặc TP.HCM để xem lịch chơi.</p>
        <button type="button" onClick={() => document.getElementById('team-city-select')?.focus()}>Chọn thành phố</button></div>}
      {error && <p role="alert" className="team-error">{error} <button type="button" onClick={() => setRetry(n => n + 1)}>Thử lại</button></p>}
      {loading && <p role="status">Đang tìm lịch chơi…</p>}
      {!loading && appliedCity && !error && !items.length && <div className="team-empty">
        <p>{hasAppliedFilter ? 'Chưa có lịch phù hợp với bộ lọc này.' : 'Hiện chưa có lịch tìm người tại thành phố này.'}</p>
        <div className="team-empty-actions">
          {hasAppliedFilter && <button type="button" onClick={clearExploreFilters}>Xóa bộ lọc</button>}
          <button type="button" onClick={() => document.getElementById('team-city-select')?.focus()}>Đổi thành phố</button>
          <a href="/me/teams">Đăng tìm người</a>
        </div>
      </div>}
      <div className="team-grid">{items.map(post => <PostCard key={post.id} post={post} />)}</div>
      {cursor && <button type="button" disabled={loading} onClick={() => void more()}>Xem thêm lịch chơi</button>}
    </div></div></CustomerShell>;
}

export function TeamDetail({ id }: { id: string }) {
  const { session, request } = useCustomerSession(); const [post, setPost] = useState<Post | null>(null);
  const [requestMessage, setRequestMessage] = useState(''); const [reason, setReason] = useState('SPAM');
  const [sharePhone, setSharePhone] = useState(false); const [contactPhone, setContactPhone] = useState('');
  const [profileOpen, setProfileOpen] = useState(false);
  const [gameOpen, setGameOpen] = useState(false);
  const [error, setError] = useState(''); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false);
  const [existingRequest, setExistingRequest] = useState<Request | null>(null);
  const [acceptedPhone, setAcceptedPhone] = useState<string | null>(null);
  const [requestCheck, setRequestCheck] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle');
  const [requestRetry, setRequestRetry] = useState(0);
  const [loading, setLoading] = useState(true);
  useEffect(() => { const controller = new AbortController(); setLoading(true); setError(''); setPost(null);
    void publicData<Post>(`/recruitment-posts/${id}`, controller.signal).then(setPost)
      .catch(e => { if (!controller.signal.aborted) setError(errorText(e)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort(); }, [id]);
  useEffect(() => {
    if (!session) { setExistingRequest(null); setAcceptedPhone(null); setRequestCheck('idle'); return; }
    const controller = new AbortController(); setExistingRequest(null); setAcceptedPhone(null); setRequestCheck('loading');
    void request(`/api/v1/me/recruitment-requests?postId=${encodeURIComponent(id)}`, { signal: controller.signal })
      .then(data<Page<{ request: Request; post: Post }>>)
      .then(result => { if (!controller.signal.aborted) {
        const item = result.items[0];
        setExistingRequest(item?.request ?? null);
        setAcceptedPhone(item?.request.status === 'ACCEPTED' ? item.post.contactPhone ?? null : null);
        setRequestCheck('ready');
      } }).catch(() => { if (!controller.signal.aborted) setRequestCheck('error'); });
    return () => controller.abort();
  }, [id, Boolean(session), request, requestRetry]);
  async function send(path: string, body: object, success: string, isRequest = false) {
    if (busy) return; setBusy(true); setError(''); setMessage('');
    try { const result = await request(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }).then(data<Request>);
      setMessage(success); if (isRequest) { setExistingRequest(result); setRequestCheck('ready'); }
    } catch (e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  return <CustomerShell title="Chi tiết lịch chơi" eyebrow="SHUTTLEBOOK / TÌM TRẬN">
    <section className="team-detail"><p><a href="/teams">← Quay lại tìm trận</a></p>
      {loading && <p role="status">Đang tải lịch chơi…</p>}{error && <p role="alert" className="team-error">{error}</p>}
      {post && <><h2>{post.venueName}</h2><p><strong>{post.courtName}</strong> · {when(post)}</p>
        <div className="team-card-author"><ProfileAvatar name={post.contactName || 'Người tổ chức'} onClick={() => setProfileOpen(true)} />
          <strong>{post.contactName || 'Người tổ chức'}</strong></div>
        {post.caption && <p className="team-caption">{post.caption}</p>}
        <TeamImage post={post} />
        <p>{post.address}</p><p>Còn {post.slotsOpen} suất · Trình độ mong muốn: {levelName[post.level]} (tự khai báo)</p>
        {contributionText(post) && <p className="team-contribution">{contributionText(post)}. Do người đăng khai báo; ShuttleBook không thu hộ.</p>}
        {post.contactPhone && <p>Liên hệ người đăng: <a href={`tel:${post.contactPhone}`}>{post.contactPhone}</a></p>}
        {session ? <div className="team-actions">
          {!existingRequest && <><label>Lời nhắn cho người đăng (tùy chọn)<textarea maxLength={500} rows={3}
            value={requestMessage} onChange={e => setRequestMessage(e.target.value)}
            placeholder="Ví dụ: Tôi mới chơi nhưng sân gần nhà, mong được tham gia." /></label>
            <label className="team-consent"><input type="checkbox" checked={sharePhone} onChange={e => setSharePhone(e.target.checked)} />
            Chia sẻ số điện thoại với chủ lịch sau khi được nhận</label>
            {sharePhone && <label>Số điện thoại<input type="tel" inputMode="tel" value={contactPhone}
              onChange={e => setContactPhone(e.target.value)} placeholder="09…" /></label>}</>}
          {requestCheck === 'loading' && <p role="status">Đang kiểm tra yêu cầu của bạn…</p>}
          {requestCheck === 'error' && <p role="alert" className="team-error">Chưa kiểm tra được yêu cầu của bạn.
            <button type="button" onClick={() => setRequestRetry(value => value + 1)}>Thử lại</button></p>}
          {requestCheck === 'ready' && (existingRequest
            ? <p role="status">Yêu cầu của bạn: <strong>{statusName[existingRequest.status] ?? existingRequest.status}</strong>.</p>
            : <button type="button" disabled={busy || sharePhone && !/^(?:\+84|0)[0-9]{9,10}$/.test(contactPhone.trim())}
              onClick={() => void send(`/api/v1/recruitment-posts/${id}/requests`,
                { message: requestMessage.trim(), contactPhone: sharePhone ? contactPhone.trim() : null, sharePhoneWithAccepted: sharePhone },
                'Đã gửi yêu cầu. Xem trạng thái trong Yêu cầu của tôi.', true)}>Gửi yêu cầu tham gia</button>)}
          {existingRequest?.status === 'ACCEPTED' && <button type="button" onClick={() => setGameOpen(true)}>Xem thông tin sân và liên hệ</button>}
          <a href="/me/team-requests">Yêu cầu của tôi</a></div>
          : <a className="team-link" href={`/login?returnTo=${encodeURIComponent(`/teams/${id}`)}`}>Đăng nhập để xin tham gia</a>}
        {session && <div className="team-report"><label>Báo cáo bài<select value={reason} onChange={e => setReason(e.target.value)}>
          <option value="SPAM">Bài rác</option><option value="MISLEADING">Thông tin gây hiểu nhầm</option><option value="UNSAFE">Không an toàn</option></select></label>
          <button type="button" disabled={busy} onClick={() => void send(`/api/v1/recruitment-posts/${id}/reports`, { reason }, 'Đã gửi báo cáo để quản trị kiểm tra.')}>Gửi báo cáo</button></div>}
        {profileOpen && <PersonProfileDialog person={{ name: post.contactName || 'Người tổ chức', phone: post.contactPhone ?? acceptedPhone }} onClose={() => setProfileOpen(false)} />}
        {gameOpen && existingRequest?.status === 'ACCEPTED' && <AcceptedGameDialog post={{ ...post, contactPhone: post.contactPhone ?? acceptedPhone }} onClose={() => setGameOpen(false)} />}
      </>}{message && <p role="status" className="team-success">{message}</p>}
    </section></CustomerShell>;
}

export function MyTeams() {
  const { session, request, displayName } = useCustomerSession(); const bookingId = new URLSearchParams(location.search).get('bookingId');
  const [bookingContext, setBookingContext] = useState<Booking | null>(null);
  const [slots, setSlots] = useState('1'); const [level, setLevel] = useState<Level>('ANY');
  const [caption, setCaption] = useState(''); const [contactName, setContactName] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [phoneVisibility, setPhoneVisibility] = useState<'HIDDEN' | 'ACCEPTED' | 'PUBLIC'>('HIDDEN');
  const [contributionMode, setContributionMode] = useState<'UNSPECIFIED' | 'COMMON' | 'BY_GENDER'>('UNSPECIFIED');
  const [commonContribution, setCommonContribution] = useState('');
  const [maleContribution, setMaleContribution] = useState('');
  const [femaleContribution, setFemaleContribution] = useState('');
  const [photo, setPhoto] = useState<File | null>(null); const [photoPreview, setPhotoPreview] = useState('');
  const fileInput = useRef<HTMLInputElement>(null); const nameInput = useRef<HTMLInputElement>(null);
  const phoneInput = useRef<HTMLInputElement>(null); const publishFeedback = useRef<HTMLParagraphElement>(null);
  const commonInput = useRef<HTMLInputElement>(null); const maleInput = useRef<HTMLInputElement>(null);
  const femaleInput = useRef<HTMLInputElement>(null);
  const [publishError, setPublishError] = useState(''); const [publishMessage, setPublishMessage] = useState('');
  const [feedbackFocus, setFeedbackFocus] = useState(0);
  const [posts, setPosts] = useState<Post[]>([]); const [selected, setSelected] = useState<string | null>(null);
  const reviewDialog = useRef<HTMLDialogElement>(null); const reviewTrigger = useRef<HTMLButtonElement | null>(null);
  const [profile, setProfile] = useState<PersonProfile | null>(null);
  const [requestsLoading, setRequestsLoading] = useState(false); const [requestError, setRequestError] = useState('');
  const [requests, setRequests] = useState<OwnerRequest[]>([]); const [error, setError] = useState('');
  const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(true);
  const [shareMessage, setShareMessage] = useState<{ postId: string; text: string } | null>(null);
  const [editingPostId, setEditingPostId] = useState<string | null>(null);
  const [editingSlots, setEditingSlots] = useState('1');
  const [capacityError, setCapacityError] = useState('');
  const [reload, setReload] = useState(0);
  useEffect(() => {
    if (!photo) { setPhotoPreview(''); return; }
    const url = URL.createObjectURL(photo); setPhotoPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [photo]);
  useEffect(() => { if (feedbackFocus > 0) publishFeedback.current?.focus(); }, [feedbackFocus]);
  useEffect(() => { if (displayName && !contactName) setContactName(displayName); }, [displayName]);
  useEffect(() => { if (!session) navigate(`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`, true); }, [Boolean(session)]);
  useEffect(() => {
    if (!session || !bookingId) { setBookingContext(null); return; }
    const controller = new AbortController();
    void request(`/api/v1/bookings/${encodeURIComponent(bookingId)}`, { signal: controller.signal }).then(data<Booking>)
      .then(result => { if (!controller.signal.aborted) setBookingContext(result); })
      .catch(() => { if (!controller.signal.aborted) setBookingContext(null); });
    return () => controller.abort();
  }, [Boolean(session), request, bookingId]);
  useEffect(() => { if (!session) return; const controller = new AbortController(); setLoading(true);
    const path = bookingId ? `/api/v1/me/recruitment-posts?bookingId=${encodeURIComponent(bookingId)}` : '/api/v1/me/recruitment-posts';
    void request(path, { signal: controller.signal }).then(data<Page<Post>>)
      .then(result => { if (!controller.signal.aborted) { setPosts(result.items); setError(''); } })
      .catch(e => { if (!controller.signal.aborted) setError(errorText(e)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); }); return () => controller.abort();
  }, [Boolean(session), request, bookingId, reload]);
  useEffect(() => { if (!session || !selected) return; const controller = new AbortController();
    setRequests([]); setRequestError(''); setRequestsLoading(true);
    void request(`/api/v1/me/recruitment-posts/${selected}/requests`, { signal: controller.signal }).then(data<Page<OwnerRequest>>)
      .then(result => { if (!controller.signal.aborted) setRequests(result.items); })
      .catch(e => { if (!controller.signal.aborted) setRequestError(errorText(e)); })
      .finally(() => { if (!controller.signal.aborted) setRequestsLoading(false); }); return () => controller.abort();
  }, [Boolean(session), request, selected, reload]);
  useEffect(() => { if (selected && reviewDialog.current && !reviewDialog.current.open) reviewDialog.current.showModal(); }, [selected]);
  async function command(path: string, body: object) {
    if (busy) return; setBusy(true); setError(''); setMessage('');
    try { await request(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }).then(data);
      setReload(n => n + 1); setMessage('Đã cập nhật.'); } catch (e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  async function saveSlots(post: Post) {
    const count = Number(editingSlots);
    if (busy || !Number.isInteger(count) || count < 1 || count > 8) return;
    setBusy(true); setCapacityError(''); setError(''); setMessage('');
    try {
      const updated = await request(`/api/v1/me/recruitment-posts/${post.id}/capacity`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ slotsNeeded: count })
      }).then(data<Post>);
      setPosts(previous => previous.map(item => item.id === post.id ? updated : item));
      setEditingPostId(null); setMessage(`Đã cập nhật: cần ${updated.slotsNeeded} người.`); setReload(value => value + 1);
    } catch (reason) { setCapacityError(errorText(reason)); } finally { setBusy(false); }
  }
  async function publish(event: FormEvent) {
    event.preventDefault();
    if (!bookingId || busy) return;
    setPublishMessage('');
    if (contactName.trim().length < 2 || contactName.trim().length > 100) {
      setPublishError('Tên người liên hệ cần từ 2 đến 100 ký tự.'); nameInput.current?.focus(); return;
    }
    if (phoneVisibility !== 'HIDDEN' && !/^(?:\+84|0)[0-9]{9,10}$/.test(contactPhone.trim())) {
      setPublishError('Nhập số điện thoại Việt Nam hợp lệ.'); phoneInput.current?.focus(); return;
    }
    const amount = (value: string) => /^\d+$/.test(value) && Number(value) <= 2_000_000 ? Number(value) : null;
    const commonAmount = contributionMode === 'COMMON' ? amount(commonContribution) : null;
    const maleAmount = contributionMode === 'BY_GENDER' ? amount(maleContribution) : null;
    const femaleAmount = contributionMode === 'BY_GENDER' ? amount(femaleContribution) : null;
    if (contributionMode === 'COMMON' && commonAmount === null) {
      setPublishError('Mức góp chung cần là số VND từ 0 đến 2.000.000.'); commonInput.current?.focus(); return;
    }
    if (contributionMode === 'BY_GENDER' && (maleAmount === null || femaleAmount === null)) {
      setPublishError('Nhập cả hai mức nam/nữ bằng số VND từ 0 đến 2.000.000.');
      (maleAmount === null ? maleInput : femaleInput).current?.focus(); return;
    }
    if (photo && (!['image/png', 'image/jpeg', 'image/webp'].includes(photo.type) || photo.size < 1 || photo.size > 5_242_880)) {
      setPublishError('Chọn ảnh PNG/JPEG/WebP tối đa 5 MB.'); fileInput.current?.focus(); return;
    }
    setBusy(true); setPublishError(''); setError(''); setMessage('');
    try {
      let imageUploadId: string | null = null;
      if (photo) {
        const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', await photo.arrayBuffer()));
        const sha256Base64 = btoa(String.fromCharCode(...digest));
        const presigned = await request(`/api/v1/bookings/${bookingId}/recruitment-image/presign`, {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ contentType: photo.type, sizeBytes: photo.size, sha256Base64 })
        }).then(data<{ id: string; uploadUrl: string; uploadHeaders: Record<string, string> }>);
        const sent = await fetch(presigned.uploadUrl, { method: 'PUT', headers: presigned.uploadHeaders, body: photo });
        if (!sent.ok) throw new Error('UPLOAD_FAILED');
        await request(`/api/v1/uploads/${presigned.id}/complete`, { method: 'POST' }).then(data);
        imageUploadId = presigned.id;
      }
      const created = await request('/api/v1/me/recruitment-posts', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ bookingId, slotsNeeded: Number(slots), level, caption: caption.trim(),
          contactName: contactName.trim(), contactPhone: phoneVisibility === 'HIDDEN' ? null : contactPhone.trim(),
          sharePhoneWithAccepted: phoneVisibility === 'ACCEPTED', publishContactPhone: phoneVisibility === 'PUBLIC',
          contributionMode, commonContributionVnd: commonAmount, maleContributionVnd: maleAmount,
          femaleContributionVnd: femaleAmount, imageUploadId }) }).then(data<Post>);
      setPublishMessage(`Bài đã đăng: cần ${created.slotsNeeded} người.`); setCaption(''); setPhoto(null);
      if (fileInput.current) fileInput.current.value = '';
      setReload(n => n + 1);
      setFeedbackFocus(value => value + 1);
    } catch (reason) { setPublishError(reason instanceof Error && reason.message === 'UPLOAD_FAILED'
      ? 'Không tải được ảnh. Hãy thử lại.' : errorText(reason));
      setFeedbackFocus(value => value + 1); }
    finally { setBusy(false); }
  }
  return <CustomerShell title={bookingId ? 'Quản lý buổi chơi' : 'Bài tìm người của tôi'} eyebrow="SHUTTLEBOOK / LỊCH CHƠI">
    <div className="team-owner"><section className="team-panel team-compose">
      <h2>{bookingId && !loading && posts.length ? 'Buổi chơi này' : 'Đăng tìm người'}</h2>
      {bookingId && <p><a href="/me/teams">Xem tất cả bài đã đăng</a> · <a href={`/bookings/${encodeURIComponent(bookingId)}`}>Xem đơn</a></p>}
      {bookingId && bookingContext && <div className="team-compose-booking" aria-label="Buổi chơi sẽ đăng bài"><strong>{bookingContext.venueName}</strong>
        <span>{bookingContext.courtName} · {bookingContext.date.split('-').reverse().join('/')} · {bookingContext.localStart}–{bookingContext.localEnd}</span></div>}
      {bookingId && !loading && !error && posts.length === 0 ? <form onSubmit={publish}>
        <label>Người liên hệ<input ref={nameInput} required minLength={2} maxLength={100} value={contactName} onChange={e => setContactName(e.target.value)} placeholder="Tên để người chơi nhận ra bạn" /></label>
        <label>Nội dung bài viết<textarea maxLength={1000} rows={4} value={caption} onChange={e => setCaption(e.target.value)} placeholder="Giới thiệu trận, trình độ mong muốn và điều cần biết…" /></label>
        <label>Ảnh bài viết (tùy chọn)<input ref={fileInput} type="file" accept="image/png,image/jpeg,image/webp" onChange={e => {
          const selected = e.target.files?.[0] ?? null;
          if (selected && (!['image/png', 'image/jpeg', 'image/webp'].includes(selected.type) || selected.size < 1 || selected.size > 5_242_880)) {
            setPhoto(null); e.target.value = ''; setPublishError('Chọn ảnh PNG/JPEG/WebP tối đa 5 MB.');
            requestAnimationFrame(() => fileInput.current?.focus()); return;
          }
          setPhoto(selected); setPublishError(''); setPublishMessage('');
        }} /></label>
        {photo && <div className="team-photo-preview">{photoPreview && <img src={photoPreview} alt="Ảnh xem trước bài tìm người" />}
          <p>Đã chọn: {photo.name}</p><div><button type="button" onClick={() => fileInput.current?.click()}>Chọn ảnh khác</button>
          <button type="button" onClick={() => { setPhoto(null); if (fileInput.current) fileInput.current.value = ''; }}>Bỏ ảnh</button></div></div>}
        <label>Hiển thị số điện thoại<select value={phoneVisibility} onChange={e => setPhoneVisibility(e.target.value as typeof phoneVisibility)}>
          <option value="HIDDEN">Không hiển thị</option><option value="ACCEPTED">Chỉ người đã được nhận</option>
          <option value="PUBLIC">Công khai trên bài</option></select></label>
        {phoneVisibility !== 'HIDDEN' && <label>Số điện thoại liên hệ<input ref={phoneInput} type="tel" required pattern="(?:\+84|0)[0-9]{9,10}"
          value={contactPhone} onChange={e => setContactPhone(e.target.value)} placeholder="09xxxxxxxx" /></label>}
        {phoneVisibility === 'PUBLIC' && <p className="team-privacy-note">Số này sẽ hiển thị cho mọi người xem bài.</p>}
        <label>Số người cần (1–8)<input type="number" min="1" max="8" value={slots} onChange={e => setSlots(e.target.value)} /></label>
        <p role="status">Bài này sẽ tìm <strong>{slots || '0'} người</strong>.</p>
        <label>Trình độ mong muốn<select value={level} onChange={e => setLevel(e.target.value as Level)}>
          {levelOptions.map(value => <option key={value} value={value}>{levelName[value]}</option>)}</select></label>
        <label>Mức góp tham gia<select value={contributionMode} onChange={e => setContributionMode(e.target.value as typeof contributionMode)}>
          <option value="UNSPECIFIED">Chưa nêu mức góp</option><option value="COMMON">Một mức chung</option>
          <option value="BY_GENDER">Mức nam và nữ riêng</option></select></label>
        {contributionMode === 'COMMON' && <label>Mức góp chung (VND/người)<input ref={commonInput} type="number" min="0" max="2000000" step="1" required
          value={commonContribution} onChange={e => setCommonContribution(e.target.value)} /></label>}
        {contributionMode === 'BY_GENDER' && <div className="team-contribution-fields"><label>Mức góp nam (VND/người)<input ref={maleInput} type="number" min="0" max="2000000" step="1" required
          value={maleContribution} onChange={e => setMaleContribution(e.target.value)} /></label>
          <label>Mức góp nữ (VND/người)<input ref={femaleInput} type="number" min="0" max="2000000" step="1" required
          value={femaleContribution} onChange={e => setFemaleContribution(e.target.value)} /></label></div>}
        {contributionMode !== 'UNSPECIFIED' && <p className="team-privacy-note">Mức góp do bạn khai báo; ShuttleBook không thu hộ và không thay đổi tiền đặt sân.</p>}
        <button type="submit" disabled={busy || !Number.isInteger(Number(slots)) || Number(slots) < 1 || Number(slots) > 8}>{busy ? 'Đang đăng…' : 'Đăng tìm người'}</button>
        {publishError && <p ref={publishFeedback} tabIndex={-1} role="alert" className="team-error">{publishError}</p>}</form>
        : bookingId ? <p>{loading ? 'Đang kiểm tra bài của buổi chơi…' : error ? 'Chưa kiểm tra được bài của buổi chơi.' : 'Bài của buổi chơi này ở bên dưới.'}</p>
          : <p>Mở một đơn vãng lai đã xác nhận trong <a href="/me/bookings">Đơn của tôi</a> để đăng bài.</p>}
      {publishMessage && <p ref={publishFeedback} tabIndex={-1} role="status" className="team-success">{publishMessage}</p>}
    </section><section className="team-panel"><h2>{bookingId ? 'Người tham gia' : 'Bài đã đăng'}</h2>
      {loading && <p role="status">Đang tải bài…</p>}{!loading && !posts.length &&
        <p>{bookingId ? 'Chưa có bài tìm người cho buổi này.' : 'Bạn chưa đăng bài nào.'}</p>}
      {posts.map(post => <article key={post.id} className="team-owner-row"><header className="team-card-author"><span className="team-avatar" aria-hidden="true">{(post.contactName || 'N').charAt(0).toUpperCase()}</span><span><strong>{post.contactName || 'Người tổ chức'}</strong><small>{new Date(post.createdAt).toLocaleString('vi-VN')}</small></span></header>
        {post.caption && <p className="team-caption">{post.caption}</p>}
        {post.status === 'OPEN' && <TeamImage post={post} />}
        <h3>{post.venueName}</h3><p>{when(post)} · {statusName[post.status] ?? post.status}</p>
        <p>Đã nhận {post.acceptedCount ?? post.slotsNeeded - post.slotsOpen}/{post.slotsNeeded} người ·
          {post.pendingCount ?? 0} yêu cầu đang chờ · còn {post.slotsOpen} suất</p>
        {post.status === 'OPEN' && (editingPostId === post.id
          ? <div className="team-capacity-edit"><label>Số người cần<input type="number" min={Math.max(1, post.acceptedCount ?? 0)} max="8"
            value={editingSlots} onChange={event => setEditingSlots(event.target.value)} /></label>
            <button type="button" disabled={busy || !Number.isInteger(Number(editingSlots)) || Number(editingSlots) < Math.max(1, post.acceptedCount ?? 0) || Number(editingSlots) > 8}
              onClick={() => void saveSlots(post)}>Lưu số người</button>
            <button type="button" onClick={() => { setEditingPostId(null); setCapacityError(''); }}>Hủy</button>
            {capacityError && <p role="alert" className="team-error">{capacityError}</p>}</div>
          : <button type="button" onClick={() => { setCapacityError(''); setEditingPostId(post.id); setEditingSlots(String(post.slotsNeeded)); }}>Sửa số người cần</button>)}
        {post.contactPhone && <p>Số điện thoại: {post.contactPhone} · {post.phoneVisibility === 'PUBLIC' ? 'Công khai' : 'Chỉ người đã được nhận'}</p>}
        {contributionText(post) && <p className="team-contribution">{contributionText(post)}</p>}
        <button type="button" onClick={event => { reviewTrigger.current = event.currentTarget; setSelected(post.id); }}>Xem yêu cầu</button>
        {post.shareable && <><a className="team-owner-share" href={`/teams/${post.id}`}>Xem bài công khai</a>
          <button type="button" onClick={() => { if (!navigator.clipboard?.writeText) {
            setShareMessage({ postId: post.id, text: 'Chưa sao chép được. Hãy mở bài công khai để lấy link.' }); return;
          } void navigator.clipboard.writeText(new URL(`/teams/${post.id}`, location.origin).href)
            .then(() => setShareMessage({ postId: post.id, text: 'Đã sao chép link bài.' }))
            .catch(() => setShareMessage({ postId: post.id, text: 'Chưa sao chép được. Hãy mở bài công khai để lấy link.' })); }}>Sao chép link</button>
          {shareMessage?.postId === post.id && <p role="status">{shareMessage.text}</p>}</>}
        {post.status === 'OPEN' && <button type="button" disabled={busy} onClick={() => void command(`/api/v1/me/recruitment-posts/${post.id}/close`, {})}>Đóng bài</button>}</article>)}
    </section>{selected && <dialog ref={reviewDialog} className="team-dialog team-review-dialog" aria-labelledby="team-review-title"
      onClose={() => { setSelected(null); setProfile(null); requestAnimationFrame(() => reviewTrigger.current?.focus()); }}
      onClick={event => { if (event.target === reviewDialog.current) reviewDialog.current?.close(); }}>
      <div className="team-dialog-head"><div><h2 id="team-review-title">Yêu cầu tham gia</h2>
        <p>{posts.find(post => post.id === selected)?.venueName ?? 'Bài tìm người'}</p></div>
        <button type="button" onClick={() => reviewDialog.current?.close()} aria-label="Đóng danh sách yêu cầu">✕</button></div>
      {requestsLoading && <p role="status">Đang tải yêu cầu…</p>}
      {requestError && <p role="alert" className="team-error">{requestError} <button type="button" onClick={() => setReload(n => n + 1)}>Thử lại</button></p>}
      {!requestsLoading && !requestError && !requests.length && <p>Chưa có yêu cầu.</p>}
      {!requestsLoading && !requestError && requests.length > 0 && <p>
        Đã nhận {requests.filter(item => item.status === 'ACCEPTED').length} người ·
        {requests.filter(item => item.status === 'PENDING').length} yêu cầu đang chờ
      </p>}
      {[...requests].sort((a, b) => (requestRank[a.status] ?? 4) - (requestRank[b.status] ?? 4)).map(item => <div key={item.id} className="team-owner-row">
        <div className="team-card-author"><ProfileAvatar name={item.displayName} onClick={() => setProfile({ name: item.displayName, level: item.level, phone: item.contactPhone })} />
          <div><strong>{item.displayName}</strong><small>{item.level !== 'UNSPECIFIED' ? `${applicantLevelName[item.level]} · ` : ''}{statusName[item.status] ?? item.status}</small></div></div>
        {item.message && <p className="team-request-message">“{item.message}”</p>}
        {item.status === 'PENDING' && <div className="team-review-actions"><button type="button" disabled={busy} onClick={() => void command(`/api/v1/me/recruitment-posts/${selected}/requests/${item.id}/accept`, {})}>Nhận người chơi</button>
          <button type="button" disabled={busy} onClick={() => void command(`/api/v1/me/recruitment-posts/${selected}/requests/${item.id}/decline`, {})}>Từ chối</button></div>}
        {item.status === 'ACCEPTED' && <div className="team-review-actions"><button type="button" onClick={() => setProfile({ name: item.displayName, level: item.level, phone: item.contactPhone })}>Xem thông tin</button></div>}</div>)}
      {!selected && error && <p role="alert" className="team-error">{error}</p>}{!selected && message && <p role="status" className="team-success">{message}</p>}
    </dialog>}
      {profile && <PersonProfileDialog person={profile} onClose={() => setProfile(null)} />}
      {error && <p role="alert" className="team-error">{error}</p>}{message && <p role="status" className="team-success">{message}</p>}
    </div></CustomerShell>;
}

export function MyTeamRequests() {
  const { session, request } = useCustomerSession(); const postId = new URLSearchParams(location.search).get('postId');
  const [items, setItems] = useState<{ request: Request; post: Post }[]>([]);
  const [profile, setProfile] = useState<PersonProfile | null>(null);
  const [selectedGame, setSelectedGame] = useState<Post | null>(null);
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [busy, setBusy] = useState(''); const [reload, setReload] = useState(0);
  useEffect(() => { if (!session) navigate(`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`, true); }, [Boolean(session)]);
  useEffect(() => { if (!session) return; const controller = new AbortController(); setLoading(true);
    const path = postId ? `/api/v1/me/recruitment-requests?postId=${encodeURIComponent(postId)}` : '/api/v1/me/recruitment-requests';
    void request(path, { signal: controller.signal }).then(data<Page<{ request: Request; post: Post }>>)
      .then(result => { if (!controller.signal.aborted) { setItems(result.items); setError(''); } })
      .catch(e => { if (!controller.signal.aborted) setError(errorText(e)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); }); return () => controller.abort();
  }, [Boolean(session), request, postId, reload]);
  async function withdraw(id: string) {
    if (busy) return; setBusy(id); setError('');
    try { await request(`/api/v1/me/recruitment-requests/${id}/withdraw`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }).then(data);
      setReload(n => n + 1); } catch (e) { setError(errorText(e)); } finally { setBusy(''); }
  }
  return <CustomerShell title="Yêu cầu tham gia của tôi" eyebrow="SHUTTLEBOOK / TÌM TRẬN">
    <section className="team-panel"><p><a href="/teams">← Tìm lịch chơi khác</a></p>
      {postId && <p><a href="/me/team-requests">Xem tất cả yêu cầu</a></p>}
      {loading && <p role="status">Đang tải yêu cầu…</p>}{error && <p role="alert" className="team-error">{error}</p>}
      {!loading && !items.length && !error && <p>Bạn chưa gửi yêu cầu nào.</p>}
      {items.map(({ request: item, post }) => <article className="team-owner-row" key={item.id}>
        <div className="team-card-author"><ProfileAvatar name={post.contactName || 'Người tổ chức'}
          onClick={() => setProfile({ name: post.contactName || 'Người tổ chức', phone: post.contactPhone })} />
          <strong>{post.contactName || 'Người tổ chức'}</strong></div>
        <h2>{post.venueName}</h2><p>{post.courtName} · {when(post)}</p><p>{post.address}</p>
        {contributionText(post) && <p className="team-contribution">{contributionText(post)} · Do người đăng khai báo</p>}
        <p><strong>{statusName[item.status] ?? item.status}</strong>{item.level !== 'UNSPECIFIED' ? ` · Trình độ đã khai báo: ${applicantLevelName[item.level]}` : ''}</p>
        {item.message && <p className="team-request-message">Lời nhắn của bạn: {item.message}</p>}
        {item.status === 'ACCEPTED' && <><p>Bạn đã được nhận vào nhóm chơi.</p>
          <button type="button" onClick={() => setSelectedGame(post)}>Xem thông tin sân và liên hệ</button></>}
        {item.status === 'PENDING' && <button type="button" disabled={busy === item.id} onClick={() => void withdraw(item.id)}>Rút yêu cầu</button>}
      </article>)}
      {profile && <PersonProfileDialog person={profile} onClose={() => setProfile(null)} />}
      {selectedGame && <AcceptedGameDialog post={selectedGame} onClose={() => setSelectedGame(null)} />}
    </section></CustomerShell>;
}
