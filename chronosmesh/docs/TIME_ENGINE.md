# The ChronosMesh Time Engine (Rust)

Every timezone-sensitive calculation in ChronosMesh flows through
`rust-core/src/availability.rs`, `recurrence.rs`, and `scheduling.rs`. This
document explains the guarantees those modules provide and why they live
in Rust rather than being re-implemented per-language.

## Guarantees

1. **Timezone correctness per user.** A user in Tehran, a manager in
   Germany, and a colleague in Shanghai each declare working hours in
   their own IANA timezone (`Asia/Tehran`, `Europe/Berlin`,
   `Asia/Shanghai`). The engine resolves every local wall-clock minute
   through `chrono-tz`, never through fixed UTC offsets, so the correct
   instant is computed for each person independently.

2. **DST safety.** Spring-forward gaps (a local time that never occurs) and
   fall-back ambiguity (a local time that occurs twice) are both handled
   explicitly rather than left to panic or silent incorrect math — see
   `local_minute_to_utc` in `availability.rs`.

3. **Leap years.** Because the engine walks `chrono::NaiveDate` day-by-day
   (via `succ_opt()`) rather than doing manual date arithmetic, Feb 29 is
   handled the same way any other date is — no special-casing needed, and
   yearly recurrences on Feb 29 correctly skip non-leap years (verified in
   `yearly_recurrence_skips_feb_29_on_non_leap_years`).

4. **Deterministic conflict resolution.** Offline edits reconciled after
   reconnecting use last-write-wins by timestamp, with a device-clock and
   then device-id tiebreak so two devices that edited at the exact same
   millisecond still resolve deterministically (`conflict.rs`).

## Test coverage

`rust-core/tests/time_engine_tests.rs` includes dedicated cases for:
DST spring-forward, DST fall-back, cross-timezone offset differences,
leap-year expansion, daily/weekly/monthly/yearly recurrence, task
splitting across multiple free slots, deadline-constrained scheduling,
priority-based allocation, password hashing, AES-GCM round-trips, and
booking conflict detection. Run with:

```bash
cd rust-core
cargo test
```
