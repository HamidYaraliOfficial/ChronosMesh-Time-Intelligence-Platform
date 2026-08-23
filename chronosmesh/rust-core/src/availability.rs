//! Availability Engine: projects a user's declarative `WorkingHours` pattern
//! onto a concrete UTC date range, subtracts busy intervals (meetings,
//! tasks, breaks, holidays), and answers the "what is actually free"
//! questions the product surfaces (next slot, total free time, remaining
//! working time, etc).
//!
//! Timezone correctness is the entire point of this module: every local
//! wall-clock minute is resolved through `chrono_tz` so Daylight Saving
//! transitions (spring-forward gaps, fall-back ambiguous hours) and leap
//! years are handled by the timezone database rather than by hand-rolled
//! offset math.

use crate::types::{AvailabilitySummary, TimeInterval, WorkingHours};
use chrono::{DateTime, Datelike, Duration, LocalResult, NaiveDate, NaiveTime, TimeZone, Utc};
use chrono_tz::Tz;
use std::str::FromStr;

#[derive(Debug, thiserror::Error)]
pub enum AvailabilityError {
    #[error("unknown IANA timezone: {0}")]
    InvalidTimezone(String),
    #[error("range end must be after range start")]
    InvalidRange,
}

/// Resolve a single local (date, minute-of-day) pair to a UTC instant.
/// DST gaps (a local time that never occurs, e.g. 02:30 during a
/// spring-forward) are resolved to the first valid instant after the gap.
/// DST ambiguity (a local time that occurs twice, e.g. during fall-back) is
/// resolved to the *earlier* of the two occurrences, matching common
/// calendar-application behaviour.
fn local_minute_to_utc(tz: Tz, date: NaiveDate, minute_of_day: u16) -> DateTime<Utc> {
    let hour = (minute_of_day / 60) as u32;
    let minute = (minute_of_day % 60) as u32;
    let naive_time = NaiveTime::from_hms_opt(hour.min(23), minute.min(59), 0)
        .unwrap_or_else(|| NaiveTime::from_hms_opt(23, 59, 0).unwrap());
    let naive_dt = date.and_time(naive_time);
    match tz.from_local_datetime(&naive_dt) {
        LocalResult::Single(dt) => dt.with_timezone(&Utc),
        LocalResult::Ambiguous(earlier, _later) => earlier.with_timezone(&Utc),
        LocalResult::None => {
            // Spring-forward gap: walk forward in 1-minute steps (bounded)
            // until we land on a resolvable local instant.
            let mut probe = naive_dt;
            for _ in 0..180 {
                probe += Duration::minutes(1);
                if let LocalResult::Single(dt) = tz.from_local_datetime(&probe) {
                    return dt.with_timezone(&Utc);
                }
            }
            // Fallback: treat as UTC directly rather than panic.
            Utc.from_utc_datetime(&naive_dt)
        }
    }
}

/// Expand a `WorkingHours` definition into concrete, DST-correct working
/// intervals (with breaks already carved out) across `[range_start,
/// range_end)`.
pub fn expand_working_intervals(
    working_hours: &WorkingHours,
    range: TimeInterval,
) -> Result<Vec<TimeInterval>, AvailabilityError> {
    if range.end <= range.start {
        return Err(AvailabilityError::InvalidRange);
    }
    let tz = Tz::from_str(&working_hours.timezone)
        .map_err(|_| AvailabilityError::InvalidTimezone(working_hours.timezone.clone()))?;

    let local_start_date = range.start.with_timezone(&tz).date_naive();
    let local_end_date = range.end.with_timezone(&tz).date_naive();

    let mut intervals = Vec::new();
    let mut date = local_start_date;
    // Iterate one calendar day at a time; NaiveDate handles leap years
    // (Feb 29) and month-length transitions natively via `succ_opt`.
    while date <= local_end_date {
        let iso_weekday = date.weekday().num_days_from_monday() as u8; // 0=Mon..6=Sun
        let date_str = date.format("%Y-%m-%d").to_string();
        if working_hours.holidays.iter().any(|h| h == &date_str) {
            date = match date.succ_opt() {
                Some(d) => d,
                None => break,
            };
            continue;
        }

        for day in working_hours.days.iter().filter(|d| d.weekday == iso_weekday) {
            let day_start = local_minute_to_utc(tz, date, day.start_minute);
            let day_end = local_minute_to_utc(tz, date, day.end_minute);
            if day_end <= day_start {
                continue;
            }
            let mut segments = vec![TimeInterval::new(day_start, day_end)];

            for &(b_start, b_end) in &day.breaks {
                let break_start = local_minute_to_utc(tz, date, b_start);
                let break_end = local_minute_to_utc(tz, date, b_end);
                let brk = TimeInterval::new(break_start.min(break_end), break_start.max(break_end));
                segments = subtract_interval(&segments, &brk);
            }
            intervals.extend(segments);
        }

        date = match date.succ_opt() {
            Some(d) => d,
            None => break,
        };
    }

    // Clip to the requested UTC range and merge/sort.
    let clipped: Vec<TimeInterval> = intervals
        .into_iter()
        .filter_map(|i| i.intersect(&range))
        .collect();
    Ok(merge_sorted(clipped))
}

/// Subtract `busy` from every interval in `segments`, returning the
/// remaining free pieces (an interval can split into zero, one or two
/// pieces).
pub fn subtract_interval(segments: &[TimeInterval], busy: &TimeInterval) -> Vec<TimeInterval> {
    let mut out = Vec::with_capacity(segments.len());
    for seg in segments {
        if !seg.overlaps(busy) {
            out.push(*seg);
            continue;
        }
        if busy.start > seg.start {
            out.push(TimeInterval::new(seg.start, busy.start.min(seg.end)));
        }
        if busy.end < seg.end {
            out.push(TimeInterval::new(busy.end.max(seg.start), seg.end));
        }
    }
    out.into_iter().filter(|i| !i.is_empty()).collect()
}

/// Subtract a whole set of busy intervals from a whole set of free
/// candidate intervals.
pub fn subtract_all(free: &[TimeInterval], busy: &[TimeInterval]) -> Vec<TimeInterval> {
    let mut current = free.to_vec();
    for b in busy {
        current = subtract_interval(&current, b);
    }
    merge_sorted(current)
}

/// Sort and merge overlapping/adjacent intervals.
pub fn merge_sorted(mut intervals: Vec<TimeInterval>) -> Vec<TimeInterval> {
    intervals.sort();
    let mut merged: Vec<TimeInterval> = Vec::with_capacity(intervals.len());
    for iv in intervals {
        if let Some(last) = merged.last_mut() {
            if let Some(m) = last.merge(&iv) {
                *last = m;
                continue;
            }
        }
        merged.push(iv);
    }
    merged
}

/// Compute the free intervals for a working-hours definition after removing
/// a set of busy intervals (meetings, tasks, existing bookings).
pub fn compute_free_intervals(
    working_hours: &WorkingHours,
    busy: &[TimeInterval],
    range: TimeInterval,
) -> Result<Vec<TimeInterval>, AvailabilityError> {
    let working = expand_working_intervals(working_hours, range)?;
    Ok(subtract_all(&working, busy))
}

/// Produce the full availability summary the product's Dashboard and
/// Availability views consume: next slot, total free time today/this week,
/// remaining working time today, time until next available slot.
pub fn summarize_availability(
    working_hours: &WorkingHours,
    busy: &[TimeInterval],
    now: DateTime<Utc>,
) -> Result<AvailabilitySummary, AvailabilityError> {
    let tz = Tz::from_str(&working_hours.timezone)
        .map_err(|_| AvailabilityError::InvalidTimezone(working_hours.timezone.clone()))?;

    let local_now = now.with_timezone(&tz);
    let today_start = local_minute_to_utc(tz, local_now.date_naive(), 0);
    let today_end = local_minute_to_utc(tz, local_now.date_naive(), 24 * 60 - 1) + Duration::minutes(1);

    let week_start_date = local_now.date_naive()
        - Duration::days(local_now.weekday().num_days_from_monday() as i64);
    let week_start = local_minute_to_utc(tz, week_start_date, 0);
    let week_end = week_start + Duration::days(7);

    let today_range = TimeInterval::new(today_start, today_end);
    let week_range = TimeInterval::new(week_start, week_end);

    let free_today = compute_free_intervals(working_hours, busy, today_range)?;
    let free_week = compute_free_intervals(working_hours, busy, week_range)?;

    let total_free_today: i64 = free_today.iter().map(|i| i.duration_minutes()).sum();
    let total_free_week: i64 = free_week.iter().map(|i| i.duration_minutes()).sum();

    let remaining_today: i64 = free_today
        .iter()
        .filter_map(|i| i.intersect(&TimeInterval::new(now, today_end)))
        .map(|i| i.duration_minutes())
        .sum();

    // Search forward up to 30 days for the next available slot.
    let horizon = TimeInterval::new(now, now + Duration::days(30));
    let future_free = compute_free_intervals(working_hours, busy, horizon)?;
    let next_slot = future_free.into_iter().find(|i| i.end > now);
    let minutes_until_next = next_slot.map(|s| (s.start.max(now) - now).num_minutes());

    Ok(AvailabilitySummary {
        free_intervals: free_today,
        next_available_slot: minutes_until_next.and(
            compute_free_intervals(working_hours, busy, TimeInterval::new(now, now + Duration::days(30)))?
                .into_iter()
                .find(|i| i.end > now),
        ),
        total_free_minutes_today: total_free_today,
        total_free_minutes_week: total_free_week,
        remaining_working_minutes_today: remaining_today.max(0),
        minutes_until_next_available: minutes_until_next,
    })
}
