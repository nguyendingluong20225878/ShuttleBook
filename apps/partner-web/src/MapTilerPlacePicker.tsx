import { useEffect, useId, useRef, useState } from 'react';

export type VenueLocation = { address: string; latitude: number; longitude: number };
type MapTilerMap = {
  setCenter: (center: [number, number]) => void;
  setZoom: (zoom: number) => void;
  remove: () => void;
  resize?: () => void;
};
type MapTilerMarker = {
  setLngLat: (position: [number, number]) => MapTilerMarker;
  addTo: (map: MapTilerMap) => MapTilerMarker;
  remove: () => void;
};
type MapTilerSdk = {
  config: { apiKey: string };
  Map: new (options: { container: HTMLElement; style: string; center: [number, number]; zoom: number }) => MapTilerMap;
  Marker: new () => MapTilerMarker;
};
type GeocodingFeature = { place_name?: string; matching_place_name?: string;
  geometry?: { coordinates?: [number, number] }; center?: [number, number]; relevance?: number };
declare global { interface Window { maptilersdk?: MapTilerSdk } }

const sdkUrl = 'https://cdn.maptiler.com/maptiler-sdk-js/v4.1.0/maptiler-sdk.umd.min.js';
const sdkCssUrl = 'https://cdn.maptiler.com/maptiler-sdk-js/v4.1.0/maptiler-sdk.css';
let sdkPromise: Promise<MapTilerSdk> | null = null;

function loadMapTiler(): Promise<MapTilerSdk> {
  if (window.maptilersdk) return Promise.resolve(window.maptilersdk);
  if (!sdkPromise) sdkPromise = new Promise<MapTilerSdk>((resolve, reject) => {
    if (!document.querySelector(`link[href="${sdkCssUrl}"]`)) {
      const stylesheet = document.createElement('link');
      stylesheet.rel = 'stylesheet'; stylesheet.href = sdkCssUrl;
      document.head.appendChild(stylesheet);
    }
    const script = document.createElement('script');
    script.src = sdkUrl; script.async = true;
    script.onload = () => window.maptilersdk ? resolve(window.maptilersdk) : reject(new Error('MapTiler SDK không khả dụng.'));
    script.onerror = () => reject(new Error('Không tải được MapTiler SDK.'));
    document.head.appendChild(script);
  }).catch(error => { sdkPromise = null; throw error; });
  return sdkPromise!;
}

export function MapTilerPlacePicker({ initial, onChange }: {
  initial?: VenueLocation | null; onChange?: (location: VenueLocation | null) => void }) {
  const [location, setLocation] = useState<VenueLocation | null>(initial ?? null);
  const addressId = useId();
  const [input, setInput] = useState('');
  const [suggestions, setSuggestions] = useState<GeocodingFeature[]>([]);
  const [candidate, setCandidate] = useState<VenueLocation | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const mapHost = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MapTilerMap | null>(null);
  const markerRef = useRef<MapTilerMarker | null>(null);
  const apiKey = import.meta.env.VITE_MAPTILER_API_KEY as string | undefined;
  const enabled = Boolean(apiKey);

  useEffect(() => {
    if (!enabled || !mapHost.current) return;
    let cancelled = false;
    const observer = new ResizeObserver(() => {
      if (mapHost.current?.clientWidth) mapRef.current?.resize?.();
    });
    observer.observe(mapHost.current);
    void loadMapTiler().then(sdk => {
      if (cancelled || !mapHost.current) return;
      sdk.config.apiKey = apiKey!;
      const center: [number, number] = initial
        ? [initial.longitude, initial.latitude] : [105.8342, 21.0278];
      const map = new sdk.Map({ container: mapHost.current, style: 'streets-v2', center,
        zoom: initial ? 16 : 11 });
      const marker = new sdk.Marker().setLngLat(center).addTo(map);
      mapRef.current = map; markerRef.current = marker;
    }).catch(() => { if (!cancelled) setError('Không tải được bản đồ MapTiler. Kiểm tra API key và kết nối mạng.'); });
    return () => {
      cancelled = true;
      observer.disconnect();
      markerRef.current?.remove(); markerRef.current = null;
      mapRef.current?.remove(); mapRef.current = null;
    };
  }, [apiKey, enabled, initial?.latitude, initial?.longitude]);

  useEffect(() => {
    if (!enabled || location || input.trim().length < 4) { setSuggestions([]); setLoading(false); return; }
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      setLoading(true); setError('');
      const query = new URLSearchParams({ key: apiKey!, autocomplete: 'true', country: 'vn',
        language: 'vi,en', limit: '5', proximity: '105.8342,21.0278' });
      const path = encodeURIComponent(input.trim());
      void fetch(`https://api.maptiler.com/geocoding/${path}.json?${query}`, { signal: controller.signal })
        .then(async response => {
          if (!response.ok) throw new Error('MapTiler không thể tìm địa chỉ lúc này.');
          return await response.json() as { features?: GeocodingFeature[] };
        })
        .then(data => setSuggestions((data.features ?? []).filter(feature =>
          (feature.place_name || feature.matching_place_name) && (feature.geometry?.coordinates || feature.center))))
        .catch(reason => { if (!controller.signal.aborted) {
          setSuggestions([]); setError(reason instanceof Error ? reason.message : 'Không tìm được địa chỉ.');
        } }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    }, 400);
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [apiKey, enabled, input, location]);

  function selectSuggestion(feature: GeocodingFeature) {
    const coordinates = feature.geometry?.coordinates ?? feature.center;
    const address = feature.place_name || feature.matching_place_name;
    if (!coordinates || !address || !Number.isFinite(coordinates[0]) || !Number.isFinite(coordinates[1])) {
      setError('MapTiler không trả về địa chỉ hoặc tọa độ hợp lệ.'); return;
    }
    const selected = { address, longitude: coordinates[0], latitude: coordinates[1] };
    setCandidate(selected); setSuggestions([]); setError('');
    mapRef.current?.setCenter([selected.longitude, selected.latitude]);
    mapRef.current?.setZoom(17);
    markerRef.current?.setLngLat([selected.longitude, selected.latitude]);
  }

  return <div className="map-picker">
    <label htmlFor={addressId}>Địa chỉ cơ sở trên MapTiler</label>
    {enabled ? <>
      <input id={addressId} value={input} autoComplete="off"
        placeholder="Nhập địa chỉ, ví dụ: 31 ngõ 16 Hoàng Cầu - Hà Nội"
        onChange={event => {
          setInput(event.target.value); setLocation(null); setCandidate(null); setError('');
          setSuggestions([]); onChange?.(null);
        }} />
      {loading && <p role="status">Đang tìm địa chỉ…</p>}
      {suggestions.length > 0 && <ul aria-label="Gợi ý địa chỉ MapTiler">{suggestions.map((feature, index) => {
        const address = feature.place_name || feature.matching_place_name!;
        return <li key={`${address}-${index}`}><button type="button" onClick={() => selectSuggestion(feature)}>{address}</button></li>;
      })}</ul>}
      <div ref={mapHost} className="map-canvas" aria-label="Bản đồ vị trí cơ sở" />
      {candidate && <p>Vị trí tìm thấy: {candidate.address} <button type="button" onClick={() => {
        setLocation(candidate); setInput(candidate.address); onChange?.(candidate); setCandidate(null);
      }}>Xác nhận vị trí này</button></p>}
      {location && <p>Địa chỉ đã xác nhận: {location.address}</p>}
    </> : <p role="alert">Cần cấu hình MapTiler API key để tìm, xác nhận địa chỉ trên bản đồ.</p>}
    {error && <p role="alert">{error}</p>}
    <input type="hidden" name="address" value={location?.address ?? ''} readOnly />
    <input type="hidden" name="latitude" value={location?.latitude ?? ''} readOnly />
    <input type="hidden" name="longitude" value={location?.longitude ?? ''} readOnly />
  </div>;
}
