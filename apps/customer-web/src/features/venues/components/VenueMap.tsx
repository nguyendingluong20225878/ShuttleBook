import { useEffect, useRef, useState } from 'react';
import type { VenueSummary } from '../types';
import { navigate } from '../../../routes/navigation';

type MapInstance = { remove: () => void };
type MarkerInstance = { setLngLat: (position: [number, number]) => MarkerInstance;
  addTo: (map: MapInstance) => MarkerInstance; remove: () => void };
type MapSdk = {
  config: { apiKey: string };
  Map: new (options: { container: HTMLElement; style: string; center: [number, number]; zoom: number }) => MapInstance;
  Marker: new (options?: { element?: HTMLElement }) => MarkerInstance;
};
declare global { interface Window { maptilersdk?: MapSdk } }

const jsUrl = 'https://cdn.maptiler.com/maptiler-sdk-js/v4.1.0/maptiler-sdk.umd.min.js';
const cssUrl = 'https://cdn.maptiler.com/maptiler-sdk-js/v4.1.0/maptiler-sdk.css';
let loading: Promise<MapSdk> | null = null;

function loadSdk(): Promise<MapSdk> {
  if (window.maptilersdk) return Promise.resolve(window.maptilersdk);
  if (!loading) loading = new Promise<MapSdk>((resolve, reject) => {
    if (!document.querySelector(`link[href="${cssUrl}"]`)) {
      const css = document.createElement('link'); css.rel = 'stylesheet'; css.href = cssUrl;
      document.head.appendChild(css);
    }
    const script = document.createElement('script'); script.src = jsUrl; script.async = true;
    script.onload = () => window.maptilersdk ? resolve(window.maptilersdk) : reject(new Error('MapTiler SDK unavailable'));
    script.onerror = () => reject(new Error('MapTiler SDK unavailable'));
    document.head.appendChild(script);
  }).catch(error => { loading = null; throw error; });
  return loading;
}

export function VenueMap({ items, center }: { items: VenueSummary[]; center: { latitude: number; longitude: number } | null }) {
  const host = useRef<HTMLDivElement>(null);
  const [failed, setFailed] = useState(false);
  const key = import.meta.env.VITE_MAPTILER_API_KEY as string | undefined;

  useEffect(() => {
    if (!key || !host.current) return;
    let disposed = false;
    let map: MapInstance | null = null;
    const markers: MarkerInstance[] = [];
    void loadSdk().then(sdk => {
      if (disposed || !host.current) return;
      sdk.config.apiKey = key;
      const point: [number, number] = center ? [center.longitude, center.latitude]
        : items[0] ? [items[0].longitude, items[0].latitude] : [105.8342, 21.0278];
      map = new sdk.Map({ container: host.current, style: 'streets-v2', center: point, zoom: center ? 12 : 11 });
      for (const venue of items) {
        const button = document.createElement('button');
        button.className = 'map-marker'; button.type = 'button'; button.textContent = '●';
        button.title = `Xem ${venue.name}`; button.setAttribute('aria-label', `Xem ${venue.name}`);
        button.addEventListener('click', () => navigate(`/venues/${encodeURIComponent(venue.id)}`));
        markers.push(new sdk.Marker({ element: button })
          .setLngLat([venue.longitude, venue.latitude]).addTo(map));
      }
      setFailed(false);
    }).catch(() => { if (!disposed) setFailed(true); });
    return () => { disposed = true; markers.forEach(marker => marker.remove()); map?.remove(); };
  }, [key, items, center?.latitude, center?.longitude]);

  if (!key) return <p className="note">Bản đồ chưa được cấu hình; danh sách sân vẫn dùng được.</p>;
  return <div className="map-wrap">
    <div className="venue-map" ref={host} aria-label="Bản đồ các cơ sở" />
    {failed && <p role="alert">Không tải được bản đồ. Bạn vẫn có thể chọn sân trong danh sách.</p>}
  </div>;
}
