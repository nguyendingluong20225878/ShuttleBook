import { useEffect, useRef, useState } from 'react';

export const partnerPages = [
  { id: 'overview', label: 'Tổng quan', description: 'Theo dõi hồ sơ và chuẩn bị cơ sở của bạn.' },
  { id: 'profile', label: 'Hồ sơ doanh nghiệp', description: 'Thông tin doanh nghiệp và trạng thái xét duyệt.' },
  { id: 'account', label: 'Thông tin cá nhân', description: 'Xem và chỉnh sửa tên hiển thị của tài khoản chủ sân.' },
  { id: 'venues', label: 'Cơ sở & sân', description: 'Thêm cơ sở, sân và ảnh nhận diện ở cùng một nơi.' },
  { id: 'courts', label: 'Sân', description: 'Mỗi sân thuộc một cơ sở và có lịch, bảng giá riêng.' },
  { id: 'schedule', label: 'Lịch & giá', description: 'Xem lịch sân theo ngày, thiết lập giờ, giá và bảo trì.' },
  { id: 'bookings', label: 'Đơn đặt sân', description: 'Đối chiếu chuyển khoản và xử lý đơn trong cơ sở của bạn.' },
  { id: 'media', label: 'Ảnh cơ sở', description: 'Bổ sung hình ảnh để khách dễ nhận biết cơ sở.' },
  { id: 'payments', label: 'Thanh toán & QR', description: 'Đề nghị thay đổi tài khoản hoặc QR của cơ sở để Admin duyệt.' },
  { id: 'notifications', label: 'Thông báo', description: 'Theo dõi phản hồi hồ sơ và thông báo của bạn.' },
] as const;
export const partnerMenuPages = partnerPages.filter(page => page.id !== 'courts' && page.id !== 'media' && page.id !== 'account');
export type PartnerPage = typeof partnerPages[number]['id'];
function readPage(): PartnerPage {
  const id = window.location.hash.replace(/^#\/?/, '').split(/[/?]/)[0];
  return partnerPages.find(page => page.id === id)?.id ?? 'overview';
}
export function usePartnerNavigation(canLeaveSchedule?: () => boolean) {
  const guard = useRef(canLeaveSchedule); guard.current = canLeaveSchedule;
  const acceptedHash = useRef(window.location.hash);
  const allowedHash = useRef('');
  const scheduleHash = useRef(window.location.hash.startsWith('#/schedule') ? window.location.hash : '#/schedule');
  const [page, setPage] = useState(readPage);
  const [bookingId, setBookingId] = useState(readBookingId);
  const [visited, setVisited] = useState<Set<PartnerPage>>(() => new Set([readPage()]));
  useEffect(() => {
    const changed = () => {
      if (acceptedHash.current.startsWith('#/schedule') && readPage() !== 'schedule'
        && allowedHash.current !== window.location.hash && guard.current && !guard.current()) {
        window.history.replaceState(null, '', acceptedHash.current); return;
      }
      // Schedule's listener can reject Back to an unsaved scope after this listener runs.
      if (readPage() === 'schedule') queueMicrotask(() => {
        acceptedHash.current = window.location.hash;
        if (window.location.hash.startsWith('#/schedule')) scheduleHash.current = window.location.hash;
      });
      else acceptedHash.current = window.location.hash;
      allowedHash.current = '';
      const id = readBookingId();
      if (id && window.location.hash.startsWith('#/bookings?')) {
        window.history.replaceState(window.history.state, '', `#/bookings/${encodeURIComponent(id)}`);
      }
      setPage(readPage()); setBookingId(id);
    };
    changed();
    window.addEventListener('hashchange', changed);
    return () => window.removeEventListener('hashchange', changed);
  }, []);
  useEffect(() => { setVisited(previous => new Set([...previous, page])); }, [page]);
  function navigate(next: PartnerPage) {
    if (readPage() === 'schedule' && next !== 'schedule' && guard.current && !guard.current()) return;
    const nextHash = next === 'schedule' ? scheduleHash.current : `#/${next}`;
    allowedHash.current = nextHash;
    if (readPage() !== next || !window.location.hash || readBookingId()) window.location.hash = nextHash;
    setPage(next);
    setBookingId(null);
  }
  function openBooking(id: string | null) {
    if (readPage() === 'schedule' && guard.current && !guard.current()) return;
    allowedHash.current = `#/bookings${id ? `/${encodeURIComponent(id)}` : ''}`;
    window.location.hash = `/bookings${id ? `/${encodeURIComponent(id)}` : ''}`;
    setPage('bookings'); setBookingId(id);
  }
  return { page, bookingId, visited, navigate, openBooking };
}
function readBookingId(): string | null {
  if (readPage() !== 'bookings') return null;
  const path = window.location.hash.replace(/^#\/?/, '').split('?')[0].split('/');
  const id = path.length === 2 ? path[1] : path.length === 1
    ? new URLSearchParams(window.location.hash.split('?')[1] ?? '').get('bookingId') : null;
  return id && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id) ? id : null;
}
export const statusLabel = (status: string) => ({ DRAFT: 'Hồ sơ nháp', ACTIVE: 'Đang hoạt động',
  PUBLISHED: 'Đang công bố', INACTIVE: 'Sân nháp',
  PENDING_APPROVAL: 'Đang chờ duyệt', CHANGES_REQUESTED: 'Cần bổ sung', PENDING: 'Đang chờ duyệt',
  APPROVED: 'Đã duyệt', SUSPENDED: 'Tạm ngưng' }[status] ?? status);
