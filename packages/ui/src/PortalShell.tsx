import './portal.css';

interface PortalShellProps {
  title: string;
  description: string;
}

export function PortalShell({ title, description }: PortalShellProps) {
  return (
    <main className="portal">
      <p className="brand">ShuttleBook</p>
      <section className="intro" aria-labelledby="portal-title">
        <p className="eyebrow">Sân cầu lông · Lịch của bạn</p>
        <h1 id="portal-title">{title}</h1>
        <p className="description">{description}</p>
        <p className="notice">Ứng dụng đang được phát triển. Các chức năng sẽ được mở khi sẵn sàng.</p>
      </section>
      <footer>ShuttleBook · Cùng nhau ra sân</footer>
    </main>
  );
}
