import { useEffect, useMemo, useState } from 'react';
import type { CourtSchedule, ScheduleSlot, VenueSchedule } from '../types';

type Selection = { courtId: string; starts: string[] } | null;
const money = new Intl.NumberFormat('vi-VN');

function minutes(time: string) { const [hour, minute] = time.split(':').map(Number); return hour * 60 + minute; }
function label(total: number) { return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`; }

export function ScheduleGrid({ schedule }: { schedule: VenueSchedule }) {
  const [selection, setSelection] = useState<Selection>(null);
  const courts = schedule.courts;
  const axis = useMemo(() => {
    const all = courts.flatMap(court => court.slots);
    if (all.length === 0) return [];
    const first = Math.min(...all.map(slot => minutes(slot.startsAt)));
    const last = Math.max(...all.map(slot => minutes(slot.endsAt)));
    const times: string[] = [];
    for (let current = first; current < last; current += 30) times.push(label(current));
    return times;
  }, [schedule.courts]);
  const axisEnd = axis.length > 0 ? label(minutes(axis[axis.length - 1]) + 30) : '';

  useEffect(() => {
    if (!selection) return;
    const court = schedule.courts.find(row => row.courtId === selection.courtId);
    if (!court || selection.starts.some(time => !court.slots.some(slot => slot.startsAt === time && slot.status === 'AVAILABLE')))
      setSelection(null);
  }, [schedule, selection]);

  function select(court: CourtSchedule, clicked: ScheduleSlot) {
    if (selection?.courtId !== court.courtId || !selection.starts.length) {
      setSelection({ courtId: court.courtId, starts: [clicked.startsAt] }); return;
    }
    const selectedIndex = selection.starts.indexOf(clicked.startsAt);
    if (selectedIndex >= 0) {
      const before = selection.starts.slice(0, selectedIndex);
      const after = selection.starts.slice(selectedIndex + 1);
      const remaining = before.length >= after.length ? before : after;
      setSelection(remaining.length ? { courtId: court.courtId, starts: remaining } : null);
      return;
    }
    const first = Math.min(minutes(selection.starts[0]), minutes(clicked.startsAt));
    const last = Math.max(minutes(selection.starts[selection.starts.length - 1]), minutes(clicked.startsAt));
    const range = court.slots.filter(slot => minutes(slot.startsAt) >= first && minutes(slot.startsAt) <= last);
    if (range.length === (last - first) / 30 + 1 && range.every(slot => slot.status === 'AVAILABLE'))
      setSelection({ courtId: court.courtId, starts: range.map(slot => slot.startsAt) });
    else setSelection({ courtId: court.courtId, starts: [clicked.startsAt] });
  }

  const selectedCourt = schedule.courts.find(court => court.courtId === selection?.courtId);
  const selectedSlots = selectedCourt?.slots.filter(slot => selection?.starts.includes(slot.startsAt)) ?? [];
  const duration = selectedSlots.length * 30;
  const valid = Boolean(selectedCourt && duration >= selectedCourt.minimumBookingMinutes &&
    duration % selectedCourt.bookingBlockMinutes === 0);
  const total = selectedSlots.reduce((sum, slot) => sum + (slot.pricePerSlot ?? 0), 0);

  return <section className="schedule-section" aria-label="Lịch các sân">
    <div className="schedule-intro"><div><h2>Lịch theo sân và giờ</h2>
      <p>Giờ địa phương: {schedule.timezone}. Giá hiển thị cho từng ca 30 phút.</p></div>
      <div className="schedule-legend" aria-label="Chú giải trạng thái">
        <span><i className="legend-dot available" /> Còn trống</span>
        <span><i className="legend-dot reserved" /> Đã kín</span>
        <span><i className="legend-dot closed" /> Ngoài giờ/chưa có giá</span>
        <span><i className="legend-dot selected" /> Đang chọn</span>
      </div></div>
    {axis.length === 0 ? <p>Ngày này chưa có giờ hoạt động cho các sân.</p> : <div className="schedule-scroll" tabIndex={0}
      aria-label="Bảng lịch sân, cuộn ngang để xem các giờ khác">
      <table className="schedule-grid"><thead><tr><th className="court-heading" scope="col">Sân / giờ</th>
        {axis.map((time, index) => <th key={time} scope="col" className="time-heading"
          aria-label={`${time} đến ${index + 1 < axis.length ? axis[index + 1] : axisEnd}, 30 phút`}>
          <span className="time-start">{time}</span><span className="time-duration">30 phút</span>
          {index === axis.length - 1 && <span className="time-end">{axisEnd}</span>}
        </th>)}</tr></thead>
        <tbody>{courts.map(court => {
          const slots = new Map(court.slots.map(slot => [slot.startsAt, slot]));
          return <tr key={court.courtId}><th scope="row" className="court-heading">
            <strong>{court.name}</strong><small>Tối thiểu {court.minimumBookingMinutes} phút · block {court.bookingBlockMinutes} phút</small>
          </th>{axis.map(time => {
            const slot = slots.get(time);
            const selected = selection?.courtId === court.courtId && selection.starts.includes(time);
            const status = slot?.status ?? 'CLOSED';
            const text = status === 'AVAILABLE' ? 'Còn trống' : status === 'RESERVED' ? 'Đã kín'
              : status === 'NO_PRICE' ? 'Chưa có giá' : status === 'PAST' ? 'Đã qua' : 'Ngoài giờ';
            const price = slot?.pricePerSlot !== null && slot?.pricePerSlot !== undefined
              ? `${money.format(slot.pricePerSlot)}đ` : '';
            return <td key={time} className={`slot-cell ${selected ? 'is-selected' : status.toLowerCase()}`}>
              {slot?.status === 'AVAILABLE' ? <button type="button" aria-pressed={selected}
                aria-label={`${court.name}, ${time} đến ${slot.endsAt}, ${text}, ${price}`}
                onClick={() => select(court, slot)}><span>{selected ? 'Đang chọn' : 'Còn trống'}</span><small>{price}</small></button>
                : <span className="slot-label" aria-label={`${court.name}, ${time}, ${text}`}>
                  {status === 'CLOSED' ? '—' : text}</span>}</td>;
          })}</tr>;
        })}</tbody></table></div>}
    {selectedCourt && selectedSlots.length > 0 && <div className="selection-summary" role="status">
      <strong>{selectedCourt.name}: {selectedSlots[0].startsAt}–{selectedSlots[selectedSlots.length - 1].endsAt}</strong>
      <span>{duration} phút · Giá tham khảo {money.format(total)}đ</span>
      <small>{valid ? 'Khung giờ hợp lệ theo thời lượng của sân.' :
        `Hãy chọn ca liên tiếp, tối thiểu ${selectedCourt.minimumBookingMinutes} phút và theo block ${selectedCourt.bookingBlockMinutes} phút.`}</small>
    </div>}
    <p className="schedule-note">Lịch và giá có thể thay đổi. Hệ thống sẽ kiểm tra lại khi tạo đơn đặt sân.</p>
  </section>;
}
