//! # chronosmesh-core
//!
//! The Secure Core & Time Engine of the ChronosMesh platform.
//!
//! This crate is the single source of truth for every timezone-sensitive
//! and security-sensitive calculation in ChronosMesh:
//!
//! - [`availability`] — projects working hours into concrete free/busy time
//!   and answers "what's free right now" queries.
//! - [`recurrence`] — expands recurring events (daily/weekly/monthly/yearly
//!   /custom) into concrete, DST-correct occurrences.
//! - [`scheduling`] — the Smart Scheduling allocator that splits tasks
//!   across free time while respecting deadlines and priorities.
//! - [`crypto`] — password hashing, AES-256-GCM encryption, secure ID
//!   generation.
//! - [`conflict`] — last-write-wins conflict resolution for offline sync,
//!   plus double-booking detection.
//!
//! It is consumed in two ways:
//! 1. As a native Rust library, embedded directly in the C++ desktop
//!    client's offline engine via the `cdylib` target (see
//!    `docs/ARCHITECTURE.md` for the FFI boundary).
//! 2. As a small HTTP microservice (`bin/server.rs`, built as
//!    `chronosmesh-core-server`) consumed by the C# backend and the Go
//!    scheduler over the internal Docker network.

pub mod availability;
pub mod conflict;
pub mod crypto;
pub mod recurrence;
pub mod scheduling;
pub mod types;

pub const VERSION: &str = env!("CARGO_PKG_VERSION");
