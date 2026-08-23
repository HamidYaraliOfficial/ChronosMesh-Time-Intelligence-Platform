//! Smart Scheduling: given a `TaskRequirement` and a set of already-computed
//! free intervals (see `availability::compute_free_intervals`), greedily
//! allocate the task's required duration into the earliest-available slots,
//! honouring priority order between competing tasks, deadlines, minimum
//! chunk size, and preferred/forbidden windows.

use crate::types::{ScheduledChunk, SchedulingResult, TaskRequirement, TimeInterval};
use chrono::Duration;

/// Allocate a single task into the given free intervals. `free_intervals`
/// must already be sorted and non-overlapping (as returned by the
/// availability engine).
pub fn schedule_task(task: &TaskRequirement, free_intervals: &[TimeInterval]) -> SchedulingResult {
    let mut remaining = task.duration_minutes;
    let mut chunks: Vec<ScheduledChunk> = Vec::new();

    // Respect forbidden intervals by removing them, and prefer intervals
    // that intersect the task's preferred windows by considering those
    // first.
    let mut candidates: Vec<TimeInterval> = free_intervals
        .iter()
        .flat_map(|iv| carve_out_forbidden(*iv, &task.forbidden_intervals))
        .filter(|iv| task.deadline.map_or(true, |dl| iv.start < dl))
        .collect();

    candidates.sort_by_key(|iv| {
        let is_preferred = task
            .preferred_intervals
            .iter()
            .any(|p| p.overlaps(iv));
        // Preferred slots sort first (0), then chronological order.
        (if is_preferred { 0 } else { 1 }, iv.start)
    });

    for candidate in candidates {
        if remaining <= 0 {
            break;
        }
        let mut window = candidate;
        if let Some(deadline) = task.deadline {
            if window.start >= deadline {
                continue;
            }
            if window.end > deadline {
                window = TimeInterval::new(window.start, deadline);
            }
        }
        let available_minutes = window.duration_minutes();
        if available_minutes <= 0 {
            continue;
        }

        if !task.splittable {
            // Whole task must fit in one slot.
            if available_minutes >= remaining {
                let end = window.start + Duration::minutes(remaining);
                chunks.push(ScheduledChunk { task_id: task.id, interval: TimeInterval::new(window.start, end) });
                remaining = 0;
                break;
            }
            continue;
        }

        let take = remaining.min(available_minutes);
        if take < task.min_chunk_minutes && take != remaining {
            // Chunk would be smaller than the allowed minimum and doesn't
            // finish the task — skip this slot.
            continue;
        }
        let end = window.start + Duration::minutes(take);
        chunks.push(ScheduledChunk { task_id: task.id, interval: TimeInterval::new(window.start, end) });
        remaining -= take;
    }

    let completion_time = chunks.last().map(|c| c.interval.end);
    SchedulingResult {
        task_id: task.id,
        chunks,
        fully_scheduled: remaining <= 0,
        completion_time,
        unscheduled_minutes: remaining.max(0),
    }
}

/// Sequentially schedule multiple tasks against a shared pool of free time,
/// highest priority first, consuming free time as tasks are placed so that
/// two tasks are never double-booked into the same minutes.
pub fn schedule_tasks(tasks: &[TaskRequirement], free_intervals: &[TimeInterval]) -> Vec<SchedulingResult> {
    let mut ordered: Vec<&TaskRequirement> = tasks.iter().collect();
    ordered.sort_by(|a, b| b.priority.cmp(&a.priority).then(cmp_deadline(a, b)));

    let mut pool = free_intervals.to_vec();
    let mut results = Vec::with_capacity(ordered.len());

    for task in ordered {
        let result = schedule_task(task, &pool);
        for chunk in &result.chunks {
            pool = crate::availability::subtract_interval(&pool, &chunk.interval);
        }
        results.push(result);
    }
    results
}

fn cmp_deadline(a: &TaskRequirement, b: &TaskRequirement) -> std::cmp::Ordering {
    match (a.deadline, b.deadline) {
        (Some(x), Some(y)) => x.cmp(&y),
        (Some(_), None) => std::cmp::Ordering::Less,
        (None, Some(_)) => std::cmp::Ordering::Greater,
        (None, None) => std::cmp::Ordering::Equal,
    }
}

fn carve_out_forbidden(interval: TimeInterval, forbidden: &[TimeInterval]) -> Vec<TimeInterval> {
    let mut segments = vec![interval];
    for f in forbidden {
        segments = crate::availability::subtract_interval(&segments, f);
    }
    segments
}
