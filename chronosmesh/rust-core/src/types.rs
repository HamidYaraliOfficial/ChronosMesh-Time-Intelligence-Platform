//! Core domain types shared across the ChronosMesh Time Engine.
//!
//! All wall-clock persistence happens in UTC (`DateTime<Utc>`). Local,
//! timezone-aware representations are only materialized transiently when
//! evaluating recurrence rules or working-hour windows, so that Daylight
//! Saving Time transitions and leap years are handled correctly at the
//! point of conversion instead of being baked into stored data.

use chrono::{DateTime, Duration, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

/// A half-open time interval `[start, end)` expressed in UTC.
#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct TimeInterval {
    pub start: DateTime<Utc>,
    pub end: DateTime<Utc>,
}

impl TimeInterval {
    pub fn new(start: DateTime<Utc>, end: DateTime<Utc>) -> Self {
        assert!(end >= start, "interval end must not precede start");
        Self { start, end }
    }

    pub fn duration(&self) -> Duration {
        self.end - self.start
    }

    pub fn duration_minutes(&self) -> i64 {
        self.duration().num_minutes()
    }

    /// Whether this interval overlaps `other` (half-open semantics: touching
    /// endpoints do not count as an overlap).
    pub fn overlaps(&self, other: &TimeInterval) -> bool {
        self.start < other.end && other.start < self.end
    }

    /// Whether `other` is fully contained within `self`.
    pub fn contains(&self, other: &TimeInterval) -> bool {
        self.start <= other.start && self.end >= other.end
    }

    pub fn intersect(&self, other: &TimeInterval) -> Option<TimeInterval> {
        let start = self.start.max(other.start);
        let end = self.end.min(other.end);
        if start < end {
            Some(TimeInterval::new(start, end))
        } else {
            None
        }
    }

    /// Merge two overlapping/adjacent intervals into one. Returns `None` if
    /// there is a gap between them.
    pub fn merge(&self, other: &TimeInterval) -> Option<TimeInterval> {
        if self.overlaps(other) || self.end == other.start || other.end == self.start {
            Some(TimeInterval::new(self.start.min(other.start), self.end.max(other.end)))
        } else {
            None
        }
    }

    pub fn is_empty(&self) -> bool {
        self.end <= self.start
    }
}

/// A single working day definition, expressed in *minutes from local
/// midnight* so it is independent of any particular calendar date. Breaks
/// are carved out of the working window at resolution time.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WorkingDay {
    /// ISO weekday: 0 = Monday .. 6 = Sunday
    pub weekday: u8,
    pub start_minute: u16,
    pub end_minute: u16,
    /// Break windows expressed as (start_minute, end_minute), local time.
    pub breaks: Vec<(u16, u16)>,
}

/// A user or workspace's full weekly working-hours definition, anchored to
/// an IANA timezone name (e.g. "Asia/Tehran", "Europe/Berlin",
/// "Asia/Shanghai"). This is the source of truth the Availability Engine
/// projects into concrete UTC intervals for a given date range.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WorkingHours {
    pub timezone: String,
    pub days: Vec<WorkingDay>,
    /// Dates (YYYY-MM-DD, in `timezone`) that are fully unavailable
    /// (holidays, days off) regardless of the weekly pattern.
    pub holidays: Vec<String>,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
pub enum RecurrenceFrequency {
    Daily,
    Weekly,
    Monthly,
    Yearly,
    Custom,
}

/// RRULE-inspired recurrence definition. Kept intentionally small and
/// explicit rather than pulling in a full RFC-5545 parser, since ChronosMesh
/// only needs to support the recurrence shapes exposed in the product UI.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RecurrenceRule {
    pub frequency: RecurrenceFrequency,
    /// Repeat every N units of `frequency` (e.g. every 2 weeks).
    pub interval: u32,
    /// For Weekly/Custom: ISO weekdays (0=Mon..6=Sun) the event occurs on.
    pub by_weekday: Vec<u8>,
    /// For Monthly/Yearly: day-of-month (1-31) the event occurs on. If
    /// absent, the day-of-month of the original event start is used.
    pub by_month_day: Option<u32>,
    pub until: Option<DateTime<Utc>>,
    pub count: Option<u32>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct EventDefinition {
    pub id: Uuid,
    pub title: String,
    pub interval: TimeInterval,
    pub timezone: String,
    pub recurrence: Option<RecurrenceRule>,
}

/// A unit of schedulable work the Smart Scheduling engine tries to place
/// into free time.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskRequirement {
    pub id: Uuid,
    pub title: String,
    pub duration_minutes: i64,
    pub deadline: Option<DateTime<Utc>>,
    /// 1 (lowest) .. 5 (highest)
    pub priority: u8,
    pub splittable: bool,
    /// Minimum contiguous chunk length when `splittable` is true.
    pub min_chunk_minutes: i64,
    pub preferred_intervals: Vec<TimeInterval>,
    pub forbidden_intervals: Vec<TimeInterval>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ScheduledChunk {
    pub task_id: Uuid,
    pub interval: TimeInterval,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SchedulingResult {
    pub task_id: Uuid,
    pub chunks: Vec<ScheduledChunk>,
    pub fully_scheduled: bool,
    pub completion_time: Option<DateTime<Utc>>,
    pub unscheduled_minutes: i64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AvailabilitySummary {
    pub free_intervals: Vec<TimeInterval>,
    pub next_available_slot: Option<TimeInterval>,
    pub total_free_minutes_today: i64,
    pub total_free_minutes_week: i64,
    pub remaining_working_minutes_today: i64,
    pub minutes_until_next_available: Option<i64>,
}
