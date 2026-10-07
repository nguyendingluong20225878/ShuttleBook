import { useEffect, useRef, useState, type FormEvent } from 'react';
import { PartnerApiError, money, paymentError, type BookingDetail, type PartnerRequest } from './types';

type Action = 'CONFIRMED' | 'NEEDS_REVIEW' | 'FINAL_REJECTION';
const reasonCodes = [
  ['EVIDENCE_REQUIRED', 'Cần bổ sung bằng chứng'], ['AMOUNT_MISMATCH', 'Số tiền không khớp'],
  ['TRANSACTION_NOT_FOUND', 'Chưa tìm thấy giao dịch'], ['OTHER', 'Lý do khác'],
] as const;
type Intent = { signature: string; key: string; path: string; body: object; bodyText: string; version: number };
export function BookingDecisions({ booking, request, onUpdated, onBusy, onUnavailable, reload }: {
  booking: BookingDetail; request: PartnerRequest; onUpdated: (next: BookingDetail) => void;
  onBusy: (busy: boolean) => void; onUnavailable: (error: PartnerApiError) => void; reload: () => void;
}) {
  const [action, setAction] = useState<Action>('CONFIRMED');
  const expectedAmount = booking.payment.expectedAmountExact ?? String(booking.payment.expectedAmount);
  const [amount, setAmount] = useState(expectedAmount);
  const [note, setNote] = useState('');
  const [reasonCode, setReasonCode] = useState<string>('EVIDENCE_REQUIRED');
  const [reason, setReason] = useState('');
  const [confirmedRejection, setConfirmedRejection] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [stale, setStale] = useState(false);
  const intent = useRef<Intent | null>(null);
  const controller = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const busyCallback = useRef(onBusy); busyCallback.current = onBusy;
  useEffect(() => { mounted.current = true; return () => {
    mounted.current = false; controller.current?.abort(); busyCallback.current(false);
  }; }, []);
  useEffect(() => { if (booking.status === 'NEEDS_REVIEW') setAction('CONFIRMED'); }, [booking.status]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (controller.current || stale) return;
    let body: object; let bodyText: string;
    if (action === 'CONFIRMED') {
      if (!/^\d{1,18}$/.test(amount.trim()) || BigInt(amount.trim()) > 999999999999999999n) {
        setError('Nhập số tiền thực nhận là số nguyên không âm, tối đa 18 chữ số.'); return;
      }
      const confirmedAmount = BigInt(amount.trim()).toString();
      if (BigInt(confirmedAmount) !== BigInt(expectedAmount)) {
        setError(booking.status === 'NEEDS_REVIEW'
          ? 'Số tiền thực nhận chưa khớp. Giữ trạng thái Cần bổ sung và chờ khách cung cấp giao dịch để đối chiếu.'
          : 'Số tiền thực nhận chưa khớp tổng tiền của đơn. Chọn Yêu cầu bổ sung để đối chiếu.'); return;
      }
      const trimmedNote = note.trim();
      if (trimmedNote.length > 1000) {
        setError('Ghi chú tối đa 1.000 ký tự.'); return;
      }
      body = { confirmedAmount, ...(trimmedNote ? { note: trimmedNote } : {}) };
      // Only validated decimal digits become a JSON numeric literal; text fields are escaped by JSON.stringify.
      bodyText = `{"confirmedAmount":${confirmedAmount}${trimmedNote ? `,"note":${JSON.stringify(trimmedNote)}` : ''}}`;
    } else {
      if (!reason.trim() || reason.trim().length > 1000) { setError('Nhập lý do rõ ràng, tối đa 1.000 ký tự.'); return; }
      if (action === 'FINAL_REJECTION' && !confirmedRejection) {
        setError('Xác nhận từ chối cuối cùng và giải phóng sân trước khi gửi.'); return;
      }
      body = { resolution: action, reasonCode, reason: reason.trim() };
      bodyText = JSON.stringify(body);
    }
    const path = `/operator/bookings/${booking.bookingId}/${action === 'CONFIRMED' ? 'confirm-payment' : 'reject-payment'}`;
    const signature = JSON.stringify({ path, bodyText });
    if (intent.current?.signature !== signature) intent.current = { signature, path, body, bodyText, key: crypto.randomUUID(), version: booking.version };
    const current = intent.current;
    const abort = new AbortController(); controller.current = abort;
    setBusy(true); busyCallback.current(true); setError('');
    try {
      const updated = await request<BookingDetail>(current.path, 'POST', current.body, current.version,
        { signal: abort.signal, idempotencyKey: current.key, bodyText: current.bodyText });
      if (!mounted.current || abort.signal.aborted) return;
      if (updated.bookingId !== booking.bookingId) throw new Error('WRONG_BOOKING');
      intent.current = null; onUpdated(updated);
    } catch (failure) {
      if (!mounted.current || abort.signal.aborted) return;
      if (failure instanceof PartnerApiError && [403, 404].includes(failure.status)) {
        onUnavailable(failure); return;
      }
      if (failure instanceof PartnerApiError && ['PRECONDITION_FAILED', 'STATE_CONFLICT'].includes(failure.code)) setStale(true);
      setError(paymentError(failure));
    } finally {
      if (controller.current === abort) controller.current = null;
      if (mounted.current) { setBusy(false); busyCallback.current(false); }
    }
  }
  return <form className="payment-decision-form" onSubmit={submit} aria-label="Quyết định thanh toán">
    <h4>Quyết định thanh toán</h4><p>Tổng tiền cần đối chiếu: <strong>{money(expectedAmount)}</strong>. Chỉ xác nhận sau khi bạn đã kiểm tra giao dịch ngân hàng.</p>
    {error && <p className="feedback error" role="alert">{error}</p>}
    {stale && <p>Hãy tải lại đơn và xem trạng thái mới. Hệ thống sẽ không tự gửi lại quyết định.</p>}
    <fieldset disabled={busy || stale}>
      <label>Quyết định <select value={action} onChange={event => {
        setAction(event.target.value as Action); setError(''); setConfirmedRejection(false);
      }}><option value="CONFIRMED">Xác nhận thanh toán</option><option value="NEEDS_REVIEW" disabled={booking.status === 'NEEDS_REVIEW'}>Yêu cầu bổ sung</option>
        <option value="FINAL_REJECTION">Từ chối cuối cùng</option></select></label>
      {action === 'CONFIRMED' ? <>
        <label>Số tiền thực nhận (đ) <input inputMode="numeric" required pattern="[0-9]{1,18}" maxLength={18} value={amount} onChange={e => setAmount(e.target.value)} /></label>
        <label>Ghi chú đối chiếu (không bắt buộc) <textarea maxLength={1000} value={note} onChange={e => setNote(e.target.value)} /></label>
      </> : <>
        <label>Lý do đối chiếu <select value={reasonCode} onChange={e => setReasonCode(e.target.value)}>
          {reasonCodes.map(([code, label]) => <option key={code} value={code}>{label}</option>)}</select></label>
        <label>Nội dung gửi khách <textarea required maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label>
        {action === 'NEEDS_REVIEW' ? <p className="decision-guidance">Khách sẽ nhận yêu cầu bổ sung. Sân tiếp tục được giữ và không hết hạn theo hạn chuyển khoản cũ.</p>
          : <label className="confirm-rejection"><input type="checkbox" checked={confirmedRejection} onChange={e => setConfirmedRejection(e.target.checked)} />
            Tôi xác nhận không chấp nhận giao dịch và giải phóng khung giờ của đơn này. Quyết định là cuối cùng.</label>}
      </>}
      <button type="submit" className={action === 'FINAL_REJECTION' ? 'danger-action' : ''}>
        {busy ? 'Đang gửi quyết định…' : action === 'CONFIRMED' ? 'Xác nhận đã nhận đủ tiền' : action === 'NEEDS_REVIEW' ? 'Gửi yêu cầu bổ sung' : 'Xác nhận từ chối cuối cùng'}
      </button>
    </fieldset>
    {stale && <button type="button" onClick={() => { intent.current = null; reload(); }}>Tải lại chi tiết</button>}
  </form>;
}
