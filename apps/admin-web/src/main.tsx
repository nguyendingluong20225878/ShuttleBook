import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { PortalShell } from '@shuttlebook/ui';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <PortalShell title="Quản trị ShuttleBook." description="Duyệt cơ sở và quản lý hoạt động của nền tảng." />
  </StrictMode>,
);
