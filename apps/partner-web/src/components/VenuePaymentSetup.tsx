import { useState } from 'react';

export function VenuePaymentSetup({ venueId, venueName, account, busy, save }: {
  venueId: string; venueName: string; account?: { bankCode?: string; accountName?: string; maskedAccountNumber: string };
  busy: boolean; save: (fields: { bankCode: string; accountName: string; accountNumber: string; file: File }) => Promise<boolean>;
}) {
  const [bank, setBank] = useState(account?.bankCode ?? '');
  const [name, setName] = useState(account?.accountName ?? '');
  const [number, setNumber] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [fileKey, setFileKey] = useState(0);
  return <form className="venue-payment-setup" aria-label={`Thiết lập QR cho ${venueName}`} onSubmit={event => {
    event.preventDefault();
    if (!file) return;
    void save({ bankCode: bank.trim(), accountName: name, accountNumber: number, file }).then(saved => {
      if (saved) { setNumber(''); setFile(null); setFileKey(value => value + 1); }
    });
  }}>
    <div className="venue-setup-heading"><h4>QR nhận tiền của cơ sở</h4>
      {account && <p>Đã lưu: {account.bankCode ? `${account.bankCode} · ` : ''}{account.accountName ? `${account.accountName} · ` : ''}{account.maskedAccountNumber}</p>}
    </div>
    <label htmlFor={`payment-bank-${venueId}`}>Ngân hàng
      <input id={`payment-bank-${venueId}`} placeholder="Ví dụ: MB Bank" minLength={2} maxLength={32} required value={bank} onChange={e => setBank(e.target.value)} /></label>
    <label htmlFor={`payment-name-${venueId}`}>Tên tài khoản
      <input id={`payment-name-${venueId}`} minLength={2} maxLength={160} required value={name} onChange={e => setName(e.target.value)} /></label>
    <label htmlFor={`payment-number-${venueId}`}>Số tài khoản
      <input id={`payment-number-${venueId}`} required inputMode="numeric" value={number} onChange={e => setNumber(e.target.value)} /></label>
    <label htmlFor={`payment-qr-${venueId}`}>Ảnh QR
      <input key={fileKey} id={`payment-qr-${venueId}`} required type="file" accept="image/png,image/jpeg,image/webp" onChange={e => setFile(e.target.files?.[0] ?? null)} /></label>
    <div className="venue-action-row"><small>PNG, JPEG hoặc WebP, tối đa 5 MB.</small><button disabled={busy}>Lưu QR và tài khoản</button></div>
  </form>;
}
