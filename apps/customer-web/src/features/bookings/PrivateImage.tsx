import { useEffect, useState } from 'react';
import { useCustomerSession } from '../auth/CustomerSession';

export function PrivateImage({ path, alt, className }: { path: string; alt: string; className?: string }) {
  const { request } = useCustomerSession(); const [src, setSrc] = useState(''); const [error, setError] = useState(false);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController(); let objectUrl = ''; setSrc(''); setError(false);
    // Private images are fetched through the authenticated API; never navigate to an arbitrary URL.
    if (!/^\/api\/v1\/(?:bookings\/[a-f0-9-]+\/qr|uploads\/[a-f0-9-]+\/view)$/.test(path)) { setError(true); return; }
    void request(path, { signal: controller.signal }).then(async response => {
      if (!response.ok) throw new Error('IMAGE_UNAVAILABLE');
      objectUrl = URL.createObjectURL(await response.blob());
      if (!controller.signal.aborted) setSrc(objectUrl); else URL.revokeObjectURL(objectUrl);
    }).catch(() => { if (!controller.signal.aborted) setError(true); });
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [path, request, revision]);
  return <div>{src ? <img src={src} alt={alt} className={className} /> : error ? <p role="status">Chưa tải được ảnh.
    <button type="button" onClick={() => setRevision(value => value + 1)}>Tải lại ảnh</button></p> : <p role="status">Đang tải ảnh…</p>}</div>;
}
