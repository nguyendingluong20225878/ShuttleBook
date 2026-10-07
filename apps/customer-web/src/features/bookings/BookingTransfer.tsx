import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { useCustomerSession } from '../auth/CustomerSession';
import { accessDenied, data, failure, type Booking } from './bookingApi';

type Upload = { id: string; uploadUrl: string; uploadHeaders: Record<string, string>; expiresAt: string };
type UploadIntent = { file: File; signed: Upload; ready: boolean };
type ReportIntent = { body: string; version: number; key: string };
const allowedTypes = ['image/png', 'image/jpeg', 'image/webp'];
const conflictCodes = ['PRECONDITION_FAILED', 'STATE_CONFLICT', 'PAYMENT_DEADLINE_EXPIRED', 'IDEMPOTENCY_KEY_REUSED'];

export function BookingTransfer({ booking, now, onBooking, onAccessDenied }: { booking: Booking; now: number; onBooking: (value: Booking) => void; onAccessDenied: (reason: unknown) => void }) {
  const { request } = useCustomerSession(); const supplement = booking.status === 'NEEDS_REVIEW';
  const noteId = useId();
  const [open, setOpen] = useState(false); const [note, setNote] = useState('');
  const [file, setFile] = useState<File | null>(null); const [error, setError] = useState('');
  const [fileError, setFileError] = useState(''); const fileInput = useRef<HTMLInputElement | null>(null);
  const [progress, setProgress] = useState(''); const [pending, setPending] = useState(false); const [mustReload, setMustReload] = useState(false);
  const upload = useRef<UploadIntent | null>(null); const intent = useRef<ReportIntent | null>(null);
  const busy = useRef(false); const active = useRef<AbortController | null>(null);
  const latest = useRef(booking); latest.current = booking;
  const selectedProof = file && upload.current?.file === file && upload.current.ready ? upload.current.signed.id : undefined;
  const draftBody = (!file || selectedProof) ? JSON.stringify({
    ...(selectedProof ? { proofUploadId: selectedProof } : {}), ...(note.trim() ? { note: note.trim() } : {}) }) : null;
  // A lost response may conceal a committed report. Only the unchanged sent intent can replay after the old deadline.
  const retrySameIntent = Boolean(intent.current && intent.current.body === draftBody);
  const expired = !supplement && now >= Date.parse(booking.paymentDeadline) && !retrySameIntent;
  useEffect(() => () => active.current?.abort(), []);

  async function readyProof(selected: File, signal: AbortSignal): Promise<string> {
    if (!upload.current || upload.current.file !== selected ||
        (!upload.current.ready && Date.parse(upload.current.signed.expiresAt) <= Date.now())) {
      setProgress('Đang chuẩn bị ảnh biên lai…');
      const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', await selected.arrayBuffer()));
      if (signal.aborted) throw new DOMException('Aborted', 'AbortError');
      const sha256Base64 = btoa(String.fromCharCode(...digest));
      const signed = await request(`/api/v1/bookings/${booking.bookingId}/proof-uploads/presign`, { method: 'POST', signal,
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ contentType: selected.type, sizeBytes: selected.size, sha256Base64 }) }).then(data<Upload>);
      upload.current = { file: selected, signed, ready: false };
    }
    const current = upload.current;
    if (current.ready) return current.signed.id;
    setProgress('Đang tải và xác minh ảnh biên lai…');
    let putFailure: unknown;
    try {
      const response = await fetch(current.signed.uploadUrl, { method: 'PUT', signal, credentials: 'omit',
        headers: current.signed.uploadHeaders, body: selected });
      // An immutable file may already exist after a timed-out PUT. Complete rechecks its checksum and MIME.
      if (!response.ok && response.status !== 409 && response.status !== 412) throw new Error('UPLOAD_FAILED');
    } catch (reason) { if (signal.aborted) throw reason; putFailure = reason; }
    try {
      const complete = await request(`/api/v1/uploads/${current.signed.id}/complete`, { method: 'POST', signal }).then(data<{ id: string; status: string }>);
      if (complete.status !== 'READY') throw new Error('UPLOAD_NOT_READY');
      current.ready = true; return current.signed.id;
    } catch (reason) {
      if (putFailure && reason instanceof Error && reason.message === 'UPLOAD_NOT_FOUND') throw putFailure;
      throw reason;
    }
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy.current || mustReload || fileError) return;
    const trimmedNote = note.trim();
    if (trimmedNote.length > 1000) {
      setError('Ghi chú tối đa 1.000 ký tự.'); return;
    }
    if (!supplement && Date.now() >= Date.parse(latest.current.paymentDeadline) && !retrySameIntent) {
      setError('Đã hết hạn báo chuyển khoản. Hãy làm mới đơn để xem trạng thái.'); setMustReload(true); return;
    }
    const controller = new AbortController(); active.current = controller; busy.current = true;
    setPending(true); setError('');
    try {
      const proofUploadId = file ? await readyProof(file, controller.signal) : undefined;
      if (controller.signal.aborted) return;
      const body = JSON.stringify({ ...(proofUploadId ? { proofUploadId } : {}), ...(trimmedNote ? { note: trimmedNote } : {}) });
      if (!intent.current || intent.current.body !== body) intent.current = { body, version: latest.current.version, key: crypto.randomUUID() };
      setProgress(supplement ? 'Đang gửi bổ sung bằng chứng…' : 'Đang gửi báo chuyển khoản…');
      const result = await request(`/api/v1/bookings/${booking.bookingId}/transfer-evidence`, { method: 'POST', signal: controller.signal,
        headers: { 'Content-Type': 'application/json', 'Idempotency-Key': intent.current.key, 'If-Match': `"${intent.current.version}"` },
        body: intent.current.body }).then(data<Booking>);
      if (!controller.signal.aborted) onBooking(result);
    } catch (reason) {
      if (!controller.signal.aborted) { if (accessDenied(reason)) { onAccessDenied(reason); return; }
        setError(failure(reason)); if (reason instanceof Error && conflictCodes.includes(reason.message)) setMustReload(true); }
    } finally {
      busy.current = false;
      if (!controller.signal.aborted) { setPending(false); setProgress(''); }
    }
  }

  async function reload() {
    if (busy.current) return;
    const controller = new AbortController(); active.current = controller; busy.current = true; setPending(true);
    try {
      const value = await request(`/api/v1/bookings/${booking.bookingId}`, { signal: controller.signal }).then(data<Booking>);
      if (!controller.signal.aborted) { intent.current = null; setMustReload(false); setError(''); onBooking(value); }
    } catch (reason) { if (!controller.signal.aborted) { if (accessDenied(reason)) onAccessDenied(reason); else setError(failure(reason)); } }
    finally { busy.current = false; if (!controller.signal.aborted) setPending(false); }
  }
  function chooseFile(selected: File | null) {
    setError(''); setFileError('');
    if (selected && (!allowedTypes.includes(selected.type) || selected.size < 1 || selected.size > 5_242_880)) {
      setFile(null); upload.current = null; setFileError('Ảnh phải là PNG, JPEG hoặc WebP, dung lượng từ 1 byte đến 5 MB.'); return;
    }
    setFile(selected); upload.current = null;
  }
  return <section aria-label={supplement ? 'Bổ sung bằng chứng' : 'Báo chuyển khoản'}>
    {!open ? <button type="button" disabled={expired} onClick={() => setOpen(true)}>{supplement ? 'Bổ sung bằng chứng' : 'Đã chuyển khoản'}</button> :
      <form className="payment-form" onSubmit={event => { void submit(event); }}>
        <h3>{supplement ? 'Bổ sung bằng chứng giao dịch' : 'Báo đã chuyển khoản'}</h3>
        <p className="form-note">{supplement ? 'Gửi thông tin để chủ sân đối chiếu lại. Bạn không cần tạo đơn mới hoặc chuyển lại toàn bộ tiền.'
          : 'Chỉ báo sau khi đã chuyển khoản. Bạn có thể gửi ảnh chụp màn hình chuyển khoản để chủ sân đối chiếu và xác nhận lịch sân.'}</p>
        <label>Ảnh chụp màn hình chuyển khoản (không bắt buộc)<input ref={fileInput} type="file" accept="image/png,image/jpeg,image/webp" disabled={pending}
          onChange={event => { const selected = event.target.files?.[0] ?? null; chooseFile(selected);
            if (selected && (!allowedTypes.includes(selected.type) || selected.size < 1 || selected.size > 5_242_880)) event.target.value = ''; }} />
          <span className="form-note">PNG, JPEG hoặc WebP, tối đa 5 MB. Ảnh chỉ dành cho bạn và chủ sân được phân quyền.</span></label>
        {file && <p className="form-note">Đã chọn: {file.name}</p>}{progress && <p role="status">{progress}</p>}
        {fileError && <p role="alert">{fileError}</p>}
        {(file || fileError) && <button type="button" disabled={pending} onClick={() => { if (fileInput.current) fileInput.current.value = ''; chooseFile(null); }}>Bỏ ảnh biên lai</button>}
        <label htmlFor={noteId}>Ghi chú (không bắt buộc)</label>
        <textarea id={noteId} rows={3} maxLength={1000} value={note} disabled={pending} onChange={event => setNote(event.target.value)} />
        {error && <p role="alert">{error}</p>}
        <button type="submit" disabled={pending || expired || mustReload || Boolean(fileError)}>{pending ? 'Đang xử lý…' : supplement ? 'Gửi bổ sung bằng chứng' : 'Gửi báo chuyển khoản'}</button>
        {mustReload && <button type="button" disabled={pending} onClick={() => { void reload(); }}>Tải lại để kiểm tra đơn</button>}
      </form>}
  </section>;
}
