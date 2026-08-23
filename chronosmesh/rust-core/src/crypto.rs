//! Secure Core: password hashing, symmetric encryption for secrets at
//! rest, and secure identifier generation. Exposed both as a Rust library
//! (for the desktop client / offline cache) and over HTTP (for the C#
//! backend and Go scheduler) via `bin/server.rs`.

use aes_gcm::aead::{Aead, KeyInit, OsRng as AesOsRng};
use aes_gcm::{Aes256Gcm, Key, Nonce};
use argon2::password_hash::{PasswordHash, PasswordHasher, PasswordVerifier, SaltString};
use argon2::Argon2;
use base64::{engine::general_purpose::STANDARD as B64, Engine};
use rand::RngCore;
use sha2::{Digest, Sha256};
use uuid::Uuid;

#[derive(Debug, thiserror::Error)]
pub enum CryptoError {
    #[error("password hashing failed: {0}")]
    HashFailed(String),
    #[error("password verification failed")]
    VerifyFailed,
    #[error("encryption failed: {0}")]
    EncryptFailed(String),
    #[error("decryption failed: {0}")]
    DecryptFailed(String),
    #[error("invalid key length, expected 32 bytes")]
    InvalidKeyLength,
    #[error("invalid base64 payload")]
    InvalidPayload,
}

/// Hash a plaintext password with Argon2id and a fresh random salt.
/// Returns a self-describing PHC string safe to store directly.
pub fn hash_password(plaintext: &str) -> Result<String, CryptoError> {
    let salt = SaltString::generate(&mut rand::rngs::OsRng);
    let argon2 = Argon2::default();
    argon2
        .hash_password(plaintext.as_bytes(), &salt)
        .map(|h| h.to_string())
        .map_err(|e| CryptoError::HashFailed(e.to_string()))
}

/// Verify a plaintext password against a previously stored PHC hash.
pub fn verify_password(plaintext: &str, stored_hash: &str) -> Result<bool, CryptoError> {
    let parsed = PasswordHash::new(stored_hash).map_err(|e| CryptoError::HashFailed(e.to_string()))?;
    Ok(Argon2::default().verify_password(plaintext.as_bytes(), &parsed).is_ok())
}

/// Encrypt `plaintext` with AES-256-GCM using `key` (32 raw bytes, e.g.
/// derived from an env-configured master key). Returns base64(nonce ||
/// ciphertext).
pub fn encrypt(key: &[u8], plaintext: &[u8]) -> Result<String, CryptoError> {
    if key.len() != 32 {
        return Err(CryptoError::InvalidKeyLength);
    }
    let cipher = Aes256Gcm::new(Key::<Aes256Gcm>::from_slice(key));
    let mut nonce_bytes = [0u8; 12];
    AesOsRng.fill_bytes(&mut nonce_bytes);
    let nonce = Nonce::from_slice(&nonce_bytes);
    let ciphertext = cipher
        .encrypt(nonce, plaintext)
        .map_err(|e| CryptoError::EncryptFailed(e.to_string()))?;
    let mut out = Vec::with_capacity(12 + ciphertext.len());
    out.extend_from_slice(&nonce_bytes);
    out.extend_from_slice(&ciphertext);
    Ok(B64.encode(out))
}

/// Reverse of `encrypt`.
pub fn decrypt(key: &[u8], payload_b64: &str) -> Result<Vec<u8>, CryptoError> {
    if key.len() != 32 {
        return Err(CryptoError::InvalidKeyLength);
    }
    let raw = B64.decode(payload_b64).map_err(|_| CryptoError::InvalidPayload)?;
    if raw.len() < 12 {
        return Err(CryptoError::InvalidPayload);
    }
    let (nonce_bytes, ciphertext) = raw.split_at(12);
    let cipher = Aes256Gcm::new(Key::<Aes256Gcm>::from_slice(key));
    let nonce = Nonce::from_slice(nonce_bytes);
    cipher
        .decrypt(nonce, ciphertext)
        .map_err(|e| CryptoError::DecryptFailed(e.to_string()))
}

/// Generate a cryptographically-random, URL-safe identifier suitable for
/// booking links, refresh tokens, etc. Distinct from entity primary keys
/// (which use UUIDv4 directly).
pub fn secure_identifier(byte_len: usize) -> String {
    let mut bytes = vec![0u8; byte_len];
    rand::rngs::OsRng.fill_bytes(&mut bytes);
    B64.encode(bytes).replace(['+', '/'], "-").replace('=', "")
}

pub fn new_entity_id() -> Uuid {
    Uuid::new_v4()
}

/// Stable SHA-256 fingerprint, used for integrity-checking cached offline
/// payloads before applying them during sync.
pub fn fingerprint(data: &[u8]) -> String {
    let mut hasher = Sha256::new();
    hasher.update(data);
    hex::encode(hasher.finalize())
}

// Minimal inline hex encoder to avoid pulling in an extra crate just for
// this one call site.
mod hex {
    pub fn encode(bytes: impl AsRef<[u8]>) -> String {
        bytes.as_ref().iter().map(|b| format!("{:02x}", b)).collect()
    }
}
