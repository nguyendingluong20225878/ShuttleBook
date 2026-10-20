/** Keep notification actions bound to bookingId while hiding long generated references in prose. */
export function noticeText(value: string): string {
  return value
    .replace(/\b(?:Đơn\s+)?BK\d{6}[0-9a-f]{32}\b/gi, 'Đơn đặt sân')
    .replace(/\bSR\d{6}[0-9a-f]{32}\b/gi, 'Lịch cố định');
}
