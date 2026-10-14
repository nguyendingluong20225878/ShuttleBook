/** Keep notification actions bound to bookingId while hiding the long transfer reference in prose. */
export function noticeText(value: string): string {
  return value.replace(/\b(?:Đơn\s+)?BK\d{6}[0-9a-f]{32}\b/gi, 'Đơn đặt sân');
}
