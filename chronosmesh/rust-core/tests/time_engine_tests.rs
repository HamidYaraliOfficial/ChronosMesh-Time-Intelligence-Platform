use chrono::{DateTime, TimeZone, Utc};
use chronosmesh_core::types::{
    EventDefinition, RecurrenceFrequency, RecurrenceRule, TaskRequirement, TimeInterval, WorkingDay, WorkingHours,
};
use chronosmesh_core::{availability, conflict, crypto, recurrence, scheduling};
use uuid::Uuid;

fn dt(y: i32, m: u32, d: u32, h: u32, mi: u32) -> DateTime<Utc> {
    Utc.with_ymd_and_hms(y, m, d, h, mi, 0).unwrap()
}

fn standard_working_hours(tz: &str) -> WorkingHours {
    WorkingHours {
        timezone: tz.to_string(),
        days: (0..5)
            .map(|wd| WorkingDay { weekday: wd, start_minute: 8 * 60, end_minute: 17 * 60, breaks: vec![(12 * 60 + 30, 13 * 60 + 30)] })
            .collect(),
        holidays: vec![],
    }
}

// ---------------------------------------------------------------------
// TimeInterval basics
// ---------------------------------------------------------------------

#[test]
fn interval_overlap_detection() {
    let a = TimeInterval::new(dt(2026, 3, 1, 9, 0), dt(2026, 3, 1, 10, 0));
    let b = TimeInterval::new(dt(2026, 3, 1, 9, 30), dt(2026, 3, 1, 10, 30));
    let c = TimeInterval::new(dt(2026, 3, 1, 10, 0), dt(2026, 3, 1, 11, 0));
    assert!(a.overlaps(&b));
    assert!(!a.overlaps(&c), "touching endpoints must not count as overlap");
}

#[test]
fn interval_intersection() {
    let a = TimeInterval::new(dt(2026, 3, 1, 9, 0), dt(2026, 3, 1, 12, 0));
    let b = TimeInterval::new(dt(2026, 3, 1, 10, 0), dt(2026, 3, 1, 13, 0));
    let i = a.intersect(&b).unwrap();
    assert_eq!(i.start, dt(2026, 3, 1, 10, 0));
    assert_eq!(i.end, dt(2026, 3, 1, 12, 0));
}

#[test]
fn interval_merge_adjacent() {
    let a = TimeInterval::new(dt(2026, 3, 1, 9, 0), dt(2026, 3, 1, 10, 0));
    let b = TimeInterval::new(dt(2026, 3, 1, 10, 0), dt(2026, 3, 1, 11, 0));
    let merged = a.merge(&b).unwrap();
    assert_eq!(merged.duration_minutes(), 120);
}

// ---------------------------------------------------------------------
// Working hours -> availability, breaks
// ---------------------------------------------------------------------

#[test]
fn working_hours_produce_break_gap() {
    let wh = standard_working_hours("Europe/Berlin");
    // Monday 2026-03-02 is a weekday.
    let range = TimeInterval::new(dt(2026, 3, 2, 0, 0), dt(2026, 3, 3, 0, 0));
    let intervals = availability::expand_working_intervals(&wh, range).unwrap();
    // Expect two segments: 08:00-12:30 and 13:30-17:00 local (CET = UTC+1
    // in March before DST switch on last Sunday of March).
    assert_eq!(intervals.len(), 2);
    let total_minutes: i64 = intervals.iter().map(|i| i.duration_minutes()).sum();
    assert_eq!(total_minutes, (17 - 8) * 60 - 60); // 8 working hours minus 1h break
}

#[test]
fn holiday_removes_whole_day() {
    let mut wh = standard_working_hours("Europe/Berlin");
    wh.holidays.push("2026-03-02".to_string());
    let range = TimeInterval::new(dt(2026, 3, 2, 0, 0), dt(2026, 3, 3, 0, 0));
    let intervals = availability::expand_working_intervals(&wh, range).unwrap();
    assert!(intervals.is_empty());
}

#[test]
fn busy_meeting_is_subtracted_from_free_time() {
    let wh = standard_working_hours("Europe/Berlin");
    let range = TimeInterval::new(dt(2026, 3, 2, 0, 0), dt(2026, 3, 3, 0, 0));
    let meeting = TimeInterval::new(dt(2026, 3, 2, 9, 0), dt(2026, 3, 2, 10, 0));
    let free = availability::compute_free_intervals(&wh, &[meeting], range).unwrap();
    assert!(free.iter().all(|f| !f.overlaps(&meeting)));
    let total_minutes: i64 = free.iter().map(|i| i.duration_minutes()).sum();
    assert_eq!(total_minutes, (17 - 8) * 60 - 60 - 60);
}

// ---------------------------------------------------------------------
// DST handling
// ---------------------------------------------------------------------

#[test]
fn dst_spring_forward_europe_berlin() {
    // 2026-03-29 is the last Sunday of March: Europe/Berlin springs
    // forward from CET (UTC+1) to CEST (UTC+2) at 02:00 local -> 03:00.
    let wh = WorkingHours {
        timezone: "Europe/Berlin".to_string(),
        days: vec![WorkingDay { weekday: 6, start_minute: 8 * 60, end_minute: 17 * 60, breaks: vec![] }], // Sunday
        holidays: vec![],
    };
    let range = TimeInterval::new(dt(2026, 3, 29, 0, 0), dt(2026, 3, 30, 0, 0));
    let intervals = availability::expand_working_intervals(&wh, range).unwrap();
    assert_eq!(intervals.len(), 1);
    // 08:00 CEST local == 06:00 UTC (post-transition offset is +2)
    assert_eq!(intervals[0].start, dt(2026, 3, 29, 6, 0));
    // 17:00 CEST local == 15:00 UTC
    assert_eq!(intervals[0].end, dt(2026, 3, 29, 15, 0));
}

#[test]
fn dst_fall_back_europe_berlin_does_not_panic() {
    // 2026-10-25 Europe/Berlin falls back from CEST to CET at 03:00 -> 02:00,
    // producing an ambiguous local hour. The engine must resolve
    // deterministically rather than erroring.
    let wh = WorkingHours {
        timezone: "Europe/Berlin".to_string(),
        days: vec![WorkingDay { weekday: 6, start_minute: 1 * 60 + 30, end_minute: 4 * 60, breaks: vec![] }],
        holidays: vec![],
    };
    let range = TimeInterval::new(dt(2026, 10, 25, 0, 0), dt(2026, 10, 26, 0, 0));
    let intervals = availability::expand_working_intervals(&wh, range).unwrap();
    assert_eq!(intervals.len(), 1);
    assert!(intervals[0].duration_minutes() > 0);
}

#[test]
fn timezone_offset_differs_across_users() {
    // Same wall-clock 09:00 start in three different timezones must map to
    // three different UTC instants.
    let tehran = standard_working_hours("Asia/Tehran");
    let berlin = standard_working_hours("Europe/Berlin");
    let shanghai = standard_working_hours("Asia/Shanghai");
    let range = TimeInterval::new(dt(2026, 3, 2, 0, 0), dt(2026, 3, 3, 0, 0));

    let t = availability::expand_working_intervals(&tehran, range).unwrap();
    let b = availability::expand_working_intervals(&berlin, range).unwrap();
    let s = availability::expand_working_intervals(&shanghai, range).unwrap();

    assert_ne!(t[0].start, b[0].start);
    assert_ne!(b[0].start, s[0].start);
    assert_ne!(t[0].start, s[0].start);
}

// ---------------------------------------------------------------------
// Leap year
// ---------------------------------------------------------------------

#[test]
fn leap_year_feb_29_expands_correctly() {
    let wh = WorkingHours {
        timezone: "UTC".to_string(),
        days: vec![WorkingDay { weekday: 6, start_minute: 9 * 60, end_minute: 10 * 60, breaks: vec![] }], // Sunday
        holidays: vec![],
    };
    // 2028-02-29 is a Tuesday in reality, so pick a real leap-year Sunday:
    // 2032-02-29 is a Sunday.
    let range = TimeInterval::new(dt(2032, 2, 29, 0, 0), dt(2032, 3, 1, 0, 0));
    let intervals = availability::expand_working_intervals(&wh, range).unwrap();
    assert_eq!(intervals.len(), 1);
    assert_eq!(intervals[0].start, dt(2032, 2, 29, 9, 0));
}

#[test]
fn yearly_recurrence_skips_feb_29_on_non_leap_years() {
    let event = EventDefinition {
        id: Uuid::new_v4(),
        title: "Leap day anniversary".to_string(),
        interval: TimeInterval::new(dt(2028, 2, 29, 9, 0), dt(2028, 2, 29, 10, 0)),
        timezone: "UTC".to_string(),
        recurrence: Some(RecurrenceRule {
            frequency: RecurrenceFrequency::Yearly,
            interval: 1,
            by_weekday: vec![],
            by_month_day: None,
            until: Some(dt(2033, 1, 1, 0, 0)),
            count: None,
        }),
    };
    let window = TimeInterval::new(dt(2028, 1, 1, 0, 0), dt(2033, 1, 1, 0, 0));
    let occurrences = recurrence::expand_occurrences(&event, window).unwrap();
    // Only 2028 and 2032 are leap years in this range.
    assert_eq!(occurrences.len(), 2);
}

// ---------------------------------------------------------------------
// Recurrence: daily / weekly / monthly / custom
// ---------------------------------------------------------------------

#[test]
fn daily_recurrence_expands_every_day() {
    let event = EventDefinition {
        id: Uuid::new_v4(),
        title: "Daily standup".to_string(),
        interval: TimeInterval::new(dt(2026, 3, 2, 9, 0), dt(2026, 3, 2, 9, 15)),
        timezone: "UTC".to_string(),
        recurrence: Some(RecurrenceRule {
            frequency: RecurrenceFrequency::Daily,
            interval: 1,
            by_weekday: vec![],
            by_month_day: None,
            until: None,
            count: Some(5),
        }),
    };
    let window = TimeInterval::new(dt(2026, 3, 1, 0, 0), dt(2026, 3, 10, 0, 0));
    let occurrences = recurrence::expand_occurrences(&event, window).unwrap();
    assert_eq!(occurrences.len(), 5);
}

#[test]
fn weekly_recurrence_monday_and_wednesday() {
    let event = EventDefinition {
        id: Uuid::new_v4(),
        title: "Every Monday and Wednesday at 10:00".to_string(),
        interval: TimeInterval::new(dt(2026, 3, 2, 10, 0), dt(2026, 3, 2, 11, 0)), // Monday
        timezone: "UTC".to_string(),
        recurrence: Some(RecurrenceRule {
            frequency: RecurrenceFrequency::Weekly,
            interval: 1,
            by_weekday: vec![0, 2], // Monday, Wednesday
            by_month_day: None,
            until: Some(dt(2026, 3, 23, 0, 0)),
            count: None,
        }),
    };
    let window = TimeInterval::new(dt(2026, 3, 1, 0, 0), dt(2026, 3, 23, 0, 0));
    let occurrences = recurrence::expand_occurrences(&event, window).unwrap();
    // 3 full weeks * 2 occurrences = 6
    assert_eq!(occurrences.len(), 6);
    for occ in &occurrences {
        let wd = occ.interval.start.format("%u").to_string(); // 1=Mon..7=Sun
        assert!(wd == "1" || wd == "3");
    }
}

#[test]
fn monthly_recurrence_respects_day_of_month() {
    let event = EventDefinition {
        id: Uuid::new_v4(),
        title: "Monthly report".to_string(),
        interval: TimeInterval::new(dt(2026, 1, 15, 9, 0), dt(2026, 1, 15, 9, 30)),
        timezone: "UTC".to_string(),
        recurrence: Some(RecurrenceRule {
            frequency: RecurrenceFrequency::Monthly,
            interval: 1,
            by_weekday: vec![],
            by_month_day: Some(15),
            until: None,
            count: Some(4),
        }),
    };
    let window = TimeInterval::new(dt(2026, 1, 1, 0, 0), dt(2026, 12, 31, 0, 0));
    let occurrences = recurrence::expand_occurrences(&event, window).unwrap();
    assert_eq!(occurrences.len(), 4);
    for occ in &occurrences {
        assert_eq!(occ.interval.start.format("%d").to_string(), "15");
    }
}

// ---------------------------------------------------------------------
// Smart scheduling / task splitting
// ---------------------------------------------------------------------

#[test]
fn splits_task_across_multiple_free_slots() {
    let task = TaskRequirement {
        id: Uuid::new_v4(),
        title: "Write proposal".to_string(),
        duration_minutes: 360, // 6 hours
        deadline: None,
        priority: 3,
        splittable: true,
        min_chunk_minutes: 30,
        preferred_intervals: vec![],
        forbidden_intervals: vec![],
    };
    let free = vec![
        TimeInterval::new(dt(2026, 3, 2, 8, 0), dt(2026, 3, 2, 10, 0)),  // Mon 2h
        TimeInterval::new(dt(2026, 3, 2, 14, 0), dt(2026, 3, 2, 16, 0)), // Mon 2h
        TimeInterval::new(dt(2026, 3, 3, 9, 0), dt(2026, 3, 3, 11, 0)),  // Tue 2h
    ];
    let result = scheduling::schedule_task(&task, &free);
    assert!(result.fully_scheduled);
    assert_eq!(result.chunks.len(), 3);
    assert_eq!(result.completion_time, Some(dt(2026, 3, 3, 11, 0)));
}

#[test]
fn non_splittable_task_needs_single_slot() {
    let task = TaskRequirement {
        id: Uuid::new_v4(),
        title: "Interview".to_string(),
        duration_minutes: 90,
        deadline: None,
        priority: 5,
        splittable: false,
        min_chunk_minutes: 90,
        preferred_intervals: vec![],
        forbidden_intervals: vec![],
    };
    let free = vec![
        TimeInterval::new(dt(2026, 3, 2, 8, 0), dt(2026, 3, 2, 9, 0)),   // too short
        TimeInterval::new(dt(2026, 3, 2, 14, 0), dt(2026, 3, 2, 16, 0)), // fits
    ];
    let result = scheduling::schedule_task(&task, &free);
    assert!(result.fully_scheduled);
    assert_eq!(result.chunks.len(), 1);
    assert_eq!(result.chunks[0].interval.start, dt(2026, 3, 2, 14, 0));
}

#[test]
fn task_respects_deadline_cutoff() {
    let task = TaskRequirement {
        id: Uuid::new_v4(),
        title: "Urgent fix".to_string(),
        duration_minutes: 120,
        deadline: Some(dt(2026, 3, 2, 9, 30)),
        priority: 5,
        splittable: true,
        min_chunk_minutes: 15,
        preferred_intervals: vec![],
        forbidden_intervals: vec![],
    };
    let free = vec![TimeInterval::new(dt(2026, 3, 2, 8, 0), dt(2026, 3, 2, 17, 0))];
    let result = scheduling::schedule_task(&task, &free);
    // Only 90 minutes available before the deadline (08:00-09:30).
    assert!(!result.fully_scheduled);
    assert_eq!(result.unscheduled_minutes, 30);
}

#[test]
fn higher_priority_task_gets_first_pick_of_shared_pool() {
    let free = vec![TimeInterval::new(dt(2026, 3, 2, 8, 0), dt(2026, 3, 2, 9, 0))];
    let low = TaskRequirement {
        id: Uuid::new_v4(),
        title: "Low priority".to_string(),
        duration_minutes: 60,
        deadline: None,
        priority: 1,
        splittable: false,
        min_chunk_minutes: 60,
        preferred_intervals: vec![],
        forbidden_intervals: vec![],
    };
    let high = TaskRequirement {
        id: Uuid::new_v4(),
        title: "High priority".to_string(),
        duration_minutes: 60,
        deadline: None,
        priority: 5,
        splittable: false,
        min_chunk_minutes: 60,
        preferred_intervals: vec![],
        forbidden_intervals: vec![],
    };
    let results = scheduling::schedule_tasks(&[low.clone(), high.clone()], &free);
    let high_result = results.iter().find(|r| r.task_id == high.id).unwrap();
    let low_result = results.iter().find(|r| r.task_id == low.id).unwrap();
    assert!(high_result.fully_scheduled);
    assert!(!low_result.fully_scheduled, "only one hour existed; low priority must lose it");
}

// ---------------------------------------------------------------------
// Crypto
// ---------------------------------------------------------------------

#[test]
fn password_hash_roundtrip() {
    let hash = crypto::hash_password("Sup3rSecret!").unwrap();
    assert!(crypto::verify_password("Sup3rSecret!", &hash).unwrap());
    assert!(!crypto::verify_password("wrong-password", &hash).unwrap());
}

#[test]
fn aes_gcm_encrypt_decrypt_roundtrip() {
    let key = [7u8; 32];
    let ciphertext = crypto::encrypt(&key, b"refresh-token-secret").unwrap();
    let plaintext = crypto::decrypt(&key, &ciphertext).unwrap();
    assert_eq!(plaintext, b"refresh-token-secret");
}

#[test]
fn secure_identifier_is_unique_and_urlsafe() {
    let a = crypto::secure_identifier(24);
    let b = crypto::secure_identifier(24);
    assert_ne!(a, b);
    assert!(!a.contains('+') && !a.contains('/') && !a.contains('='));
}

// ---------------------------------------------------------------------
// Conflict resolution
// ---------------------------------------------------------------------

#[test]
fn last_write_wins_by_timestamp() {
    use chronosmesh_core::conflict::{resolve, Resolution, VersionedRecord};
    let id = Uuid::new_v4();
    let local = VersionedRecord { entity_id: id, updated_at: dt(2026, 3, 2, 10, 0), device_clock: 1, device_id: "desktop-A".into(), payload: "local" };
    let remote = VersionedRecord { entity_id: id, updated_at: dt(2026, 3, 2, 11, 0), device_clock: 1, device_id: "server".into(), payload: "remote" };
    assert_eq!(resolve(&local, &remote), Resolution::KeepRemote);
}

#[test]
fn booking_conflict_detected() {
    let existing = vec![TimeInterval::new(dt(2026, 3, 2, 9, 0), dt(2026, 3, 2, 10, 0))];
    let candidate = TimeInterval::new(dt(2026, 3, 2, 9, 30), dt(2026, 3, 2, 10, 30));
    assert!(conflict::find_conflict(&candidate, &existing).is_some());

    let non_conflicting = TimeInterval::new(dt(2026, 3, 2, 10, 0), dt(2026, 3, 2, 11, 0));
    assert!(conflict::find_conflict(&non_conflicting, &existing).is_none());
}

// ---------------------------------------------------------------------
// Availability summary (next slot / totals)
// ---------------------------------------------------------------------

#[test]
fn availability_summary_reports_remaining_time() {
    let wh = standard_working_hours("UTC");
    let now = dt(2026, 3, 2, 10, 0); // Monday 10:00, inside working hours
    let summary = availability::summarize_availability(&wh, &[], now).unwrap();
    assert!(summary.remaining_working_minutes_today > 0);
    assert!(summary.total_free_minutes_week >= summary.total_free_minutes_today);
}
