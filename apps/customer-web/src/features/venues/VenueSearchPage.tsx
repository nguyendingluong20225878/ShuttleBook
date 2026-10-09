import { FormEvent, useEffect, useRef, useState } from 'react';
import { CustomerShell } from '../../components/CustomerShell';
import { getPublic } from './api';
import { VenuePhoto } from './components/VenuePhoto';
import { distanceLabel, estimatedDistanceMeters } from './distance';
import type { Page, VenueSummary } from './types';

type Position = { latitude: number; longitude: number };
type Suggestion = { place_name?: string; geometry?: { coordinates?: [number, number] }; center?: [number, number] };

export function VenueSearchPage() {
  const initialQuery = new URLSearchParams(location.search).get('q') ?? '';
  const [query, setQuery] = useState(initialQuery);
  const [activeQuery, setActiveQuery] = useState(initialQuery);
  const [area, setArea] = useState('');
  const [selectedArea, setSelectedArea] = useState('');
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [position, setPosition] = useState<Position | null>(null);
  const [currentPosition, setCurrentPosition] = useState<Position | null>(null);
  const [locating, setLocating] = useState(false);
  const generation = useRef(0);
  const moreController = useRef<AbortController | null>(null);
  const locationAttempt = useRef(0);
  const mounted = useRef(true);
  const [radius, setRadius] = useState(5000);
  const [mode, setMode] = useState<'list' | 'nearby'>('list');
  const [items, setItems] = useState<VenueSummary[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [locationError, setLocationError] = useState('');
  const [retry, setRetry] = useState(0);
  const key = import.meta.env.VITE_MAPTILER_API_KEY as string | undefined;

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; locationAttempt.current++; };
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    const scope = ++generation.current;
    moreController.current?.abort(); moreController.current = null;
    setLoading(true); setError(''); setItems([]); setNextCursor(null);
    const path = mode === 'nearby' && position
      ? `/venues/nearby?${new URLSearchParams({ latitude: `${position.latitude}`, longitude: `${position.longitude}`,
          radiusMeters: `${radius}` })}`
      : `/venues?${new URLSearchParams({ q: activeQuery })}`;
    void getPublic<Page<VenueSummary>>(path, controller.signal)
      .then(data => { if (!controller.signal.aborted && generation.current === scope) { setItems(data.items); setNextCursor(data.nextCursor); } })
      .catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'Không tải được sân.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => { controller.abort(); moreController.current?.abort(); generation.current++; };
  }, [mode, activeQuery, position?.latitude, position?.longitude, radius, retry]);

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
    locationAttempt.current++; setLocating(false);
    setMode('list'); setActiveQuery(query.trim()); setPosition(null);
    setRetry(value => value + 1);
    const params = query.trim() ? `?q=${encodeURIComponent(query.trim())}` : '';
    history.replaceState(null, '', `/venues${params}`);
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
    }, () => {
      if (!mounted.current || attempt !== locationAttempt.current) return;
      setLocating(false); setCurrentPosition(null);
      if (!selectedArea) { setPosition(null); setMode('list'); }
      setLocationError('Bạn chưa cấp quyền vị trí hoặc chưa lấy được vị trí hiện tại. Hãy tìm bằng tên hoặc địa chỉ.');
    },
    { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 });
  }

  function selectArea(suggestion: Suggestion) {
    const point = suggestion.geometry?.coordinates ?? suggestion.center;
    if (!point || !suggestion.place_name) return;
    locationAttempt.current++; setLocating(false);
    setArea(suggestion.place_name); setSelectedArea(suggestion.place_name);
    setSuggestions([]); setLocationError(''); setPosition({ latitude: point[1], longitude: point[0] });
    setMode('nearby');
  }

  async function loadMore() {
    if (!nextCursor || loading || moreController.current) return;
    const scope = generation.current;
    const controller = new AbortController(); moreController.current = controller;
    setLoading(true); setError('');
    const params = mode === 'nearby' && position
      ? new URLSearchParams({ latitude: `${position.latitude}`, longitude: `${position.longitude}`,
          radiusMeters: `${radius}`, cursor: nextCursor })
      : new URLSearchParams({ q: activeQuery, cursor: nextCursor });
    try {
      const page = await getPublic<Page<VenueSummary>>(`/venues${mode === 'nearby' ? '/nearby' : ''}?${params}`, controller.signal);
      if (controller.signal.aborted || scope !== generation.current) return;
      setItems(previous => [...new Map([...previous, ...page.items].map(venue => [venue.id, venue])).values()]); setNextCursor(page.nextCursor);
    } catch (reason) { if (!controller.signal.aborted && scope === generation.current) setError(reason instanceof Error ? reason.message : 'Không tải được trang tiếp.'); }
    finally { if (moreController.current === controller && scope === generation.current) { moreController.current = null; setLoading(false); } }
  }

  function distance(venue: VenueSummary) {
    const serverDistance = Number.isFinite(venue.distanceMeters) && venue.distanceMeters! >= 0 ? venue.distanceMeters! : null;
    if (currentPosition) return mode === 'nearby' && !selectedArea && serverDistance !== null
      ? serverDistance : estimatedDistanceMeters(currentPosition, venue);
    return mode === 'nearby' && selectedArea ? serverDistance : null;
  }

  return <CustomerShell className="customer-discovery">
    <section className="hero"><p className="eyebrow">Tìm sân cầu lông</p><h1>Chọn cơ sở phù hợp với bạn</h1>
      <p>Xem vị trí, các sân trong cơ sở và lịch trống theo từng ca 30 phút.</p>
      <div className="discovery-steps" aria-label="Các bước chọn sân"><span>1. Chọn cơ sở</span><span>2. Chọn sân &amp; giờ</span><span>3. Xem báo giá</span></div></section>
    <section className="search-panel" aria-label="Tìm cơ sở">
      <form onSubmit={searchText}><label htmlFor="venue-query">Tên sân hoặc địa chỉ</label>
        <div className="input-row"><input id="venue-query" value={query} onChange={event => setQuery(event.target.value)}
          placeholder="Ví dụ: Hoàng Cầu, Hà Nội" /><button type="submit">Tìm sân</button></div></form>
      <div className="search-divider"><span>Hoặc tìm quanh một khu vực</span></div>
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
      {locationError && <p className="inline-feedback" role="alert">{locationError}</p>}
      {locating && <p className="results-summary" role="status">Đang lấy vị trí của bạn…</p>}
      <p className="distance-note">{currentPosition || position
        ? 'Khoảng cách là ước lượng theo đường thẳng, không phải quãng đường di chuyển.'
        : 'Bấm Dùng vị trí của tôi để xem khoảng cách từ bạn đến từng cơ sở.'}</p>
    </section>
    <div className="results-layout"><section aria-label="Danh sách cơ sở">
      <div className="section-heading"><div><h2>{mode === 'nearby' ? 'Sân gần khu vực đã chọn' : 'Danh sách cơ sở'}</h2>
        {!loading && !error && <p className="results-summary">Đã tải {items.length} cơ sở{activeQuery && mode === 'list' ? ` cho “${activeQuery}”` : ''}.</p>}</div>
        {loading && <span className="loading-label" role="status">Đang tải…</span>}</div>
      {error && <div className="discovery-feedback" role="alert"><p>{error}</p><button type="button" onClick={() => setRetry(value => value + 1)}>Thử lại</button></div>}
      {!loading && !error && items.length === 0 && <div className="empty-state"><h3>Chưa tìm thấy cơ sở phù hợp</h3><p>Chưa có cơ sở phù hợp. Hãy thử tên hoặc bán kính khác.</p>
        <button type="button" onClick={() => document.getElementById('venue-query')?.focus()}>Đổi tìm kiếm</button></div>}
      <div className="venue-list">{items.map(venue => { const meters = distance(venue); return <article className="venue-card" key={venue.id}>
        <VenuePhoto imageUrl={venue.imageUrl} name={venue.name} />
        <div className="venue-card-content"><p className="venue-card-kind">Cơ sở cầu lông</p><h3><a href={`/venues/${venue.id}`}>{venue.name}</a></h3><p className="venue-address">{venue.address}</p>
          {meters !== null && <p className="distance">Cách {currentPosition ? 'vị trí của bạn' : 'khu vực đã chọn'} khoảng {distanceLabel(meters)}</p>}
          <a className="text-link" href={`/venues/${venue.id}`}>Xem lịch các sân →</a></div>
      </article>; })}</div>
      {nextCursor && <button className="load-more" type="button" disabled={loading} onClick={() => void loadMore()}>Xem thêm cơ sở</button>}
    </section></div>
  </CustomerShell>;
}
