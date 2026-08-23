//! Conflict Resolution: used by the Desktop Client's Offline Mode when
//! reconciling locally-edited records with the server after reconnecting,
//! and by the Availability Engine when checking whether a proposed booking
//! collides with existing events.

use crate::types::TimeInterval;
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};

/// A versioned record as seen by either the local offline cache or the
/// server, carrying enough metadata to make a last-writer-wins decision
/// with a deterministic tiebreaker.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct VersionedRecord<T> {
    pub entity_id: uuid::Uuid,
    pub updated_at: DateTime<Utc>,
    /// Monotonic per-device counter, used to break exact-timestamp ties
    /// deterministically instead of relying on clock precision alone.
    pub device_clock: u64,
    pub device_id: String,
    pub payload: T,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum Resolution {
    KeepLocal,
    KeepRemote,
    /// Timestamps and device clocks were identical — resolved by comparing
    /// device_id lexicographically for full determinism.
    KeepRemoteTiebreak,
}

/// Resolve a conflict between a local (offline-edited) and remote
/// (server-authoritative) version of the same record using Last-Write-Wins
/// semantics with a deterministic tiebreaker.
pub fn resolve<T>(local: &VersionedRecord<T>, remote: &VersionedRecord<T>) -> Resolution {
    if local.updated_at != remote.updated_at {
        return if local.updated_at > remote.updated_at {
            Resolution::KeepLocal
        } else {
            Resolution::KeepRemote
        };
    }
    if local.device_clock != remote.device_clock {
        return if local.device_clock > remote.device_clock {
            Resolution::KeepLocal
        } else {
            Resolution::KeepRemote
        };
    }
    if local.device_id == remote.device_id {
        return Resolution::KeepRemote;
    }
    if local.device_id < remote.device_id {
        Resolution::KeepRemoteTiebreak
    } else {
        Resolution::KeepLocal
    }
}

/// Double-booking detection: does `candidate` collide with any interval in
/// `existing`? Returns the first colliding interval, if any.
pub fn find_conflict(candidate: &TimeInterval, existing: &[TimeInterval]) -> Option<TimeInterval> {
    existing.iter().find(|e| e.overlaps(candidate)).copied()
}

/// Batch variant: returns every colliding interval rather than the first.
pub fn find_all_conflicts(candidate: &TimeInterval, existing: &[TimeInterval]) -> Vec<TimeInterval> {
    existing.iter().filter(|e| e.overlaps(candidate)).copied().collect()
}
