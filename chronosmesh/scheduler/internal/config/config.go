// Package config centralizes environment-variable driven configuration, so
// nothing in the scheduler service is hard-coded.
package config

import (
	"os"
	"strconv"
)

type Config struct {
	Port           string
	CoreEngineURL  string
	WorkerCount    int
	QueueCapacity  int
	JobTimeoutSecs int
	LogLevel       string
	AllowedOrigins string
}

func Load() Config {
	return Config{
		Port:           getEnv("SCHEDULER_PORT", "8081"),
		CoreEngineURL:  getEnv("CORE_ENGINE_URL", "http://rust-core:7301"),
		WorkerCount:    getEnvInt("SCHEDULER_WORKER_COUNT", 64),
		QueueCapacity:  getEnvInt("SCHEDULER_QUEUE_CAPACITY", 5000),
		JobTimeoutSecs: getEnvInt("SCHEDULER_JOB_TIMEOUT_SECONDS", 30),
		LogLevel:       getEnv("LOG_LEVEL", "info"),
		AllowedOrigins: getEnv("ALLOWED_ORIGINS", "*"),
	}
}

func getEnv(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func getEnvInt(key string, fallback int) int {
	if v := os.Getenv(key); v != "" {
		if n, err := strconv.Atoi(v); err == nil {
			return n
		}
	}
	return fallback
}
