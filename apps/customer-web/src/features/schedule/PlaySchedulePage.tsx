import { useEffect, useRef, useState } from 'react';
import { CustomerShell } from '../../components/CustomerShell';
import { navigate } from '../../routes/navigation';
import { useCustomerSession } from '../auth/CustomerSession';
import { data, dateTime } from '../bookings/bookingApi';
import { bookingStatusLabel } from '../bookings/BookingStatus';
import './play-schedule.css';

type Recruitment = { postId: string | null; status: string | null; slotsNeeded: number | null;
  acceptedCount: number | null; pendingCount: number | null; canCreate: boolean };
type Game = { kind: 'BOOKING' | 'TEAM'; id: string; bookingId: string | null; postId: string | null;
  startsAt: string; endsAt: string; date: string; localStart: string; localEnd: string; timezone: string;
  venueName: string; courtName: string; status: string; organizerName: string | null;
  bookingType?: string | null; paymentDeadline?: string | null; transferDeadlinePassed?: boolean;
  recruitment?: Recruitment | null };
type Page = { items: Game[]; nextCursor: string | null };
type View = 'upcoming' | 'past';

function dayLabel(date: string) {
  const parsed = new Date(`${date}T00:00:00Z`);
  return Number.isNaN(parsed.getTime()) ? date : new Intl.DateTimeFormat('vi-VN',
    { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' }).format(parsed);
}

function bookingAction(item: Game, view: View) {
  if (view === 'past') return 'Xem đơn đặt sân';
  if (item.status === 'AWAITING_TRANSFER' && item.transferDeadlinePassed) return 'Kiểm tra trạng thái đơn';
  if (item.status === 'AWAITING_TRANSFER') return 'Xem hạn chuyển khoản';
  if (item.status === 'NEEDS_REVIEW') return 'Kiểm tra bằng chứng';
  if (item.status === 'AWAITING_OWNER_CONFIRMATION') return 'Theo dõi xác nhận';
  return 'Xem thông tin sân';
}

function bookingNote(item: Game) {
  if (item.status === 'AWAITING_TRANSFER' && item.transferDeadlinePassed)
    return 'Hạn chuyển khoản đã qua · Kiểm tra trạng thái mới nhất trong đơn';
  if (item.status === 'AWAITING_TRANSFER')
    return item.paymentDeadline && !Number.isNaN(Date.parse(item.paymentDeadline))
      ? `Chưa xác nhận · Hạn chuyển khoản: ${dateTime(item.paymentDeadline, item.timezone)}`
      : 'Chưa xác nhận · Xem hạn chuyển khoản trong đơn';
  if (item.status === 'NEEDS_REVIEW') return 'Chưa xác nhận · Kiểm tra yêu cầu đối chiếu trong đơn';
  if (item.status === 'AWAITING_OWNER_CONFIRMATION') return 'Chưa xác nhận · Đang chờ cơ sở đối chiếu';
  return null;
}

export function PlaySchedulePage({ view }: { view: View }) {
  const { session, request } = useCustomerSession();
  const [items, setItems] = useState<Game[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState('');
  const [errorSource, setErrorSource] = useState<'first' | 'refresh' | 'more' | null>(null);
  const [retry, setRetry] = useState(0);
  const moreController = useRef<AbortController | null>(null);
  const loadedView = useRef<View | null>(null);
  const loadedToken = useRef<string | null>(null);
  const visibleItems = loadedView.current === view && loadedToken.current === session?.accessToken ? items : [];

  useEffect(() => {
    if (!session && location.pathname !== '/login')
      navigate(`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`, true);
  }, [Boolean(session)]);
  useEffect(() => {
    if (!session) { loadedView.current = null; loadedToken.current = null; setItems([]); setCursor(null); setRefreshing(false); return; }
    const controller = new AbortController();
    const background = loadedView.current === view && loadedToken.current === session.accessToken;
    moreController.current?.abort(); moreController.current = null; setLoadingMore(false);
    setError(''); setErrorSource(null);
    if (background) setRefreshing(true);
    else { loadedView.current = null; loadedToken.current = null; setRefreshing(false); setLoading(true); setItems([]); setCursor(null); }
    void request(`/api/v1/me/play-schedule?view=${view}`, { signal: controller.signal }).then(data<Page>)
      .then(page => { if (!controller.signal.aborted) {
        setItems(page.items); setCursor(page.nextCursor); loadedView.current = view; loadedToken.current = session.accessToken;
      } })
      .catch(() => { if (!controller.signal.aborted) {
        setError(background ? 'Chưa cập nhật được lịch chơi. Đang hiển thị dữ liệu lần tải trước.' : 'Chưa tải được lịch chơi. Vui lòng thử lại.');
        setErrorSource(background ? 'refresh' : 'first');
      } })
      .finally(() => { if (!controller.signal.aborted) { setLoading(false); setRefreshing(false); } });
    return () => { controller.abort(); moreController.current?.abort(); };
  }, [Boolean(session), session?.accessToken, request, view, retry]);
  useEffect(() => {
    if (!session) return;
    const refreshWhenVisible = () => {
      if (document.visibilityState === 'visible') setRetry(value => value + 1);
    };
    document.addEventListener('visibilitychange', refreshWhenVisible);
    return () => document.removeEventListener('visibilitychange', refreshWhenVisible);
  }, [Boolean(session)]);

  async function loadMore() {
    if (!cursor || loadingMore) return;
    const controller = new AbortController(); moreController.current = controller;
    setLoadingMore(true); setError(''); setErrorSource(null);
    try {
      const page = await request(`/api/v1/me/play-schedule?view=${view}&cursor=${encodeURIComponent(cursor)}`,
        { signal: controller.signal }).then(data<Page>);
      if (!controller.signal.aborted) {
        setItems(previous => [...previous, ...page.items.filter(item => !previous.some(old => old.kind === item.kind && old.id === item.id))]);
        setCursor(page.nextCursor);
      }
    } catch { if (!controller.signal.aborted) { setError('Chưa tải được trang tiếp theo. Vui lòng thử lại.'); setErrorSource('more'); } }
    finally { if (moreController.current === controller) { moreController.current = null; setLoadingMore(false); } }
  }

  return <CustomerShell title="Lịch chơi của tôi" eyebrow="SHUTTLEBOOK / LỊCH CHƠI">
    <section className="play-schedule">
      <nav className="play-schedule-tabs" aria-label="Khoảng thời gian lịch chơi">
        <a href="/me/play-schedule" aria-current={view === 'upcoming' ? 'page' : undefined}>Sắp tới</a>
        <a href="/me/play-schedule?view=past" aria-current={view === 'past' ? 'page' : undefined}>Đã qua</a>
      </nav>
      {loading && <p role="status">Đang tải lịch chơi…</p>}
      {refreshing && <p className="play-schedule-refresh" role="status">Đang cập nhật lịch chơi…</p>}
      {error && <div className="play-schedule-feedback" role="alert">{error}
        <button type="button" onClick={() => { if (errorSource === 'more') void loadMore(); else setRetry(value => value + 1); }}>Thử lại</button></div>}
      {!loading && !error && visibleItems.length === 0 && <div className="play-schedule-empty">
        <h2>{view === 'upcoming' ? 'Bạn chưa có lịch chơi sắp tới' : 'Chưa có lịch chơi đã qua'}</h2>
        {view === 'upcoming' && <div><a href="/venues">Tìm sân</a><a href="/teams">Tìm người chơi</a></div>}
      </div>}
      <ol className="play-schedule-list">{visibleItems.map(item => <li key={`${item.kind}-${item.id}`}>
        <article className="play-schedule-card"><div className="play-schedule-card-top">
          <span className={`play-schedule-type play-schedule-type--${item.kind.toLowerCase()}`}>
            {item.kind === 'BOOKING' ? 'Sân bạn đặt' : 'Trận bạn tham gia'}</span>
          <span className="play-schedule-status">{item.kind === 'BOOKING' ? bookingStatusLabel(item.status) : 'Đã được nhận'}</span>
        </div>
          <h2>{dayLabel(item.date)} <small>{item.localStart}–{item.localEnd}</small></h2>
          <p><strong>{item.venueName}</strong> · {item.courtName}</p>
          {item.kind === 'TEAM' && <p>Người tổ chức: {item.organizerName || 'Người tổ chức'}</p>}
          {item.kind === 'BOOKING' && bookingNote(item) && <p className="play-schedule-note">{bookingNote(item)}</p>}
          <a className="play-schedule-link" href={item.kind === 'BOOKING'
            ? `/bookings/${encodeURIComponent(item.bookingId ?? item.id)}`
            : `/me/team-requests?postId=${encodeURIComponent(item.postId ?? '')}`}>
            {item.kind === 'BOOKING' ? bookingAction(item, view) : 'Xem buổi chơi'} <span aria-hidden="true">→</span>
          </a>
          {view === 'upcoming' && item.kind === 'BOOKING' && item.bookingType === 'CASUAL' &&
            item.status === 'CONFIRMED' && item.recruitment &&
            (item.recruitment.postId || item.recruitment.canCreate) && <div className="play-schedule-recruitment">
              {item.recruitment.postId && <p>
                Đã nhận {item.recruitment.acceptedCount ?? 0}/{item.recruitment.slotsNeeded ?? 0} người
                {item.recruitment.pendingCount ? ` · ${item.recruitment.pendingCount} yêu cầu đang chờ` : ''}
                {item.recruitment.status === 'CLOSED' ? ' · Bài đã đóng' :
                  item.recruitment.status === 'HIDDEN' ? ' · Bài đã ẩn' : ''}
              </p>}
              <a href={`/me/teams?bookingId=${encodeURIComponent(item.bookingId ?? item.id)}`}>
                {item.recruitment.postId ? 'Quản lý người chơi' : 'Đăng tìm người'}
              </a>
            </div>}
        </article>
      </li>)}</ol>
      {cursor && visibleItems.length > 0 && <button type="button" className="play-schedule-more" disabled={loadingMore} onClick={() => void loadMore()}>
        {loadingMore ? 'Đang tải…' : 'Xem thêm lịch chơi'}</button>}
      <div className="play-schedule-other"><a href="/me/bookings">Tất cả đơn đặt sân</a><a href="/me/team-requests">Tất cả yêu cầu tham gia</a></div>
    </section>
  </CustomerShell>;
}
