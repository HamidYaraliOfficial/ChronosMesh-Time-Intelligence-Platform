// Package ws implements the Real-Time Synchronization layer: a WebSocket
// hub that keeps every connected client of a Workspace informed the moment
// another member changes a calendar event, task, or booking.
package ws

import (
	"encoding/json"
	"log/slog"
	"net/http"
	"sync"
	"time"

	"github.com/gorilla/websocket"
)

var upgrader = websocket.Upgrader{
	ReadBufferSize:  1024,
	WriteBufferSize: 1024,
	// The desktop client, web app, and admin panel are the only expected
	// origins; the API gateway enforces the allow-list in production. The
	// scheduler itself stays permissive so it can sit behind different
	// front-ends in dev/staging.
	CheckOrigin: func(r *http.Request) bool { return true },
}

// Event is a real-time notification broadcast to every client subscribed
// to a workspace.
type Event struct {
	Type        string    `json:"type"` // e.g. "event.updated", "task.moved", "booking.created"
	WorkspaceID string    `json:"workspace_id"`
	ActorUserID string    `json:"actor_user_id"`
	EntityID    string    `json:"entity_id"`
	Payload     any       `json:"payload"`
	Timestamp   time.Time `json:"timestamp"`
}

type client struct {
	conn        *websocket.Conn
	send        chan Event
	workspaceID string
	userID      string
}

// Hub fans out Events to all clients currently connected to the same
// workspace, so a change made by one team member appears live for
// everyone else.
type Hub struct {
	mu       sync.RWMutex
	clients  map[string]map[*client]bool // workspaceID -> set of clients
	logger   *slog.Logger
}

func NewHub(logger *slog.Logger) *Hub {
	return &Hub{
		clients: make(map[string]map[*client]bool),
		logger:  logger,
	}
}

// ServeWS upgrades an HTTP request to a WebSocket connection and registers
// the client under the given workspace.
func (h *Hub) ServeWS(w http.ResponseWriter, r *http.Request, workspaceID, userID string) {
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		h.logger.Error("websocket upgrade failed", "error", err)
		return
	}
	c := &client{conn: conn, send: make(chan Event, 32), workspaceID: workspaceID, userID: userID}

	h.mu.Lock()
	if h.clients[workspaceID] == nil {
		h.clients[workspaceID] = make(map[*client]bool)
	}
	h.clients[workspaceID][c] = true
	h.mu.Unlock()

	h.logger.Info("client connected", "workspace_id", workspaceID, "user_id", userID)

	go c.writePump(h)
	c.readPump(h)
}

func (c *client) readPump(h *Hub) {
	defer h.unregister(c)
	c.conn.SetReadLimit(4096)
	for {
		if _, _, err := c.conn.ReadMessage(); err != nil {
			return
		}
		// Clients don't send data other than pings/pongs; any inbound
		// message is treated as a liveness signal and discarded.
	}
}

func (c *client) writePump(h *Hub) {
	ticker := time.NewTicker(30 * time.Second)
	defer func() {
		ticker.Stop()
		c.conn.Close()
	}()
	for {
		select {
		case event, ok := <-c.send:
			if !ok {
				c.conn.WriteMessage(websocket.CloseMessage, []byte{})
				return
			}
			data, err := json.Marshal(event)
			if err != nil {
				continue
			}
			if err := c.conn.WriteMessage(websocket.TextMessage, data); err != nil {
				return
			}
		case <-ticker.C:
			if err := c.conn.WriteMessage(websocket.PingMessage, nil); err != nil {
				return
			}
		}
	}
}

func (h *Hub) unregister(c *client) {
	h.mu.Lock()
	defer h.mu.Unlock()
	if set, ok := h.clients[c.workspaceID]; ok {
		delete(set, c)
		close(c.send)
		if len(set) == 0 {
			delete(h.clients, c.workspaceID)
		}
	}
	h.logger.Info("client disconnected", "workspace_id", c.workspaceID, "user_id", c.userID)
}

// Broadcast pushes `event` to every client currently connected to
// event.WorkspaceID. Non-blocking per-client: a slow/stuck client is
// dropped rather than allowed to back-pressure the whole workspace.
func (h *Hub) Broadcast(event Event) {
	if event.Timestamp.IsZero() {
		event.Timestamp = time.Now().UTC()
	}
	h.mu.RLock()
	defer h.mu.RUnlock()
	for c := range h.clients[event.WorkspaceID] {
		select {
		case c.send <- event:
		default:
			h.logger.Warn("dropping event for slow client", "workspace_id", event.WorkspaceID, "user_id", c.userID)
		}
	}
}

// ConnectedClients returns the current connection count per workspace, for
// the /v1/stats endpoint.
func (h *Hub) ConnectedClients() map[string]int {
	h.mu.RLock()
	defer h.mu.RUnlock()
	out := make(map[string]int, len(h.clients))
	for ws, set := range h.clients {
		out[ws] = len(set)
	}
	return out
}
