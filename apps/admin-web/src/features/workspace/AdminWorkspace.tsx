import { useState } from 'react';
import { AdminApprovals } from '../../AdminApprovals';
import { AdminNotifications } from '../../AdminNotifications';
import { AdminIcon } from '../../components/AdminIcon';
import { AdminShell } from '../../layouts/AdminShell';
import { useAdminNavigation } from './navigation';

export type AdminSummary = { count: number | null; error: boolean };
const initial: AdminSummary = { count: null, error: false };

function countText(summary: AdminSummary) { return summary.error ? 'Chưa tải được' : summary.count === null ? 'Đang tải…' : summary.count.toLocaleString('vi-VN'); }

export function AdminWorkspace({ accessToken, busy, onLogout }: { accessToken: string; busy: boolean; onLogout: () => void }) {
  const page = useAdminNavigation();
  const [approvals, setApprovals] = useState<AdminSummary>(initial);
  const [notices, setNotices] = useState<AdminSummary>(initial);
  return <AdminShell page={page} busy={busy} onLogout={onLogout}>
    {page === 'overview' && <section className="admin-overview" aria-label="Tổng quan vận hành">
      <div className="admin-metrics">
        <a href="#approvals" className="admin-metric"><span><AdminIcon name="approvals" />Hồ sơ chờ xử lý</span><strong>{countText(approvals)}</strong><small className="admin-metric-caption">Số hồ sơ đang chờ duyệt</small><small>Mở danh sách hồ sơ <AdminIcon name="arrow" /></small></a>
        <a href="#notifications" className="admin-metric"><span><AdminIcon name="notifications" />Thông báo chưa đọc</span><strong>{countText(notices)}</strong><small>Xem thông báo <AdminIcon name="arrow" /></small></a>
      </div>
      <p className="admin-guidance">Duyệt hồ sơ đối tác và theo dõi cảnh báo đối chiếu. Quyết định xác nhận tiền thuộc chủ sân.</p>
    </section>}
    <div className="admin-panels">
      <div hidden={page === 'notifications'}><AdminApprovals accessToken={accessToken} onSummary={setApprovals} /></div>
      <div hidden={page === 'approvals'}><AdminNotifications accessToken={accessToken} onSummary={setNotices} /></div>
    </div>
  </AdminShell>;
}
