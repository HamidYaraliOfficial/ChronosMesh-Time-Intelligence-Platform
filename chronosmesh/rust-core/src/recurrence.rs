//! Recurrence expansion: turns an `EventDefinition` with an optional
//! `RecurrenceRule` into concrete occurrences within a UTC window.
//!
//! Expansion always walks *local* calendar dates in the event's own
//! timezone and re-resolves each occurrence's wall-clock start/end through
//! `chrono_tz`, so a recurring 09:00 meeting stays at 09:00 local time
//! across DST transitions even though its UTC offset changes twice a year.
//! Leap years fall out naturally because we operate on `NaiveDate`.

use crate::types::{EventDefinition, RecurrenceFrequency, TimeInterval};
use chrono::{DateTime, Datelike, Duration, LocalResult, NaiveDate, TimeZone, Utc};
use chrono_tz::Tz;
use std::str::FromStr;

#[derive(Debug, thiserror::Error)]
pub enum RecurrenceError {
    #[error("unknown IANA timezone: {0}")]
    InvalidTimezone(String),
}

/// A single materialized occurrence of a recurring (or one-off) event.
#[derive(Debug, Clone, serde::Serialize)]
pub struct Occurrence {
    pub event_id: uuid::Uuid,
    pub interval: TimeInterval,
}

fn resolve_local(tz: Tz, naive: chrono::NaiveDateTime) -> DateTime<Utc> {
    match tz.from_local_datetime(&naive) {
        LocalResult::Single(dt) => dt.with_timezone(&Utc),
        LocalResult::Ambiguous(earlier, _) => earlier.with_timezone(&Utc),
        LocalResult::None => {
            let mut probe = naive;
            for _ in 0..180 {
                probe += Duration::minutes(1);
                if let LocalResult::Single(dt) = tz.from_local_datetime(&probe) {
                    return dt.with_timezone(&Utc);
                }
            }
            Utc.from_utc_datetime(&naive)
        }
    }
}

/// Expand `event` into all occurrences intersecting `window`.
pub fn expand_occurrences(
    event: &EventDefinition,
    window: TimeInterval,
) -> Result<Vec<Occurrence>, RecurrenceError> {
    let tz = Tz::from_str(&event.timezone).map_err(|_| RecurrenceError::InvalidTimezone(event.timezone.clone()))?;

    let Some(rule) = &event.recurrence else {
        return Ok(single_occurrence(event, window));
    };

    let duration = event.interval.duration();
    let local_origin = event.interval.start.with_timezone(&tz);
    let origin_naive_time = local_origin.time();
    let mut occurrences = Vec::new();
    let mut count_emitted: u32 = 0;
    let max_count = rule.count.unwrap_or(u32::MAX);
    let until = rule.until.unwrap_or(window.end);

    let interval_step = rule.interval.max(1);

    match rule.frequency {
        RecurrenceFrequency::Daily => {
            let mut date = local_origin.date_naive();
            loop {
                let occ_start = resolve_local(tz, date.and_time(origin_naive_time));
                if occ_start > until || occ_start > window.end || count_emitted >= max_count {
                    break;
                }
                push_if_in_window(&mut occurrences, event.id, occ_start, duration, window);
                count_emitted += 1;
                date = advance_date(date, Duration::days(interval_step as i64));
            }
        }
        RecurrenceFrequency::Weekly | RecurrenceFrequency::Custom => {
            let weekdays: Vec<u8> = if rule.by_weekday.is_empty() {
                vec![local_origin.weekday().num_days_from_monday() as u8]
            } else {
                rule.by_weekday.clone()
            };
            let week_start = local_origin.date_naive()
                - Duration::days(local_origin.weekday().num_days_from_monday() as i64);
            let mut week_cursor = week_start;
            'weeks: loop {
                for &wd in &weekdays {
                    let date = week_cursor + Duration::days(wd as i64);
                    if date < local_origin.date_naive() {
                        continue;
                    }
                    let occ_start = resolve_local(tz, date.and_time(origin_naive_time));
                    if occ_start > until || occ_start > window.end {
                        continue;
                    }
                    if count_emitted >= max_count {
                        break 'weeks;
                    }
                    push_if_in_window(&mut occurrences, event.id, occ_start, duration, window);
                    count_emitted += 1;
                }
                if week_cursor > until.with_timezone(&tz).date_naive() + Duration::days(7) {
                    break;
                }
                week_cursor += Duration::days(7 * interval_step as i64);
                if week_cursor.year() > window.end.with_timezone(&tz).year() + 1 {
                    break;
                }
            }
        }
        RecurrenceFrequency::Monthly => {
            let day_of_month = rule.by_month_day.unwrap_or(local_origin.day());
            let mut year = local_origin.year();
            let mut month = local_origin.month();
            loop {
                if let Some(date) = safe_ymd(year, month, day_of_month) {
                    let occ_start = resolve_local(tz, date.and_time(origin_naive_time));
                    if occ_start > until || occ_start > window.end || count_emitted >= max_count {
                        break;
                    }
                    if occ_start >= event.interval.start {
                        push_if_in_window(&mut occurrences, event.id, occ_start, duration, window);
                        count_emitted += 1;
                    }
                }
                month += interval_step;
                while month > 12 {
                    month -= 12;
                    year += 1;
                }
                if year > window.end.with_timezone(&tz).year() + 1 {
                    break;
                }
            }
        }
        RecurrenceFrequency::Yearly => {
            let mut year = local_origin.year();
            loop {
                if let Some(date) = safe_ymd(year, local_origin.month(), local_origin.day()) {
                    let occ_start = resolve_local(tz, date.and_time(origin_naive_time));
                    if occ_start > until || occ_start > window.end || count_emitted >= max_count {
                        break;
                    }
                    if occ_start >= event.interval.start {
                        push_if_in_window(&mut occurrences, event.id, occ_start, duration, window);
                        count_emitted += 1;
                    }
                }
                year += interval_step as i32;
                if year > window.end.with_timezone(&tz).year() + 1 {
                    break;
                }
            }
        }
    }

    Ok(occurrences)
}

/// Build a leap-year-safe date, returning `None` for e.g. Feb 30 or Feb 29
/// on a non-leap year (the occurrence is simply skipped for that period,
/// matching common calendar-application semantics for "31st of every
/// month").
fn safe_ymd(year: i32, month: u32, day: u32) -> Option<NaiveDate> {
    NaiveDate::from_ymd_opt(year, month, day)
}

fn advance_date(date: NaiveDate, step: Duration) -> NaiveDate {
    date + step
}

fn push_if_in_window(
    out: &mut Vec<Occurrence>,
    event_id: uuid::Uuid,
    occ_start: DateTime<Utc>,
    duration: Duration,
    window: TimeInterval,
) {
    let occ = TimeInterval::new(occ_start, occ_start + duration);
    if occ.overlaps(&window) {
        out.push(Occurrence { event_id, interval: occ });
    }
}

fn single_occurrence(event: &EventDefinition, window: TimeInterval) -> Vec<Occurrence> {
    if event.interval.overlaps(&window) {
        vec![Occurrence { event_id: event.id, interval: event.interval }]
    } else {
        vec![]
    }
}
