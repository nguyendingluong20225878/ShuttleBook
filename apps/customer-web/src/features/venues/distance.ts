type Point = { latitude: number; longitude: number };

/** Display estimate only; nearby filtering remains authoritative in PostGIS. */
export function estimatedDistanceMeters(from: Point, to: Point): number | null {
  const valid = (point: Point) => Number.isFinite(point.latitude) && Number.isFinite(point.longitude) &&
    Math.abs(point.latitude) <= 90 && Math.abs(point.longitude) <= 180;
  if (!valid(from) || !valid(to)) return null;
  const radians = (degrees: number) => degrees * Math.PI / 180;
  const deltaLatitude = radians(to.latitude - from.latitude);
  const deltaLongitude = radians(to.longitude - from.longitude);
  const square = Math.sin(deltaLatitude / 2) ** 2 + Math.cos(radians(from.latitude)) *
    Math.cos(radians(to.latitude)) * Math.sin(deltaLongitude / 2) ** 2;
  return 6371008.8 * 2 * Math.asin(Math.sqrt(Math.min(1, Math.max(0, square))));
}

export function distanceLabel(meters: number): string {
  return meters < 1000 ? `${Math.round(meters)} m` :
    `${new Intl.NumberFormat('vi-VN', { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(meters / 1000)} km`;
}
