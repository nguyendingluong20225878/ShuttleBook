// Browser fixtures only: backend occupancy/transaction guarantees are covered by PostGIS tests.
export function emptyPartnerCalendar(venue: { id: string; name: string; courts: Array<{ id: string; name: string; status: string }> }, date: string) {
  const utc = (time: string) => new Date(`${date}T${time}:00+07:00`).toISOString();
  return { venueId: venue.id, venueName: venue.name, venueStatus: 'PUBLISHED', timezone: 'Asia/Ho_Chi_Minh', date,
    generatedAt: `${date}T02:00:00Z`, dayStartsAt: utc('00:00'), dayEndsAt: new Date(Date.parse(utc('00:00')) + 86400000).toISOString(),
    courts: [...venue.courts].sort((a, b) => a.name.localeCompare(b.name, 'vi', { numeric: true })).map(court => ({ ...court, canRequestNewBooking: court.status === 'ACTIVE',
      openRanges: [{ startsAt: utc('07:00'), endsAt: utc('22:00'), localStart: '07:00', localEnd: '22:00', isPriced: true }], events: [],
      slots: Array.from({ length: 48 }, (_, index) => {
        const minutes = index * 30, local = (n: number) => `${String(Math.floor(n / 60)).padStart(2, '0')}:${String(n % 60).padStart(2, '0')}`;
        const start = local(minutes), end = local(minutes + 30), isOpen = minutes >= 420 && minutes < 1320;
        return { startsAt: start, endsAt: end, startsAtUtc: utc(start), endsAtUtc: new Date(Date.parse(utc(start)) + 1800000).toISOString(),
          isPast: false, isOpen, occupancy: isOpen ? 'FREE' : 'CLOSED', canRequestNewBooking: isOpen && court.status === 'ACTIVE', pricePerSlotExact: isOpen ? '100000' : null };
      }) })) };
}
