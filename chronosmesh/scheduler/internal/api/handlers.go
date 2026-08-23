// Package api exposes the scheduler's HTTP surface: job submission,
// health/stats, and the WebSocket upgrade endpoint for real-time sync.
// Intended to be called from the C# backend (server-to-server) and, for
// the WebSocket endpoint, directly from the Desktop Client and Web App.
package api

import (
	"encoding/json"
	"log/slog"
	"net/http"

	"chronosmesh/scheduler/internal/queue"
	"chronosmesh/scheduler/internal/worker"
	"chronosmesh/scheduler/internal/ws"
)

type Server struct {
	Queue  *queue.Queue
	Pool   *worker.Pool
	Hub    *ws.Hub
	Logger *slog.Logger
}

func (s *Server) Routes() *http.ServeMux {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /v1/health", s.handleHealth)
	mux.HandleFunc("GET /v1/stats", s.handleStats)
	mux.HandleFunc("POST /v1/jobs", s.handleEnqueueJob)
	mux.HandleFunc("GET /v1/ws", s.handleWebSocket)
	return mux
}

func (s *Server) handleHealth(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{"status": "ok", "service": "chronosmesh-scheduler"})
}

func (s *Server) handleStats(w http.ResponseWriter, r *http.Request) {
	processed, failed, retried := s.Pool.Stats()
	writeJSON(w, http.StatusOK, map[string]any{
		"queue_depth":       s.Queue.Len(),
		"processed":         processed,
		"failed":            failed,
		"retried":           retried,
		"connected_clients": s.Hub.ConnectedClients(),
	})
}

type enqueueRequest struct {
	Type        string         `json:"type"`
	Priority    int            `json:"priority"`
	WorkspaceID string         `json:"workspace_id"`
	Payload     map[string]any `json:"payload"`
}

func (s *Server) handleEnqueueJob(w http.ResponseWriter, r *http.Request) {
	var req enqueueRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid JSON body"})
		return
	}
	if req.WorkspaceID == "" || req.Type == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "type and workspace_id are required"})
		return
	}

	job := queue.Job{
		Type:        queue.JobType(req.Type),
		Priority:    queue.Priority(req.Priority),
		WorkspaceID: req.WorkspaceID,
		Payload:     req.Payload,
	}
	if err := s.Queue.Enqueue(job); err != nil {
		s.Logger.Warn("enqueue rejected", "error", err)
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusAccepted, map[string]string{"job_id": job.ID, "status": "queued"})
}

func (s *Server) handleWebSocket(w http.ResponseWriter, r *http.Request) {
	workspaceID := r.URL.Query().Get("workspace_id")
	userID := r.URL.Query().Get("user_id")
	if workspaceID == "" || userID == "" {
		http.Error(w, "workspace_id and user_id query params are required", http.StatusBadRequest)
		return
	}
	s.Hub.ServeWS(w, r, workspaceID, userID)
}

func writeJSON(w http.ResponseWriter, status int, body any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(body)
}
