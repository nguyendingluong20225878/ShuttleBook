export type VenueSummary = {
  id: string;
  name: string;
  address: string;
  latitude: number;
  longitude: number;
  distanceMeters?: number;
  imageUrl: string | null;
};

export type CourtSummary = {
  id: string;
  name: string;
  bookingBlockMinutes: number;
  minimumBookingMinutes: number;
  holdMinutes: number;
};

export type VenueDetail = VenueSummary & {
  contact: string;
  timezone: string;
  courts: CourtSummary[];
};

export type SlotStatus = 'AVAILABLE' | 'RESERVED' | 'NO_PRICE' | 'PAST';
export type ScheduleSlot = {
  startsAt: string;
  endsAt: string;
  startsAtUtc: string;
  endsAtUtc: string;
  status: SlotStatus;
  pricePerSlot: number | null;
};
export type CourtSchedule = {
  courtId: string;
  name: string;
  bookingBlockMinutes: number;
  minimumBookingMinutes: number;
  holdMinutes: number;
  slots: ScheduleSlot[];
};
export type VenueSchedule = {
  venueId: string;
  date: string;
  timezone: string;
  generatedAt: string;
  stepMinutes: number;
  courts: CourtSchedule[];
};
export type Page<T> = { items: T[]; nextCursor: string | null };
