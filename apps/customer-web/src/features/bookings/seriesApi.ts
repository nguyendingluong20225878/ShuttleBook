import type { Slot } from './bookingApi';

export const weekdays = [
  { value: 'MONDAY', label: 'Thứ 2' }, { value: 'TUESDAY', label: 'Thứ 3' },
  { value: 'WEDNESDAY', label: 'Thứ 4' }, { value: 'THURSDAY', label: 'Thứ 5' },
  { value: 'FRIDAY', label: 'Thứ 6' }, { value: 'SATURDAY', label: 'Thứ 7' }, { value: 'SUNDAY', label: 'Chủ nhật' },
] as const;
export type SeriesForm = { courtId: string; dayOfWeek: string; localStartTime: string; durationMinutes: number;
  startsOn: string; endsOn: string };
export type SeriesPreview = { date: string; localStart: string; localEnd: string; startsAt: string; endsAt: string;
  amount: number; amountExact: string; slots: Slot[] };
export type SeriesConflict = { date: string; startsAt: string; endsAt: string; code: string };
export type SeriesQuote = { quoteId: string | null; expiresAt: string | null; canCreate: boolean; courtId: string; venueId: string;
  courtName: string; venueName: string; timezone: string; dayOfWeek: string; startsOn: string; endsOn: string;
  localStartTime: string; durationMinutes: number; occurrenceCount: number; amount: number; amountExact: string; currency: string;
  holdMinutes: number; minimumBookingMinutes: number; bookingBlockMinutes: number; occurrences: SeriesPreview[]; conflicts: SeriesConflict[] };

export function isDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) && Number.isFinite(Date.parse(`${value}T00:00:00Z`)) &&
    new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value;
}
export function calendarMonthAfter(value: string) {
  if (!isDate(value)) return '';
  const source = new Date(`${value}T00:00:00Z`);
  const target = new Date(Date.UTC(source.getUTCFullYear(), source.getUTCMonth() + 1, 1));
  const last = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate();
  target.setUTCDate(Math.min(source.getUTCDate(), last));
  return target.toISOString().slice(0, 10);
}
export function addDays(value: string, days: number) {
  if (!isDate(value)) return '';
  const date = new Date(`${value}T00:00:00Z`); date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}
export function zoneToday(timezone: string) {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: timezone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
  const part = (type: string) => parts.find(item => item.type === type)?.value ?? '';
  return `${part('year')}-${part('month')}-${part('day')}`;
}
export function weekdayFromDate(value: string) {
  if (!isDate(value)) return 'MONDAY';
  return ['SUNDAY', 'MONDAY', 'TUESDAY', 'WEDNESDAY', 'THURSDAY', 'FRIDAY', 'SATURDAY'][new Date(`${value}T00:00:00Z`).getUTCDay()];
}
export function weekdayLabel(value: string) { return weekdays.find(day => day.value === value)?.label ?? value; }
export function timeMinutes(value: string) {
  if (!/^\d{2}:\d{2}$/.test(value)) return NaN;
  const [hour, minute] = value.split(':').map(Number);
  return hour < 24 && minute < 60 ? hour * 60 + minute : NaN;
}
export function endTime(start: string, duration: number) {
  const total = timeMinutes(start) + duration;
  return Number.isFinite(total) && total <= 1440 ? `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}` : '—';
}
export function validateSeries(form: SeriesForm, minimum: number, today: string) {
  if (!weekdays.some(day => day.value === form.dayOfWeek)) return 'Hãy chọn một ngày trong tuần.';
  if (!Number.isFinite(timeMinutes(form.localStartTime)) || timeMinutes(form.localStartTime) % 30 !== 0)
    return 'Giờ bắt đầu phải nằm trên mốc 30 phút.';
  if (!Number.isInteger(form.durationMinutes) || form.durationMinutes < minimum || form.durationMinutes % 30 !== 0)
    return `Mỗi buổi cần ít nhất ${minimum} phút và là các ca 30 phút liên tiếp.`;
  if (timeMinutes(form.localStartTime) + form.durationMinutes >= 1440) return 'Khung giờ một buổi phải nằm trong cùng ngày.';
  if (!isDate(form.startsOn) || !isDate(form.endsOn)) return 'Hãy nhập ngày bắt đầu và kết thúc hợp lệ.';
  if (form.endsOn < calendarMonthAfter(form.startsOn)) return 'Kỳ cố định phải dài ít nhất một tháng theo lịch.';
  if (form.startsOn < today || form.startsOn > addDays(today, 60) || form.endsOn > addDays(today, 60))
    return 'Toàn bộ kỳ phải nằm trong 60 ngày đặt trước của cơ sở.';
  return '';
}
