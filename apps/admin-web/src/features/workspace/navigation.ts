import { useEffect, useState } from 'react';

export type AdminPage = 'overview' | 'approvals' | 'notifications';
export const adminPages = [
  { id: 'overview', title: 'Tổng quan', description: 'Theo dõi hồ sơ đối tác và các thông báo cần xử lý.' },
  { id: 'approvals', title: 'Duyệt hồ sơ', description: 'Kiểm tra thông tin đối tác trước khi phê duyệt hoặc yêu cầu chỉnh sửa.' },
  { id: 'notifications', title: 'Thông báo', description: 'Theo dõi cảnh báo vận hành và đánh dấu các thông báo đã đọc.' },
] as const;

function readPage(): AdminPage {
  const value = window.location.hash.slice(1);
  return adminPages.find(page => page.id === value)?.id ?? 'overview';
}

export function useAdminNavigation() {
  const [page, setPage] = useState<AdminPage>(readPage);
  useEffect(() => {
    const changed = () => setPage(readPage());
    window.addEventListener('hashchange', changed);
    return () => window.removeEventListener('hashchange', changed);
  }, []);
  return page;
}
