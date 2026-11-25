package protocol

import (
	"bufio"
	"context"
	"encoding/json"
	"fmt"
	"net"
	"sync"
	"time"

	"hteam/modx-autoskh/internal/model"
)

// ClientMsg gói tin NDJSON từ Client Mod -> Manager Go.
type ClientMsg struct {
	Type      string          `json:"type"` // "hello" | "telemetry" | "event"
	AccountID string          `json:"account_id"`
	Username  string          `json:"username"`
	PID       int             `json:"pid,omitempty"`
	BuildSig  string          `json:"sig,omitempty"`
	Payload   json.RawMessage `json:"payload,omitempty"`
}

// TelemetryPayload trạng thái live đẩy từ client (chỉ gửi khi có biến đổi hoặc heartbeat 3s).
type TelemetryPayload struct {
	CharName      string           `json:"char_name"`
	Gender        int              `json:"gender"`
	MapID         int              `json:"map_id"`
	MapName       string           `json:"map_name"`
	ZoneID        int              `json:"zone_id"`
	ZonePlayers   int              `json:"zone_players"`
	ZoneMax       int              `json:"zone_max"`
	CX            int              `json:"cx"`
	CY            int              `json:"cy"`
	InPrivateMap  bool             `json:"in_private_map"`
	TicketRemain  string           `json:"ticket_remain"`
	LastExpSec    int              `json:"last_exp_sec"`
	HitsPerMin    int              `json:"hits_per_min"`
	TotalHits     int              `json:"total_hits"`
	TargetMob     string           `json:"target_mob"`
	ActiveSkill   string           `json:"active_skill"`
	HP            int64            `json:"hp"`
	HPMax         int64            `json:"hp_max"`
	MP            int64            `json:"mp"`
	MPMax         int64            `json:"mp_max"`
	Power         int64            `json:"power"`
	Gold          int64            `json:"gold"`
	Gem           int              `json:"gem"`
	GemLock       int              `json:"gem_lock"`
	TDLTMinutes   int              `json:"tdlt_minutes"`
	HasTDLTActive bool             `json:"has_tdlt_active"`
	HasLuckyGrass bool             `json:"has_lucky_grass"`
	IsNewMember   bool             `json:"is_new_member"`
	MamRemain     string           `json:"mam_remain"`
	LuckPercent   int              `json:"luck_percent"`
	LuckDetail    string           `json:"luck_detail"`
	TitleName     string           `json:"title_name"`
	TitleRemain   string           `json:"title_remain"`
	TitleExpiryMs int64            `json:"title_expiry_ms"`
	BagUsed       int              `json:"bag_used"`
	BagMax        int              `json:"bag_max"`
	BoxUsed       int              `json:"box_used"`
	BoxMax        int              `json:"box_max"`
	FSMState      string           `json:"fsm_state"`
	StatusText    string           `json:"status_text"`
	Paused        bool             `json:"paused"`
	BlackScreen   bool             `json:"black_screen"`
	ActiveProxy   string           `json:"active_proxy"`
	Loot          []model.LootItem `json:"loot,omitempty"`
}

// EventPayload sự kiện nóng từ client.
type EventPayload struct {
	Kind    string          `json:"kind"` // "skh_drop" | "box_full" | "low_gem" | "stuck" | "log"
	Message string          `json:"message"`
	Item    *model.LootItem `json:"item,omitempty"`
}

// ServerCmd gói tin NDJSON lệnh nóng từ Manager Go -> Client Mod.
type ServerCmd struct {
	Cmd  string      `json:"cmd"` // "profile_update" | "pause" | "resume" | "switch_zone" | "force_sell" | "force_store" | "toggle_black_screen" | "exit"
	Data interface{} `json:"data,omitempty"`
}

type clientConn struct {
	accountID string
	conn      net.Conn
	writeMu   sync.Mutex
}

func (c *clientConn) send(cmd ServerCmd) error {
	b, err := json.Marshal(cmd)
	if err != nil {
		return err
	}
	b = append(b, '\n')
	c.writeMu.Lock()
	defer c.writeMu.Unlock()
	_ = c.conn.SetWriteDeadline(time.Now().Add(5 * time.Second))
	_, err = c.conn.Write(b)
	return err
}

// Handlers callback khi nhận gói từ client hoặc khi ngắt kết nối.
type Handlers struct {
	OnHello      func(accountID, username string, pid int, sig string)
	OnTelemetry  func(accountID string, t TelemetryPayload)
	OnEvent      func(accountID string, e EventPayload)
	OnDisconnect func(accountID string)
}

// Hub quản lý kết nối TCP NDJSON duy nhất trên 127.0.0.1 (an toàn đa luồng, mỗi client 1 buffer riêng).
type Hub struct {
	port     int
	handlers Handlers

	mu       sync.RWMutex
	listener net.Listener
	clients  map[string]*clientConn
	closed   bool
}

func NewHub(port int, handlers Handlers) *Hub {
	return &Hub{
		port:     port,
		handlers: handlers,
		clients:  make(map[string]*clientConn),
	}
}

func (h *Hub) Start(ctx context.Context) error {
	addr := fmt.Sprintf("127.0.0.1:%d", h.port)
	ln, err := net.Listen("tcp", addr)
	if err != nil {
		return fmt.Errorf("không mở được cổng Socket %s: %w", addr, err)
	}
	h.mu.Lock()
	h.listener = ln
	h.mu.Unlock()

	go func() {
		<-ctx.Done()
		h.Close()
	}()

	go func() {
		for {
			conn, err := ln.Accept()
			if err != nil {
				h.mu.RLock()
				closed := h.closed
				h.mu.RUnlock()
				if closed {
					return
				}
				continue
			}
			go h.serveConn(conn)
		}
	}()
	return nil
}

func (h *Hub) Close() {
	h.mu.Lock()
	h.closed = true
	ln := h.listener
	conns := make([]*clientConn, 0, len(h.clients))
	for _, c := range h.clients {
		conns = append(conns, c)
	}
	h.clients = make(map[string]*clientConn)
	h.mu.Unlock()

	if ln != nil {
		_ = ln.Close()
	}
	for _, c := range conns {
		_ = c.conn.Close()
	}
}

func (h *Hub) serveConn(conn net.Conn) {
	defer conn.Close()
	cc := &clientConn{conn: conn}

	scanner := bufio.NewScanner(conn)
	// Buffer riêng 1MB cho mỗi tab, tuyệt đối không dùng chung buffer toàn cục!
	buf := make([]byte, 0, 64*1024)
	scanner.Buffer(buf, 1024*1024)

	var accountID string
	for scanner.Scan() {
		line := scanner.Bytes()
		if len(line) == 0 {
			continue
		}
		var msg ClientMsg
		if err := json.Unmarshal(line, &msg); err != nil {
			continue
		}
		if msg.AccountID != "" && accountID == "" {
			accountID = msg.AccountID
			cc.accountID = accountID
			h.registerClient(accountID, cc)
		}
		switch msg.Type {
		case "hello":
			if h.handlers.OnHello != nil && accountID != "" {
				h.handlers.OnHello(accountID, msg.Username, msg.PID, msg.BuildSig)
			}
		case "telemetry":
			if h.handlers.OnTelemetry != nil && accountID != "" && len(msg.Payload) > 0 {
				var tp TelemetryPayload
				if err := json.Unmarshal(msg.Payload, &tp); err == nil {
					h.handlers.OnTelemetry(accountID, tp)
				}
			}
		case "event":
			if h.handlers.OnEvent != nil && accountID != "" && len(msg.Payload) > 0 {
				var ep EventPayload
				if err := json.Unmarshal(msg.Payload, &ep); err == nil {
					h.handlers.OnEvent(accountID, ep)
				}
			}
		}
	}

	if accountID != "" {
		if h.unregisterClient(accountID, cc) && h.handlers.OnDisconnect != nil {
			h.handlers.OnDisconnect(accountID)
		}
	}
}

func (h *Hub) registerClient(accountID string, cc *clientConn) {
	h.mu.Lock()
	old, exists := h.clients[accountID]
	h.clients[accountID] = cc
	h.mu.Unlock()
	if exists && old != cc {
		_ = old.conn.Close()
	}
}

func (h *Hub) unregisterClient(accountID string, cc *clientConn) bool {
	h.mu.Lock()
	removed := false
	if cur, ok := h.clients[accountID]; ok && cur == cc {
		delete(h.clients, accountID)
		removed = true
	}
	h.mu.Unlock()
	return removed
}

// SendTo gửi lệnh nóng tới 1 tab đang kết nối.
func (h *Hub) SendTo(accountID string, cmd ServerCmd) error {
	h.mu.RLock()
	cc, ok := h.clients[accountID]
	h.mu.RUnlock()
	if !ok {
		return fmt.Errorf("acc %s chưa kết nối socket", accountID)
	}
	return cc.send(cmd)
}

// Broadcast gửi lệnh tới toàn bộ tab đang online.
func (h *Hub) Broadcast(cmd ServerCmd) {
	h.mu.RLock()
	list := make([]*clientConn, 0, len(h.clients))
	for _, cc := range h.clients {
		list = append(list, cc)
	}
	h.mu.RUnlock()
	for _, cc := range list {
		_ = cc.send(cmd)
	}
}

// IsConnected kiểm tra 1 acc có đang nối socket không.
func (h *Hub) IsConnected(accountID string) bool {
	h.mu.RLock()
	_, ok := h.clients[accountID]
	h.mu.RUnlock()
	return ok
}

// Disconnect chủ động ngắt và gỡ kết nối socket của 1 acc (dùng khi xóa/đổi acc để chặn gói telemetry cũ).
func (h *Hub) Disconnect(accountID string) {
	h.mu.Lock()
	cc, ok := h.clients[accountID]
	if ok {
		delete(h.clients, accountID)
	}
	h.mu.Unlock()
	if ok && cc != nil && cc.conn != nil {
		_ = cc.conn.Close()
	}
}
