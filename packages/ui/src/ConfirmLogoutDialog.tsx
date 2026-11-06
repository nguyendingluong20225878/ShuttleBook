import { useEffect, useRef } from 'react';

export function ConfirmLogoutDialog({ onCancel, onConfirm }: { onCancel: () => void; onConfirm: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const node = dialog.current;
    node?.showModal();
    return () => { if (node?.open) node.close(); };
  }, []);
  return <dialog ref={dialog} className="sb-confirm-dialog" aria-labelledby="sb-logout-title"
    onCancel={event => { event.preventDefault(); onCancel(); }}>
    <h2 id="sb-logout-title">Đăng xuất khỏi ShuttleBook?</h2>
    <p>Bạn cần đăng nhập lại để xem đơn và quản lý tài khoản.</p>
    <div className="sb-confirm-actions">
      <button type="button" onClick={onCancel}>Ở lại</button>
      <button type="button" onClick={onConfirm}>Đăng xuất</button>
    </div>
  </dialog>;
}
