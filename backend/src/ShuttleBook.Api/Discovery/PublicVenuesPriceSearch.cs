using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Api.Discovery;

public static partial class PublicVenuesEndpoints
{
    private sealed class PriceSearchRow
    {
        public Guid Id { get; set; }
        public Guid MatchedCourtId { get; set; }
        public long EstimatedTotalVnd { get; set; }
    }

    private static async Task<IResult> PriceSearch(HttpContext http, ShuttleBookDbContext db,
        TimeProvider clock, IConfiguration config, CancellationToken ct)
    {
        if (!OnlyQuery(http, "city", "q", "date", "startsAt", "durationMinutes", "maxPriceVnd", "limit", "cursor") ||
            !TryPage(http, out var page) ||
            http.Request.Query["city"].ToString() is not ("HN" or "HCM") ||
            !DateOnly.TryParseExact(http.Request.Query["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ||
            !TimeOnly.TryParseExact(http.Request.Query["startsAt"], "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var start) || start.Minute is not (0 or 30) ||
            !int.TryParse(http.Request.Query["durationMinutes"], NumberStyles.None, CultureInfo.InvariantCulture,
                out var duration) || duration is < 30 or > 480 || duration % 30 != 0 ||
            !long.TryParse(http.Request.Query["maxPriceVnd"], NumberStyles.None, CultureInfo.InvariantCulture,
                out var budget) || budget is < 1 or > BookingAmountPolicy.MaximumPaymentVnd ||
            start.Hour * 60 + start.Minute + duration >= 1440)
            return Error(http, 400, "VALIDATION_FAILED");

        var q = http.Request.Query["q"].ToString().Trim();
        if (q.Length > 120) return Error(http, 400, "VALIDATION_FAILED");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).Date);
        var horizon = Math.Clamp(config.GetValue("Booking:MaxAdvanceDays", 60), 1, 365);
        if (date < today || date > today.AddDays(horizon)) return Error(http, 400, "VALIDATION_FAILED");

        var end = start.AddMinutes(duration);
        var city = http.Request.Query["city"].ToString();
        var day = (int)date.DayOfWeek;
        var slots = duration / 30;
        var now = clock.GetUtcNow();
        var pattern = $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        // A single PostgreSQL statement filters the full city inventory before pagination. The same
        // court/date/slot rules and active-hold semantics are used again by the authoritative quote.
        var rows = await db.Database.SqlQuery<PriceSearchRow>($"""
            WITH eligible AS (
                SELECT v.id AS venue_id, c.id AS court_id
                FROM venues v
                JOIN businesses b ON b.id = v.business_id AND b.status = 'ACTIVE'
                JOIN pg_timezone_names tz ON tz.name = v.timezone
                JOIN courts c ON c.venue_id = v.id AND c.status = 'ACTIVE'
                JOIN court_operating_hours h ON h.court_id = c.id AND h.day_of_week = {day}
                JOIN venue_payment_accounts pa ON pa.venue_id = v.id
                JOIN media_uploads qr ON qr.id = pa.qr_upload_id AND qr.venue_id = v.id
                    AND qr.purpose = 'QR' AND qr.status = 'READY'
                WHERE v.city_code = {city} AND v.status = 'PUBLISHED'
                    AND ({q} = '' OR v.name ILIKE {pattern} ESCAPE '\' OR v.address ILIKE {pattern} ESCAPE '\')
                    AND h.opens_at <= {start} AND h.closes_at >= {end}
                    AND c.minimum_booking_minutes <= {duration}
                    AND {date} >= ({now} AT TIME ZONE v.timezone)::date
                    AND {date} <= ({now} AT TIME ZONE v.timezone)::date + {horizon}
                    AND ({date}::date + {start}::time) AT TIME ZONE v.timezone > {now}
                    AND NOT EXISTS (
                        SELECT 1 FROM court_allocations a
                        WHERE a.court_id = c.id AND a.status = 'RESERVED'
                            AND a.starts_at < (({date}::date + {end}::time) AT TIME ZONE v.timezone)
                            AND a.ends_at > (({date}::date + {start}::time) AT TIME ZONE v.timezone)
                            AND (a.kind <> 'QUOTE_HOLD' OR EXISTS (
                                SELECT 1 FROM quote_reservations r WHERE r.id = a.quote_reservation_id
                                    AND r.released_at IS NULL AND r.consumed_at IS NULL AND r.expires_at > {now}))
                    )
            ), priced AS (
                SELECT e.venue_id, e.court_id, p.price_per_slot
                FROM eligible e
                CROSS JOIN generate_series(0, {slots} - 1) AS gs(i)
                LEFT JOIN LATERAL (
                    SELECT pr.price_per_slot FROM pricing_rules pr
                    WHERE pr.court_id = e.court_id AND pr.day_of_week = {day}
                        AND pr.starts_on <= {date} AND pr.ends_on >= {date}
                        AND pr.starts_at <= ({start}::time + gs.i * interval '30 minutes')::time
                        AND pr.ends_at >= ({start}::time + (gs.i + 1) * interval '30 minutes')::time
                    ORDER BY pr.priority DESC, pr.id
                    LIMIT 1
                ) p ON true
            ), court_totals AS (
                SELECT venue_id, court_id, SUM(price_per_slot)::bigint AS total
                FROM priced GROUP BY venue_id, court_id
                HAVING COUNT(price_per_slot) = {slots} AND SUM(price_per_slot) BETWEEN 1 AND {budget}
            ), winners AS (
                SELECT DISTINCT ON (venue_id) venue_id, court_id, total
                FROM court_totals ORDER BY venue_id, total, court_id
            )
            SELECT w.venue_id AS "Id", w.court_id AS "MatchedCourtId", w.total AS "EstimatedTotalVnd"
            FROM winners w JOIN venues v ON v.id = w.venue_id
            ORDER BY w.total, v.name, v.id
            LIMIT {page.Limit + 1} OFFSET {page.Offset}
            """).ToListAsync(ct);
        var hasMore = rows.Count > page.Limit;
        rows = rows.Take(page.Limit).ToList();
        var ids = rows.Select(x => x.Id).ToArray();
        var venues = await db.Venues.AsNoTracking().Where(v => ids.Contains(v.Id)).ToListAsync(ct);
        var byId = venues.ToDictionary(v => v.Id);
        var images = await ReadyImages(db, venues, ct);
        http.Response.Headers.CacheControl = "no-store";
        return Ok(http, new
        {
            items = rows.Select(row => new
            {
                row.Id, byId[row.Id].Name, byId[row.Id].Address, byId[row.Id].CityCode,
                byId[row.Id].Latitude, byId[row.Id].Longitude, imageUrl = ImageUrl(byId[row.Id], images),
                row.MatchedCourtId, row.EstimatedTotalVnd
            }),
            nextCursor = hasMore ? Cursor(page.Offset + page.Limit) : null,
            date, startsAt = start.ToString("HH:mm", CultureInfo.InvariantCulture),
            endsAt = end.ToString("HH:mm", CultureInfo.InvariantCulture), durationMinutes = duration,
            currency = "VND", generatedAt = now
        });
    }
}
