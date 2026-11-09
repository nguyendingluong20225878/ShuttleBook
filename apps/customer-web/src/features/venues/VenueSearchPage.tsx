import { FormEvent, useEffect, useRef, useState } from 'react';
import { CustomerShell } from '../../components/CustomerShell';
import { CustomerFeatureHero } from '../../components/CustomerFeatureHero';
import { navigate } from '../../routes/navigation';
import { getPublic } from './api';
import { VenuePhoto } from './components/VenuePhoto';
import { distanceLabel, estimatedDistanceMeters } from './distance';
import type { Page, VenueSummary } from './types';

type City = 'HN' | 'HCM';
type Position = { latitude: number; longitude: number };
type Suggestion = { place_name?: string; geometry?: { coordinates?: [number, number] }; center?: [number, number] };
type PriceFilter = { date: string; startsAt: string; durationMinutes: number; maxPriceVnd: number };
type PriceDraft = { date: string; startsAt: string; endsAt: string; maxPriceVnd: string };
const cityKey = 'shuttlebook.customer.city.v1';
const money = new Intl.NumberFormat('vi-VN');
function todayVietnam() {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
  const value = (name: string) => parts.find(part => part.type === name)?.value ?? '';
  return `${value('year')}-${value('month')}-${value('day')}`;
}
function defaultDraft(): PriceDraft { return { date: todayVietnam(), startsAt: '18:00', endsAt: '19:00', maxPriceVnd: '300000' }; }
function urlPriceFilter(): PriceFilter | null {
  const params = new URLSearchParams(location.search);
  const date = params.get('date') ?? '';
  const startsAt = params.get('startsAt') ?? '';
  const durationMinutes = Number(params.get('durationMinutes'));
  const maxPriceVnd = Number(params.get('maxPriceVnd'));
  const start = /^([01]\d|2[0-3]):(00|30)$/.test(startsAt) ? Number(startsAt.slice(0, 2)) * 60 + Number(startsAt.slice(3)) : -1;
  return /^\d{4}-\d{2}-\d{2}$/.test(date) && start >= 0 &&
    Number.isInteger(durationMinutes) && durationMinutes >= 30 && durationMinutes <= 480 && durationMinutes % 30 === 0 &&
    start + durationMinutes < 1440 && Number.isSafeInteger(maxPriceVnd) && maxPriceVnd >= 1 && maxPriceVnd <= 10_000_000
    ? { date, startsAt, durationMinutes, maxPriceVnd } : null;
}
function draftFromFilter(filter: PriceFilter | null): PriceDraft {
  return filter ? { date: filter.date, startsAt: filter.startsAt, endsAt: endTime(filter),
    maxPriceVnd: String(filter.maxPriceVnd) } : defaultDraft();
}
function searchUrl(city: City, q: string, filter: PriceFilter | null) {
  return `/venues?${new URLSearchParams({ city, ...(q ? { q } : {}), ...(filter ? {
    date: filter.date, startsAt: filter.startsAt, durationMinutes: String(filter.durationMinutes),
    maxPriceVnd: String(filter.maxPriceVnd) } : {}) })}`;
}
function endTime(filter: PriceFilter) {
  const minutes = Number(filter.startsAt.slice(0, 2)) * 60 + Number(filter.startsAt.slice(3)) + filter.durationMinutes;
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
}
const cityName = (city: City) => city === 'HN' ? 'Hà Nội' : 'TP.HCM';
function playDateLabel(value: string) {
  const [year, month, day] = value.split('-');
  return `${day}/${month}/${year}`;
}
function validCity(value: string | null): City | null { return value === 'HN' || value === 'HCM' ? value : null; }
function storedCity(): City | null {
  try { return validCity(localStorage.getItem(cityKey)); } catch { return null; }
}
function urlCity(): City | null { return validCity(new URLSearchParams(location.search).get('city')); }
function rememberCity(value: City) { try { localStorage.setItem(cityKey, value); } catch { /* Browsing still works. */ } }

export function VenueSearchPage() {
  const initialQuery = new URLSearchParams(location.search).get('q') ?? '';
  const [city, setCity] = useState<City | null>(() => urlCity() ?? storedCity());
  const [query, setQuery] = useState(initialQuery);
  const [activeQuery, setActiveQuery] = useState(initialQuery);
  const [priceApplied, setPriceApplied] = useState<PriceFilter | null>(() => urlPriceFilter());
  const [priceDraft, setPriceDraft] = useState<PriceDraft>(() => draftFromFilter(urlPriceFilter()));
  const [priceError, setPriceError] = useState('');
  const [area, setArea] = useState('');
  const [selectedArea, setSelectedArea] = useState('');
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [position, setPosition] = useState<Position | null>(null);
  const [currentPosition, setCurrentPosition] = useState<Position | null>(null);
  const [locating, setLocating] = useState(false);
  const generation = useRef(0);
  const priceDetails = useRef<HTMLDetailsElement | null>(null);
  const moreController = useRef<AbortController | null>(null);
  const locationAttempt = useRef(0);
  const mounted = useRef(true);
  const [radius, setRadius] = useState(5000);
  const [mode, setMode] = useState<'list' | 'nearby'>('list');
  const [items, setItems] = useState<VenueSummary[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [locationError, setLocationError] = useState('');
  const [retry, setRetry] = useState(0);
  const key = import.meta.env.VITE_MAPTILER_API_KEY as string | undefined;

  useEffect(() => {
    mounted.current = true;
    if (city) rememberCity(city);
    const restore = () => {
      const next = urlCity() ?? storedCity();
      setCity(next);
      const text = new URLSearchParams(location.search).get('q') ?? '';
      setQuery(text); setActiveQuery(text);
      const filter = urlPriceFilter(); setPriceApplied(filter); setPriceDraft(draftFromFilter(filter)); setPriceError('');
      setArea(''); setSelectedArea(''); setPosition(null); setMode('list');
      locationAttempt.current++;
      if (next) rememberCity(next);
    };
    window.addEventListener('popstate', restore);
    return () => { mounted.current = false; locationAttempt.current++; window.removeEventListener('popstate', restore); };
  }, []);

  function selectCity(next: City) {
    locationAttempt.current++; setLocating(false);
    setCity(next); rememberCity(next);
    setQuery(''); setActiveQuery(''); setArea(''); setSelectedArea(''); setSuggestions([]);
    setPriceApplied(null); setPriceDraft(defaultDraft()); setPriceError('');
    setPosition(null); setCurrentPosition(null); setMode('list'); setLocationError('');
    navigate(`/venues?city=${next}`);
  }

  useEffect(() => {
    const controller = new AbortController();
    const scope = ++generation.current;
    moreController.current?.abort(); moreController.current = null;
    setItems([]); setNextCursor(null); setError('');
    if (!city) { setLoading(false); return () => { controller.abort(); generation.current++; }; }
    setLoading(true);
    const path = mode === 'nearby' && position
      ? `/venues/nearby?${new URLSearchParams({ latitude: `${position.latitude}`, longitude: `${position.longitude}`,
          radiusMeters: `${radius}` })}`
      : priceApplied
        ? `/venues/price-search?${new URLSearchParams({ city, ...(activeQuery ? { q: activeQuery } : {}),
          date: priceApplied.date, startsAt: priceApplied.startsAt, durationMinutes: String(priceApplied.durationMinutes),
          maxPriceVnd: String(priceApplied.maxPriceVnd) })}`
      : `/venues?${new URLSearchParams({ city, ...(activeQuery ? { q: activeQuery } : {}) })}`;
    void getPublic<Page<VenueSummary>>(path, controller.signal)
      .then(data => { if (!controller.signal.aborted && generation.current === scope) { setItems(data.items); setNextCursor(data.nextCursor); } })
      .catch(reason => { if (!controller.signal.aborted && generation.current === scope)
        setError(reason instanceof Error ? reason.message : 'Không tải được sân.'); })
      .finally(() => { if (!controller.signal.aborted && generation.current === scope) setLoading(false); });
    return () => { controller.abort(); moreController.current?.abort(); generation.current++; };
  }, [city, mode, activeQuery, position?.latitude, position?.longitude, radius, retry,
    priceApplied?.date, priceApplied?.startsAt, priceApplied?.durationMinutes, priceApplied?.maxPriceVnd]);

  useEffect(() => {
    if (!key || area.trim().length < 3 || area === selectedArea) { setSuggestions([]); return; }
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      const params = new URLSearchParams({ key, country: 'vn', language: 'vi', autocomplete: 'true', limit: '5' });
      void fetch(`https://api.maptiler.com/geocoding/${encodeURIComponent(area.trim())}.json?${params}`,
        { signal: controller.signal })
        .then(response => { if (!response.ok) throw new Error(); return response.json(); })
        .then((body: { features?: Suggestion[] }) => setSuggestions((body.features ?? []).filter(s =>
          Boolean(s.place_name && (s.geometry?.coordinates || s.center)))))
        .catch(() => { if (!controller.signal.aborted) setLocationError('Không tìm được khu vực. Vui lòng thử địa chỉ khác.'); });
    }, 400);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [area, key, selectedArea]);

  function searchText(event: FormEvent) {
    event.preventDefault();
    if (!city) return;
    locationAttempt.current++; setLocating(false);
    setMode('list'); setActiveQuery(query.trim()); setPosition(null); setSelectedArea(''); setArea('');
    setRetry(value => value + 1);
    history.pushState(null, '', searchUrl(city, query.trim(), priceApplied));
  }

  function applyPrice(event: FormEvent) {
    event.preventDefault();
    if (!city) return;
    const { date, startsAt } = priceDraft;
    const finish = /^([01]\d|2[0-3]):(00|30)$/.test(priceDraft.endsAt)
      ? Number(priceDraft.endsAt.slice(0, 2)) * 60 + Number(priceDraft.endsAt.slice(3)) : -1;
    const maxPriceVnd = Number(priceDraft.maxPriceVnd);
    const start = /^([01]\d|2[0-3]):(00|30)$/.test(startsAt)
      ? Number(startsAt.slice(0, 2)) * 60 + Number(startsAt.slice(3)) : -1;
    const durationMinutes = finish - start;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || date < todayVietnam() || start < 0 || finish < 0 ||
      !Number.isInteger(durationMinutes) || durationMinutes < 30 || durationMinutes > 480 || durationMinutes % 30 !== 0 ||
      start + durationMinutes >= 1440 || !Number.isSafeInteger(maxPriceVnd) || maxPriceVnd < 1 || maxPriceVnd > 10_000_000) {
      setPriceError('Chọn ngày chưa qua; giờ kết thúc phải sau giờ bắt đầu 30 phút–8 giờ, theo bước 30 phút và trước 24:00. Ngân sách từ 1–10.000.000đ.'); return;
    }
    const filter = { date, startsAt, durationMinutes, maxPriceVnd };
    const nextQuery = query.trim();
    locationAttempt.current++; setLocating(false); setLocationError('');
    setPriceError(''); setPriceApplied(filter); setActiveQuery(nextQuery); setMode('list');
    setPosition(null); setSelectedArea(''); setArea('');
    if (priceDetails.current) priceDetails.current.open = false;
    history.pushState(null, '', searchUrl(city, nextQuery, filter));
    setRetry(value => value + 1);
  }

  function clearPrice() {
    if (!city) return;
    setPriceApplied(null); setPriceDraft(defaultDraft()); setPriceError('');
    if (priceDetails.current) priceDetails.current.open = false;
    history.pushState(null, '', searchUrl(city, activeQuery, null));
  }

  function editPrice() {
    if (!priceDetails.current) return;
    priceDetails.current.open = true;
    priceDetails.current.scrollIntoView({ block: 'start', behavior: 'smooth' });
    requestAnimationFrame(() => priceDetails.current?.querySelector<HTMLInputElement>('input[type="date"]')?.focus());
  }

  function useCurrentPosition() {
    setLocationError('');
    if (!navigator.geolocation) { setLocationError('Trình duyệt không hỗ trợ vị trí. Hãy tìm bằng tên hoặc địa chỉ.'); return; }
    const attempt = ++locationAttempt.current; setLocating(true);
    navigator.geolocation.getCurrentPosition(result => {
      if (!mounted.current || attempt !== locationAttempt.current) return;
      const point = { latitude: result.coords.latitude, longitude: result.coords.longitude };
      setPosition(point); setCurrentPosition(point); setLocating(false);
      setMode('nearby'); setSelectedArea(''); setArea('');
      setPriceApplied(null); setPriceDraft(defaultDraft());
      if (city && priceApplied) history.pushState(null, '', searchUrl(city, activeQuery, null));
    }, () => {
      if (!mounted.current || attempt !== locationAttempt.current) return;
      setLocating(false); setCurrentPosition(null);
      if (!selectedArea) { setPosition(null); setMode('list'); }
      setLocationError('Bạn chưa cấp quyền vị trí hoặc chưa lấy được vị trí hiện tại. Hãy tìm bằng tên hoặc địa chỉ.');
    }, { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 });
  }

  function selectArea(suggestion: Suggestion) {
    const point = suggestion.geometry?.coordinates ?? suggestion.center;
    if (!point || !suggestion.place_name) return;
    locationAttempt.current++; setLocating(false);
    setArea(suggestion.place_name); setSelectedArea(suggestion.place_name);
    setSuggestions([]); setLocationError(''); setPosition({ latitude: point[1], longitude: point[0] });
    setMode('nearby');
    setPriceApplied(null); setPriceDraft(defaultDraft());
    if (city && priceApplied) history.pushState(null, '', searchUrl(city, activeQuery, null));
  }

  async function loadMore() {
    if (!city || !nextCursor || loading || moreController.current) return;
    const scope = generation.current;
    const controller = new AbortController(); moreController.current = controller;
    setLoading(true); setError('');
    const params = mode === 'nearby' && position
      ? new URLSearchParams({ latitude: `${position.latitude}`, longitude: `${position.longitude}`,
          radiusMeters: `${radius}`, cursor: nextCursor })
      : priceApplied
        ? new URLSearchParams({ city, ...(activeQuery ? { q: activeQuery } : {}), date: priceApplied.date,
          startsAt: priceApplied.startsAt, durationMinutes: String(priceApplied.durationMinutes),
          maxPriceVnd: String(priceApplied.maxPriceVnd), cursor: nextCursor })
      : new URLSearchParams({ city, ...(activeQuery ? { q: activeQuery } : {}), cursor: nextCursor });
    try {
      const page = await getPublic<Page<VenueSummary>>(`/venues${mode === 'nearby' ? '/nearby' : priceApplied ? '/price-search' : ''}?${params}`, controller.signal);
      if (controller.signal.aborted || scope !== generation.current) return;
      setItems(previous => [...new Map([...previous, ...page.items].map(venue => [venue.id, venue])).values()]); setNextCursor(page.nextCursor);
    } catch (reason) { if (!controller.signal.aborted && scope === generation.current)
      setError(reason instanceof Error ? reason.message : 'Không tải được trang tiếp.'); }
    finally { if (moreController.current === controller && scope === generation.current) { moreController.current = null; setLoading(false); } }
  }

  function distance(venue: VenueSummary) {
    const serverDistance = Number.isFinite(venue.distanceMeters) && venue.distanceMeters! >= 0 ? venue.distanceMeters! : null;
    if (currentPosition) return mode === 'nearby' && !selectedArea && serverDistance !== null
      ? serverDistance : estimatedDistanceMeters(currentPosition, venue);
    return mode === 'nearby' && selectedArea ? serverDistance : null;
  }

  return <CustomerShell className="customer-discovery">
    {!city ? <section className="city-choice" aria-labelledby="city-choice-title">
      <p className="eyebrow">CHỌN NƠI BẠN MUỐN CHƠI</p>
      <h1 id="city-choice-title">Tìm sân cầu lông ở đâu?</h1>
      <div className="city-choice-grid">
        <button type="button" onClick={() => selectCity('HN')}><span>Hà Nội</span><small>Xem sân tại Hà Nội</small><b aria-hidden="true">→</b></button>
        <button type="button" onClick={() => selectCity('HCM')}><span>TP.HCM</span><small>Xem sân tại TP.HCM</small><b aria-hidden="true">→</b></button>
      </div>
    </section> : <>
      <CustomerFeatureHero label="TÌM SÂN" title="Tìm sân, chọn giờ chơi." />
      <section className="search-panel" aria-label="Tìm cơ sở">
        <form onSubmit={searchText}><label htmlFor="venue-query">Tên sân hoặc địa chỉ</label>
          <div className="input-row"><input id="venue-query" value={query} onChange={event => setQuery(event.target.value)}
            placeholder={`Tìm tại ${cityName(city)}`} />
            <label className="city-switch"><span>Đang xem tại</span><select aria-label="Đổi thành phố" value={city}
              onChange={event => selectCity(event.target.value as City)}><option value="HN">Hà Nội</option><option value="HCM">TP.HCM</option></select>
            </label><button type="submit">Tìm sân</button></div></form>
        <details className="location-options"><summary>Tìm quanh vị trí khác</summary>
          <div className="area-controls"><button type="button" disabled={locating} onClick={useCurrentPosition}>Dùng vị trí của tôi</button>
            {key && <div className="area-input"><label htmlFor="area-query">Nhập khu vực tìm kiếm</label>
              <input id="area-query" autoComplete="off" value={area} onChange={event => {
                setArea(event.target.value); setSelectedArea(''); setSuggestions([]); setLocationError('');
              }} placeholder="Nhập quận, phố hoặc địa điểm" />
              {suggestions.length > 0 && <ul className="suggestions" aria-label="Gợi ý khu vực">
                {suggestions.map((suggestion, index) => <li key={`${suggestion.place_name}-${index}`}>
                  <button type="button" onClick={() => selectArea(suggestion)}>{suggestion.place_name}</button></li>)}
              </ul>}</div>}
            <div className="radius-control"><label htmlFor="radius">Bán kính</label><select id="radius" value={radius}
              onChange={event => setRadius(Number(event.target.value))}><option value={3000}>3 km</option>
              <option value={5000}>5 km</option><option value={10000}>10 km</option>
              <option value={20000}>20 km</option></select></div></div>
          <p className="distance-note">Khoảng cách theo đường thẳng, không phải quãng đường di chuyển.</p>
        </details>
        {locationError && <p className="inline-feedback" role="alert">{locationError}</p>}
        {locating && <p className="results-summary" role="status">Đang lấy vị trí của bạn…</p>}
      </section>
      {mode === 'list' && <section className="price-filter" aria-label="Lọc theo lịch và ngân sách">
        <details ref={priceDetails}><summary><span>Ngày, giờ và ngân sách</span>
          {priceApplied && <small>Đang áp dụng</small>}
        </summary>
          <form onSubmit={applyPrice} className="price-filter-form">
            <label>Ngày chơi<input type="date" min={todayVietnam()} value={priceDraft.date}
              onChange={event => setPriceDraft(previous => ({ ...previous, date: event.target.value }))} required /></label>
            <label>Giờ bắt đầu<input type="time" step="1800" value={priceDraft.startsAt}
              onChange={event => setPriceDraft(previous => ({ ...previous, startsAt: event.target.value }))} required /></label>
            <label>Giờ kết thúc<input type="time" step="1800" value={priceDraft.endsAt}
              onChange={event => setPriceDraft(previous => ({ ...previous, endsAt: event.target.value }))} required /></label>
            <label>Ngân sách tối đa (đ)<input type="number" min="1" max="10000000" step="1"
              value={priceDraft.maxPriceVnd} onChange={event => setPriceDraft(previous => ({ ...previous, maxPriceVnd: event.target.value }))} required /></label>
            <div className="price-filter-actions"><button type="submit">Tìm sân phù hợp</button>
              {priceApplied && <button type="button" onClick={clearPrice}>Xóa bộ lọc</button>}</div>
          </form>
          {priceError && <p role="alert">{priceError}</p>}
          <p className="price-filter-note">Giá là tổng dự kiến cho một lượt chơi. Sân trống và giá được kiểm tra lại khi báo giá.</p>
        </details>
      </section>}
      <section className="results-layout" aria-label="Danh sách cơ sở"><div className="section-heading"><div>
        <h2>{mode === 'nearby' ? 'Sân gần khu vực đã chọn' : priceApplied ? 'Sân phù hợp lịch và ngân sách' : 'Danh sách cơ sở'}</h2>
        {!loading && !error && <p className="results-summary">{items.length} cơ sở đã tải{mode === 'list' ? ` tại ${cityName(city)}` : ''}.</p>}
      </div>{loading && <span className="loading-label" role="status">Đang tải…</span>}</div>
        {mode === 'list' && priceApplied && <div className="active-price-filter" aria-label="Bộ lọc ngày, giờ và ngân sách đang áp dụng">
          <span>Ngày {playDateLabel(priceApplied.date)}</span>
          <span>{priceApplied.startsAt}–{endTime(priceApplied)}</span>
          <span>Tối đa {money.format(priceApplied.maxPriceVnd)}đ</span>
          <button type="button" onClick={editPrice}>Sửa lọc</button>
          <button type="button" onClick={clearPrice} aria-label="Xóa bộ lọc ngày, giờ và ngân sách">Xóa lọc</button>
        </div>}
        {error && <div className="discovery-feedback" role="alert"><p>{error}</p><button type="button" onClick={() => setRetry(value => value + 1)}>Thử lại</button></div>}
        {!loading && !error && items.length === 0 && <div className="empty-state"><h3>Chưa tìm thấy cơ sở phù hợp</h3>
          <p>{priceApplied ? 'Thử giờ khác, tăng ngân sách hoặc xóa bộ lọc. Giá và sân trống có thể thay đổi.' :
            'Thử tên khác hoặc đổi thành phố. Cơ sở mới chỉ xuất hiện sau khi được công bố.'}</p>
          {priceApplied ? <div className="empty-state-actions">
            <button type="button" onClick={editPrice}>Đổi giờ/ngân sách</button>
            <button type="button" onClick={clearPrice}>Xóa bộ lọc</button>
          </div> : <button type="button" onClick={() => document.getElementById('venue-query')?.focus()}>Đổi tìm kiếm</button>}</div>}
        {loading && items.length === 0 && <div className="venue-list" aria-hidden="true">{[1, 2, 3, 4, 5, 6].map(n =>
          <div className="venue-card venue-card-skeleton" key={n}><div /><div /></div>)}</div>}
        <div className="venue-list">{items.map(venue => { const meters = distance(venue); return <article className="venue-card" key={venue.id}>
          <VenuePhoto imageUrl={venue.imageUrl} name={venue.name} />
          <div className="venue-card-content"><h3>{venue.name}</h3><p className="venue-address">{venue.address}</p>
            {meters !== null && <p className="distance">Cách {currentPosition ? 'vị trí của bạn' : 'khu vực đã chọn'} khoảng {distanceLabel(meters)}</p>}
            {priceApplied && venue.estimatedTotalVnd !== undefined && <p className="price-estimate">
              <strong>Ước tính {money.format(venue.estimatedTotalVnd)}đ</strong>
              <span>{playDateLabel(priceApplied.date)} · {priceApplied.startsAt}–{endTime(priceApplied)}</span>
              <small>Tham khảo lúc tìm; báo giá sẽ được tính lại khi đặt sân.</small></p>}
            <a className="card-action" href={`/venues/${venue.id}${priceApplied ? `?${new URLSearchParams({
              date: priceApplied.date, startsAt: priceApplied.startsAt, durationMinutes: String(priceApplied.durationMinutes),
              suggestedCourtId: venue.matchedCourtId ?? '' })}` : ''}`}>Xem lịch sân <span aria-hidden="true">→</span></a></div>
        </article>; })}</div>
        {nextCursor && <button className="load-more" type="button" disabled={loading} onClick={() => void loadMore()}>Xem thêm cơ sở</button>}
      </section>
    </>}
  </CustomerShell>;
}
