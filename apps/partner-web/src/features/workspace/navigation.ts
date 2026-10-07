import { useEffect, useState } from 'react';

export const partnerPages = [
  { id: 'overview', label: 'Tổng quan', description: 'Theo dõi hồ sơ và chuẩn bị cơ sở của bạn.' },
  { id: 'profile', label: 'Hồ sơ doanh nghiệp', description: 'Thông tin doanh nghiệp và trạng thái xét duyệt.' },
  { id: 'venues', label: 'Cơ sở', description: 'Quản lý địa chỉ, liên hệ và thông tin từng cơ sở.' },
  { id: 'courts', label: 'Sân', description: 'Mỗi sân thuộc một cơ sở và có lịch, bảng giá riêng.' },
  { id: 'schedule', label: 'Lịch & giá', description: 'Thiết lập giờ hoạt động, đơn giá và ca bảo trì.' },
  { id: 'bookings', label: 'Đơn đặt sân', description: 'Đối chiếu chuyển khoản và xử lý đơn trong cơ sở của bạn.' },
  { id: 'media', label: 'Ảnh cơ sở', description: 'Bổ sung hình ảnh để khách dễ nhận biết cơ sở.' },
  { id: 'payments', label: 'Thanh toán & QR', description: 'Cấu hình tài khoản và QR nhận tiền của từng cơ sở.' },
  { id: 'notifications', label: 'Thông báo', description: 'Theo dõi phản hồi hồ sơ và thông báo của bạn.' },
] as const;
export type PartnerPage = typeof partnerPages[number]['id'];
function readPage(): PartnerPage {
  const id = window.location.hash.replace(/^#\/?/, '').split('?')[0];
  return partnerPages.find(page => page.id === id)?.id ?? 'overview';
}
export function usePartnerNavigation() {
  const [page, setPage] = useState(readPage);
  const [bookingId, setBookingId] = useState(readBookingId);
  const [visited, setVisited] = useState<Set<PartnerPage>>(() => new Set([readPage()]));
  useEffect(() => {
    const changed = () => { setPage(readPage()); setBookingId(readBookingId()); };
    window.addEventListener('hashchange', changed);
    return () => window.removeEventListener('hashchange', changed);
  }, []);
  useEffect(() => { setVisited(previous => new Set([...previous, page])); }, [page]);
  function navigate(next: PartnerPage) {
    if (readPage() !== next || !window.location.hash || readBookingId()) window.location.hash = `/${next}`;
    setPage(next);
    setBookingId(null);
  }
  function openBooking(id: string | null) {
    window.location.hash = `/bookings${id ? `?bookingId=${encodeURIComponent(id)}` : ''}`;
    setPage('bookings'); setBookingId(id);
  }
  return { page, bookingId, visited, navigate, openBooking };
}
function readBookingId(): string | null {
  if (readPage() !== 'bookings') return null;
  const id = new URLSearchParams(window.location.hash.split('?')[1] ?? '').get('bookingId');
  return id && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id) ? id : null;
}
export const statusLabel = (status: string) => ({ DRAFT: 'Hồ sơ nháp', ACTIVE: 'Đang hoạt động',
  PENDING_APPROVAL: 'Đang chờ duyệt', CHANGES_REQUESTED: 'Cần bổ sung', PENDING: 'Đang chờ duyệt',
  APPROVED: 'Đã duyệt', SUSPENDED: 'Tạm ngưng' }[status] ?? status);
