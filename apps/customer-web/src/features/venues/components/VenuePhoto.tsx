import { useEffect, useState } from 'react';
import { publicImageUrl } from '../api';

export function VenuePhoto({ imageUrl, name }: { imageUrl: string | null; name: string }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [imageUrl]);
  const src = publicImageUrl(imageUrl);
  return src && !failed ? <img src={src} alt={`Ảnh ${name}`} loading="lazy" onError={() => setFailed(true)} />
    : <img className="venue-photo-fallback" src="/venue-default.svg" alt={`Hình minh họa sân cầu lông; ${name} chưa có ảnh cơ sở`} loading="lazy" />;
}
