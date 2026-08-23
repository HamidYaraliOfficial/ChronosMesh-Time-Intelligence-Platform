//! HTTP microservice wrapper around `chronosmesh_core`, consumed internally
//! by the C# backend (business logic) and the Go scheduler (background
//! jobs). Not exposed to the public internet — sits behind the API gateway
//! / backend on the internal Docker network.

use axum::extract::State;
use axum::http::StatusCode;
use axum::routing::{get, post};
use axum::{Json, Router};
use chronosmesh_core::types::{
    AvailabilitySummary, EventDefinition, SchedulingResult, TaskRequirement, TimeInterval, WorkingHours,
};
use chronosmesh_core::{availability, crypto, recurrence, scheduling};
use serde::{Deserialize, Serialize};
use std::env;
use std::net::SocketAddr;
use std::sync::Arc;

#[derive(Clone)]
struct AppState {
    encryption_key: Arc<Vec<u8>>,
}

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt()
        .with_env_filter(tracing_subscriber::EnvFilter::from_default_env().add_directive("info".parse().unwrap()))
        .init();

    let key_hex = env::var("CHRONOSMESH_MASTER_KEY")
        .unwrap_or_else(|_| "00000000000000000000000000000000000000000000000000000000000000".to_string());
    let key_bytes = decode_hex_key(&key_hex);

    let state = AppState { encryption_key: Arc::new(key_bytes) };

    let app = Router::new()
        .route("/v1/health", get(health))
        .route("/v1/availability/compute", post(compute_availability))
        .route("/v1/availability/summary", post(compute_summary))
        .route("/v1/recurrence/expand", post(expand_recurrence))
        .route("/v1/scheduling/allocate", post(allocate_tasks))
        .route("/v1/crypto/hash-password", post(hash_password_handler))
        .route("/v1/crypto/verify-password", post(verify_password_handler))
        .route("/v1/crypto/encrypt", post(encrypt_handler))
        .route("/v1/crypto/decrypt", post(decrypt_handler))
        .route("/v1/crypto/secure-id", post(secure_id_handler))
        .with_state(state)
        .layer(tower_http::trace::TraceLayer::new_for_http())
        .layer(tower_http::cors::CorsLayer::permissive());

    let port: u16 = env::var("PORT").ok().and_then(|p| p.parse().ok()).unwrap_or(7301);
    let addr = SocketAddr::from(([0, 0, 0, 0], port));
    tracing::info!("chronosmesh-core-server listening on {addr}");
    let listener = tokio::net::TcpListener::bind(addr).await.unwrap();
    axum::serve(listener, app).await.unwrap();
}

fn decode_hex_key(hex: &str) -> Vec<u8> {
    (0..hex.len())
        .step_by(2)
        .filter_map(|i| hex.get(i..i + 2).and_then(|s| u8::from_str_radix(s, 16).ok()))
        .collect()
}

async fn health() -> Json<serde_json::Value> {
    Json(serde_json::json!({ "status": "ok", "service": "chronosmesh-core-server", "version": chronosmesh_core::VERSION }))
}

#[derive(Deserialize)]
struct AvailabilityRequest {
    working_hours: WorkingHours,
    busy: Vec<TimeInterval>,
    range: TimeInterval,
}

async fn compute_availability(
    Json(req): Json<AvailabilityRequest>,
) -> Result<Json<Vec<TimeInterval>>, (StatusCode, String)> {
    availability::compute_free_intervals(&req.working_hours, &req.busy, req.range)
        .map(Json)
        .map_err(|e| (StatusCode::BAD_REQUEST, e.to_string()))
}

#[derive(Deserialize)]
struct SummaryRequest {
    working_hours: WorkingHours,
    busy: Vec<TimeInterval>,
    now: chrono::DateTime<chrono::Utc>,
}

async fn compute_summary(
    Json(req): Json<SummaryRequest>,
) -> Result<Json<AvailabilitySummary>, (StatusCode, String)> {
    availability::summarize_availability(&req.working_hours, &req.busy, req.now)
        .map(Json)
        .map_err(|e| (StatusCode::BAD_REQUEST, e.to_string()))
}

#[derive(Deserialize)]
struct RecurrenceRequest {
    event: EventDefinition,
    window: TimeInterval,
}

async fn expand_recurrence(
    Json(req): Json<RecurrenceRequest>,
) -> Result<Json<Vec<recurrence::Occurrence>>, (StatusCode, String)> {
    recurrence::expand_occurrences(&req.event, req.window)
        .map(Json)
        .map_err(|e| (StatusCode::BAD_REQUEST, e.to_string()))
}

#[derive(Deserialize)]
struct SchedulingRequest {
    tasks: Vec<TaskRequirement>,
    free_intervals: Vec<TimeInterval>,
}

async fn allocate_tasks(Json(req): Json<SchedulingRequest>) -> Json<Vec<SchedulingResult>> {
    Json(scheduling::schedule_tasks(&req.tasks, &req.free_intervals))
}

#[derive(Deserialize)]
struct PasswordRequest {
    password: String,
}

#[derive(Serialize)]
struct HashResponse {
    hash: String,
}

async fn hash_password_handler(
    Json(req): Json<PasswordRequest>,
) -> Result<Json<HashResponse>, (StatusCode, String)> {
    crypto::hash_password(&req.password)
        .map(|hash| Json(HashResponse { hash }))
        .map_err(|e| (StatusCode::INTERNAL_SERVER_ERROR, e.to_string()))
}

#[derive(Deserialize)]
struct VerifyRequest {
    password: String,
    hash: String,
}

#[derive(Serialize)]
struct VerifyResponse {
    valid: bool,
}

async fn verify_password_handler(
    Json(req): Json<VerifyRequest>,
) -> Result<Json<VerifyResponse>, (StatusCode, String)> {
    crypto::verify_password(&req.password, &req.hash)
        .map(|valid| Json(VerifyResponse { valid }))
        .map_err(|e| (StatusCode::BAD_REQUEST, e.to_string()))
}

#[derive(Deserialize)]
struct EncryptRequest {
    plaintext_base64: String,
}

#[derive(Serialize)]
struct EncryptResponse {
    ciphertext_base64: String,
}

async fn encrypt_handler(
    State(state): State<AppState>,
    Json(req): Json<EncryptRequest>,
) -> Result<Json<EncryptResponse>, (StatusCode, String)> {
    use base64::{engine::general_purpose::STANDARD as B64, Engine};
    let plaintext = B64
        .decode(&req.plaintext_base64)
        .map_err(|_| (StatusCode::BAD_REQUEST, "invalid base64".to_string()))?;
    crypto::encrypt(&state.encryption_key, &plaintext)
        .map(|ciphertext_base64| Json(EncryptResponse { ciphertext_base64 }))
        .map_err(|e| (StatusCode::INTERNAL_SERVER_ERROR, e.to_string()))
}

#[derive(Deserialize)]
struct DecryptRequest {
    ciphertext_base64: String,
}

#[derive(Serialize)]
struct DecryptResponse {
    plaintext_base64: String,
}

async fn decrypt_handler(
    State(state): State<AppState>,
    Json(req): Json<DecryptRequest>,
) -> Result<Json<DecryptResponse>, (StatusCode, String)> {
    use base64::{engine::general_purpose::STANDARD as B64, Engine};
    crypto::decrypt(&state.encryption_key, &req.ciphertext_base64)
        .map(|plaintext| Json(DecryptResponse { plaintext_base64: B64.encode(plaintext) }))
        .map_err(|e| (StatusCode::BAD_REQUEST, e.to_string()))
}

#[derive(Deserialize)]
struct SecureIdRequest {
    #[serde(default = "default_len")]
    length: usize,
}
fn default_len() -> usize {
    24
}

#[derive(Serialize)]
struct SecureIdResponse {
    id: String,
}

async fn secure_id_handler(Json(req): Json<SecureIdRequest>) -> Json<SecureIdResponse> {
    Json(SecureIdResponse { id: crypto::secure_identifier(req.length) })
}
