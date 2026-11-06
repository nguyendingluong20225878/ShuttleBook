import { PartnerIcon } from '../../components/PartnerIcon';
import { PartnerPage, statusLabel } from './navigation';

type VenueSummary = { id: string; name: string; address: string; status: string; imageUploadId?: string;
  paymentAccount?: object; courts: Array<{ id: string; hours: object[]; prices: object[] }> };
export function PartnerOverview({ name, status, venues, navigate }: {
  name: string; status: string; venues: VenueSummary[]; navigate: (page: PartnerPage) => void;
}) {
  const courts = venues.flatMap(venue => venue.courts);
  const configured = courts.filter(court => court.hours.length > 0 && court.prices.length > 0).length;
  const steps: Array<{ label: string; done: boolean; page: PartnerPage; detail: string }> = [
    { label: 'Thông tin doanh nghiệp', done: true, page: 'profile', detail: 'Tên pháp lý và liên hệ' },
    { label: 'Cơ sở và sân', done: venues.length > 0 && venues.every(v => v.courts.length > 0), page: 'venues', detail: 'Mỗi cơ sở cần ít nhất một sân; ảnh cơ sở tùy chọn' },
    { label: 'Giờ hoạt động và bảng giá', done: courts.length > 0 && configured === courts.length, page: 'schedule', detail: 'Cấu hình riêng cho từng sân' },
    { label: 'Tài khoản nhận tiền và QR', done: venues.length > 0 && venues.every(v => Boolean(v.paymentAccount)), page: 'payments', detail: 'QR do bạn cung cấp' },
  ];
  const complete = steps.filter(step => step.done).length;
  const showSetup = status !== 'ACTIVE' || complete < steps.length;
  const visibleSteps = status === 'ACTIVE' ? steps.filter(step => !step.done) : steps;
  return <>
    <section className="overview-welcome"><div><p className="eyebrow">{status === 'ACTIVE' ? 'VẬN HÀNH CƠ SỞ' : 'CÙNG CHUẨN BỊ CHO KHÁCH CHƠI'}</p><h2>{name}</h2><p>{status === 'ACTIVE'
      ? 'Quản lý đơn, lịch sân và thông tin cơ sở tại đây.'
      : 'Hoàn thiện thông tin để cơ sở của bạn sẵn sàng xuất hiện trên ShuttleBook.'}</p>
      {status !== 'ACTIVE' && <a className="button" href="#/profile" onClick={() => navigate('profile')}>
        Xem hồ sơ<PartnerIcon name="arrow" /></a>}</div>
      {status !== 'ACTIVE' && <span className={`status-badge status-${status.toLowerCase()}`}>{statusLabel(status)}</span>}
    </section>
    {status === 'ACTIVE' && <section className="overview-quick-actions" aria-label="Công việc vận hành">
      {([
        { page: 'bookings', title: 'Đơn đặt sân', detail: 'Xử lý đơn và đối chiếu chuyển khoản' },
        { page: 'schedule', title: 'Lịch & giá', detail: 'Xem lịch sân, giá và bảo trì' },
        { page: 'venues', title: 'Cơ sở & sân', detail: 'Cập nhật thông tin cơ sở' },
        { page: 'notifications', title: 'Thông báo', detail: 'Xem cập nhật cần chú ý' },
      ] as const).map(action => <a key={action.page} href={`#/${action.page}`} onClick={() => navigate(action.page)}>
        <strong>{action.title}</strong><small>{action.detail}</small><PartnerIcon name="arrow" />
      </a>)}
    </section>}
    <div className="stats-grid" aria-label="Thống kê cấu hình">{[
      { icon: 'venues' as const, label: 'Cơ sở', value: venues.length, note: 'Thuộc doanh nghiệp này' },
      { icon: 'courts' as const, label: 'Sân', value: courts.length, note: 'Trong các cơ sở của bạn' },
      { icon: 'schedule' as const, label: 'Sân có lịch & giá', value: `${configured}/${courts.length}`, note: 'Đã có cấu hình giờ và giá' },
    ].map(item => <article className="stat-card" key={item.label}><span className="stat-icon"><PartnerIcon name={item.icon} /></span><p>{item.label}</p><strong>{item.value}</strong><small>{item.note}</small></article>)}</div>
    <div className={`overview-columns ${showSetup ? '' : 'overview-columns--single'}`}>
      {showSetup && <section className="panel"><div className="panel-heading"><h3>{status === 'ACTIVE' ? 'Cấu hình còn thiếu' : 'Hoàn thiện hồ sơ'}</h3><span>{complete}/{steps.length} mục</span></div>
        <ul className="checklist">{visibleSteps.map(step => <li key={step.label}><span className={`step-marker ${step.done ? 'done' : ''}`}>
          {step.done ? <PartnerIcon name="check" /> : <span aria-hidden="true">·</span>}</span><a href={`#/${step.page}`} onClick={() => navigate(step.page)}>
          <strong>{step.label}</strong><small>{step.done ? 'Đã có thông tin' : step.detail}</small></a><PartnerIcon name="arrow" /></li>)}</ul>
      </section>}
      <section className="panel"><div className="panel-heading"><h3>Cơ sở của bạn</h3><a href="#/venues" onClick={() => navigate('venues')}>Quản lý</a></div>
        {venues.length === 0 ? <div className="empty-state"><PartnerIcon name="venues" /><h4>Chưa có cơ sở</h4><p>Thêm địa chỉ cơ sở, sau đó tạo các sân bên trong.</p><a className="button secondary" href="#/venues" onClick={() => navigate('venues')}>Thêm cơ sở</a></div>
          : <ul className="venue-summary-list">{venues.map(venue => <li key={venue.id}><span className="stat-icon"><PartnerIcon name="venues" /></span><div><strong>{venue.name}</strong><p>{venue.address}</p><small>{venue.courts.length} sân · {statusLabel(venue.status)}</small></div></li>)}</ul>}
      </section>
    </div>
  </>;
}
