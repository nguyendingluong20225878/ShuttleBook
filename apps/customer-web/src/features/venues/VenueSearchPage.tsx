import { FormEvent, useEffect, useState } from 'react';
import { getPublic } from './api';
import { VenueMap } from './components/VenueMap';
import { VenuePhoto } from './components/VenuePhoto';
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
    const controller = new AbortController();
    setLoading(true); setError('');
    const path = mode === 'nearby' && position
      ? `/venues/nearby?${new URLSearchParams({ latitude: `${position.latitude}`, longitude: `${position.longitude}`,
          radiusMeters: `${radius}` })}`
      : `/venues?${new URLSearchParams({ q: activeQuery })}`;
    void getPublic<Page<VenueSummary>>(path, controller.signal)
      .then(data => { setItems(data.items); setNextCursor(data.nextCursor); })
      .catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'Không tải được sân.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
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
        .catch(() => { if (!controller.signal.aborted) setLocationError('Không tìm được khu vực trên bản đồ.'); });
    }, 400);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [area, key, selectedArea]);

  function searchText(event: FormEvent) {
    event.preventDefault();
    setMode('list'); setActiveQuery(query.trim()); setPosition(null);
    setRetry(value => value + 1);
    const params = query.trim() ? `?q=${encodeURIComponent(query.trim())}` : '';
    history.replaceState(null, '', `/venues${params}`);
  }

  function useCurrentPosition() {
    setLocationError('');
    if (!navigator.geolocation) { setLocationError('Trình duyệt không hỗ trợ vị trí. Hãy tìm bằng tên hoặc địa chỉ.'); return; }
    navigator.geolocation.getCurrentPosition(result => {
      setPosition({ latitude: result.coords.latitude, longitude: result.coords.longitude });
      setMode('nearby'); setSelectedArea(''); setArea('');
    }, () => setLocationError('Bạn chưa cấp quyền vị trí. Hãy tìm bằng tên hoặc địa chỉ.'),
    { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 });
  }

  function selectArea(suggestion: Suggestion) {
    const point = suggestion.geometry?.coordinates ?? suggestion.center;
    if (!point || !suggestion.place_name) return;
    setArea(suggestion.place_name); setSelectedArea(suggestion.place_name);
    setSuggestions([]); setLocationError(''); setPosition({ latitude: point[1], longitude: point[0] });
    setMode('nearby');
  }

  async function loadMore() {
    if (!nextCursor || loading) return;
    setLoading(true); setError('');
    const params = mode === 'nearby' && position
      ? new URLSearchParams({ latitude: `${position.latitude}`, longitude: `${position.longitude}`,
          radiusMeters: `${radius}`, cursor: nextCursor })
      : new URLSearchParams({ q: activeQuery, cursor: nextCursor });
    try {
      const page = await getPublic<Page<VenueSummary>>(`/venues${mode === 'nearby' ? '/nearby' : ''}?${params}`);
      setItems(previous => [...previous, ...page.items]); setNextCursor(page.nextCursor);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Không tải được trang tiếp.'); }
    finally { setLoading(false); }
  }

  return <main className="customer-shell">
    <header className="site-header"><a className="brand" href="/venues">ShuttleBook</a>
      <nav><a href="/">Đăng ký</a><a href="/login">Đăng nhập</a></nav></header>
    <section className="hero"><p className="eyebrow">Tìm sân cầu lông</p><h1>Chọn cơ sở phù hợp với bạn</h1>
      <p>Xem vị trí, các sân trong cơ sở và lịch trống theo từng ca 30 phút.</p></section>
    <section className="search-panel" aria-label="Tìm cơ sở">
      <form onSubmit={searchText}><label htmlFor="venue-query">Tên sân hoặc địa chỉ</label>
        <div className="input-row"><input id="venue-query" value={query} onChange={event => setQuery(event.target.value)}
          placeholder="Ví dụ: Hoàng Cầu, Hà Nội" /><button type="submit">Tìm sân</button></div></form>
      <div className="search-divider">hoặc tìm quanh một khu vực</div>
      <div className="area-controls"><button type="button" onClick={useCurrentPosition}>Dùng vị trí của tôi</button>
        {key && <div className="area-input"><label htmlFor="area-query">Nhập khu vực trên bản đồ</label>
          <input id="area-query" autoComplete="off" value={area} onChange={event => {
            setArea(event.target.value); setSelectedArea(''); setSuggestions([]); setLocationError('');
          }} placeholder="Nhập quận, phố hoặc địa điểm" />
          {suggestions.length > 0 && <ul className="suggestions" aria-label="Gợi ý khu vực">
            {suggestions.map((suggestion, index) => <li key={`${suggestion.place_name}-${index}`}>
              <button type="button" onClick={() => selectArea(suggestion)}>{suggestion.place_name}</button></li>)}
          </ul>}</div>}
        <label htmlFor="radius">Bán kính</label><select id="radius" value={radius}
          onChange={event => setRadius(Number(event.target.value))}><option value={3000}>3 km</option>
          <option value={5000}>5 km</option><option value={10000}>10 km</option>
          <option value={20000}>20 km</option></select></div>
      {locationError && <p role="alert">{locationError}</p>}
    </section>
    <div className="results-layout"><section aria-label="Danh sách cơ sở">
      <div className="section-heading"><h2>{mode === 'nearby' ? 'Sân gần khu vực đã chọn' : 'Danh sách cơ sở'}</h2>
        {loading && <span role="status">Đang tải…</span>}</div>
      {error && <p role="alert">{error} <button type="button" onClick={() => setRetry(value => value + 1)}>Thử lại</button></p>}
      {!loading && !error && items.length === 0 && <p>Chưa có cơ sở phù hợp. Hãy thử tên hoặc bán kính khác.</p>}
      <div className="venue-list">{items.map(venue => <article className="venue-card" key={venue.id}>
        <VenuePhoto imageUrl={venue.imageUrl} name={venue.name} />
        <div><h3><a href={`/venues/${venue.id}`}>{venue.name}</a></h3><p>{venue.address}</p>
          {venue.distanceMeters !== undefined && <p className="distance">Cách khoảng {(venue.distanceMeters / 1000).toFixed(1)} km</p>}
          <a className="text-link" href={`/venues/${venue.id}`}>Xem lịch các sân →</a></div>
      </article>)}</div>
      {nextCursor && <button type="button" disabled={loading} onClick={() => void loadMore()}>Xem thêm cơ sở</button>}
    </section><aside aria-label="Bản đồ cơ sở"><VenueMap items={items} center={position} /></aside></div>
  </main>;
}
