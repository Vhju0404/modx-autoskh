package main

import (
	"context"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io/fs"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"time"

	"hteam/modx-autoskh/internal/launcher"
	"hteam/modx-autoskh/internal/loot"
	"hteam/modx-autoskh/internal/manager"
	"hteam/modx-autoskh/internal/model"
	"hteam/modx-autoskh/internal/proxy"
	"hteam/modx-autoskh/internal/store"
)

type App struct {
	store        *store.Store
	mgr          *manager.Manager
	activeUIPort int

	emitMu      sync.Mutex
	emitPending bool
	started     atomic.Bool
	stateSeq    atomic.Uint64

	sseMu   sync.Mutex
	sseSubs map[chan []byte]struct{}
}

func NewApp() *App {
	return &App{
		sseSubs: make(map[chan []byte]struct{}),
	}
}

func resolveBaseDir() string {
	baseDir := "."
	if exe, err := os.Executable(); err == nil {
		exeDir := filepath.Dir(exe)
		if _, err2 := os.Stat(filepath.Join(exeDir, "game")); err2 == nil {
			baseDir = exeDir
		} else if _, err3 := os.Stat(filepath.Join(exeDir, "data")); err3 == nil {
			baseDir = exeDir
		}
	}
	return baseDir
}

func (a *App) startup() {
	baseDir := resolveBaseDir()
	dataDir := filepath.Join(baseDir, "data")

	st, err := store.New(dataDir)
	if err != nil {
		panic(err)
	}
	a.store = st
	a.mgr = manager.New(st, a.scheduleUIEmit)
	a.started.Store(true)
	_ = a.mgr.Start()
}

func (a *App) shutdown() {
	a.started.Store(false)
	a.sseMu.Lock()
	for ch := range a.sseSubs {
		delete(a.sseSubs, ch)
		close(ch)
	}
	a.sseMu.Unlock()
	if a.mgr != nil {
		a.mgr.Stop()
	}
}

// SubscribeSSE đăng ký nhận gói LiveSnapshot JSON realtime từ backend (khi đóng trình duyệt = 0 subscriber, 0% CPU).
func (a *App) SubscribeSSE() (chan []byte, func()) {
	ch := make(chan []byte, 4)
	a.sseMu.Lock()
	a.sseSubs[ch] = struct{}{}
	a.sseMu.Unlock()
	unsub := func() {
		a.sseMu.Lock()
		if _, ok := a.sseSubs[ch]; ok {
			delete(a.sseSubs, ch)
			close(ch)
		}
		a.sseMu.Unlock()
	}
	return ch, unsub
}

func (a *App) broadcastSnapshot(snap LiveSnapshot) {
	a.sseMu.Lock()
	subCount := len(a.sseSubs)
	a.sseMu.Unlock()
	if subCount == 0 {
		return
	}
	payload, err := json.Marshal(snap)
	if err != nil {
		return
	}
	a.sseMu.Lock()
	for ch := range a.sseSubs {
		select {
		case ch <- payload:
		default:
			select {
			case <-ch:
			default:
			}
			select {
			case ch <- payload:
			default:
			}
		}
	}
	a.sseMu.Unlock()
}

// scheduleUIEmit gom sự kiện cập nhật tối đa ~3 lần/giây để UI luôn mượt 60FPS kể cả khi 50 tab đẩy telemetry.
func (a *App) scheduleUIEmit() {
	if !a.started.Load() {
		return
	}
	a.stateSeq.Add(1)
	a.emitMu.Lock()
	if a.emitPending {
		a.emitMu.Unlock()
		return
	}
	a.emitPending = true
	a.emitMu.Unlock()

	go func() {
		time.Sleep(300 * time.Millisecond)
		a.emitMu.Lock()
		a.emitPending = false
		a.emitMu.Unlock()
		if a.started.Load() {
			a.broadcastSnapshot(a.GetLiveSnapshot())
		}
	}()
}

// bumpAndEmitNow tăng stateSeq ngay lập tức và phát snapshot mới nhất xuống UI sau các thao tác Thêm / Sửa / Xóa.
func (a *App) bumpAndEmitNow() {
	a.stateSeq.Add(1)
	if a.started.Load() {
		a.broadcastSnapshot(a.GetLiveSnapshot())
	}
}

// --- Bootstrap & Live Snapshot ---

type BootstrapPayload struct {
	StateSeq    uint64                   `json:"state_seq"`
	Layout      store.LayoutMeta         `json:"layout"`
	Summary     model.DashboardSummary   `json:"summary"`
	Rows        []model.AccountRow       `json:"rows"`
	Profiles    []model.FarmProfile      `json:"profiles"`
	Servers     []model.ServerConfig     `json:"servers"`
	Proxies     []model.ProxyItem        `json:"proxies"`
	Settings    model.AppSettings        `json:"settings"`
	LootReport  loot.LootReport          `json:"loot_report"`
	LootHistory []model.LootHistoryEntry `json:"loot_history"`
	Logs        []model.LogEntry         `json:"logs"`
	MapPresets  []store.MapPreset        `json:"map_presets"`
	SKHSets     []loot.SetMeta           `json:"skh_sets"`
}

type LiveSnapshot struct {
	StateSeq    uint64                   `json:"state_seq"`
	Summary     model.DashboardSummary   `json:"summary"`
	Rows        []model.AccountRow       `json:"rows"`
	LootReport  loot.LootReport          `json:"loot_report"`
	LootHistory []model.LootHistoryEntry `json:"loot_history"`
	Logs        []model.LogEntry         `json:"logs"`
}

func (a *App) GetBootstrap() BootstrapPayload {
	seq := a.stateSeq.Load()
	sum := a.mgr.GetSummary()
	if a.activeUIPort > 0 {
		sum.UIPort = a.activeUIPort
	}
	return BootstrapPayload{
		StateSeq:    seq,
		Layout:      store.GetLayoutMeta(),
		Summary:     sum,
		Rows:        a.mgr.GetAccountRows(),
		Profiles:    a.store.GetProfiles(),
		Servers:     a.store.GetServers(),
		Proxies:     a.store.GetProxies(),
		Settings:    a.store.GetSettings(),
		LootReport:  a.mgr.GetLootReport(),
		LootHistory: a.store.GetLootHistory(),
		Logs:        a.mgr.GetLogs(),
		MapPresets:  store.PresetSKHMaps(),
		SKHSets:     loot.AllSKHSets,
	}
}

func (a *App) GetLiveSnapshot() LiveSnapshot {
	seq := a.stateSeq.Load()
	sum := a.mgr.GetSummary()
	if a.activeUIPort > 0 {
		sum.UIPort = a.activeUIPort
	}
	return LiveSnapshot{
		StateSeq:    seq,
		Summary:     sum,
		Rows:        a.mgr.GetAccountRows(),
		LootReport:  a.mgr.GetLootReport(),
		LootHistory: a.store.GetLootHistory(),
		Logs:        a.mgr.GetLogs(),
	}
}

// --- Quản lý Tài Khoản ---

type BulkAddRequest struct {
	RawText     string `json:"raw_text"`
	Tag         string `json:"tag"`
	ServerID    string `json:"server_id"`
	ProfileID   string `json:"profile_id"`
	MapID       int    `json:"map_id"`
	ZoneMode    string `json:"zone_mode"`
	ZoneID      int    `json:"zone_id"`
	ProxyID     string `json:"proxy_id"`
	CustomProxy string `json:"custom_proxy"`
}

type ProxyCheckResult struct {
	OK         bool   `json:"ok"`
	Type       string `json:"type"`
	Host       string `json:"host"`
	Port       int    `json:"port"`
	Username   string `json:"username"`
	Normalized string `json:"normalized"`
	PingMs     int64  `json:"ping_ms"`
	Message    string `json:"message"`
}

// CheckProxyString kiểm tra nhanh 1 proxy riêng (nhận mọi định dạng & tự nhận diện HTTP/SOCKS5).
func (a *App) CheckProxyString(raw string, serverID string) ProxyCheckResult {
	raw = strings.TrimSpace(raw)
	if raw == "" {
		return ProxyCheckResult{OK: false, Message: "Chưa nhập chuỗi proxy"}
	}
	targetHost := "dragon1.teamobi.com"
	targetPort := 14445
	if serverID != "" {
		if sv, ok := a.store.GetServerByID(serverID); ok && sv.Host != "" {
			targetHost = sv.Host
			if sv.Port > 0 {
				targetPort = sv.Port
			}
		}
	} else if svs := a.store.GetServers(); len(svs) > 0 {
		targetHost = svs[0].Host
		targetPort = svs[0].Port
	}

	ctx, cancel := context.WithTimeout(context.Background(), 12*time.Second)
	defer cancel()
	item, ok, ping, err := proxy.CheckOneAuto(ctx, raw, targetHost, targetPort, 5*time.Second)
	if item.Host == "" && err != nil {
		return ProxyCheckResult{OK: false, Message: err.Error()}
	}
	norm := proxy.NormalizeRaw(item)
	if ok {
		return ProxyCheckResult{
			OK:         true,
			Type:       item.Type,
			Host:       item.Host,
			Port:       item.Port,
			Username:   item.Username,
			Normalized: norm,
			PingMs:     ping,
			Message:    fmt.Sprintf("LIVE (%s · %d ms · %s:%d)", strings.ToUpper(item.Type), ping, item.Host, item.Port),
		}
	}
	errMsg := "Không kết nối được tới server NRO"
	if err != nil {
		errMsg = err.Error()
	}
	return ProxyCheckResult{
		OK:         false,
		Type:       item.Type,
		Host:       item.Host,
		Port:       item.Port,
		Username:   item.Username,
		Normalized: norm,
		PingMs:     0,
		Message:    fmt.Sprintf("DIE (%s:%d) — %s", item.Host, item.Port, errMsg),
	}
}

type BulkAddResult struct {
	Added   int `json:"added"`
	Skipped int `json:"skipped"`
}

func (a *App) AddAccountsBulk(req BulkAddRequest) (BulkAddResult, error) {
	rawText := strings.TrimPrefix(req.RawText, "\uFEFF")
	lines := strings.Split(strings.ReplaceAll(rawText, "\r\n", "\n"), "\n")
	existing := a.store.GetAccounts()
	existingSet := make(map[string]bool, len(existing))
	for _, acc := range existing {
		existingSet[strings.ToLower(acc.Username)+"@"+acc.ServerID] = true
	}

	if prof, ok := a.store.GetProfileByID(req.ProfileID); ok {
		req.ProfileID = prof.ID
	}
	if sv, ok := a.store.GetServerByID(req.ServerID); ok {
		req.ServerID = sv.ID
	}
	if req.ZoneMode == "" {
		req.ZoneMode = "profile"
	}
	defaultCustomProxy := strings.TrimSpace(req.CustomProxy)
	if defaultCustomProxy != "" {
		if cp, err := proxy.ParseLine(defaultCustomProxy, "http"); err != nil {
			return BulkAddResult{}, fmt.Errorf("proxy riêng không hợp lệ: %v", err)
		} else {
			defaultCustomProxy = proxy.NormalizeRaw(cp)
			req.ProxyID = "custom"
		}
	} else if req.ProxyID == "" {
		req.ProxyID = "auto"
	}

	added := 0
	skipped := 0
	for _, rawLine := range lines {
		line := strings.TrimSpace(rawLine)
		if line == "" || strings.HasPrefix(line, "#") {
			continue
		}
		sep := "|"
		if !strings.Contains(line, "|") {
			if strings.Contains(line, "\t") {
				sep = "\t"
			} else if strings.Contains(line, ":") {
				sep = ":"
			} else if strings.Contains(line, ",") {
				sep = ","
			}
		}
		parts := strings.Split(line, sep)
		if len(parts) < 2 {
			skipped++
			continue
		}
		user := strings.TrimSpace(parts[0])
		pass := strings.TrimSpace(parts[1])
		if user == "" {
			skipped++
			continue
		}

		svID := req.ServerID
		mapID := req.MapID
		zoneID := req.ZoneID
		zoneMode := req.ZoneMode
		tag := strings.TrimSpace(req.Tag)
		prxID := req.ProxyID
		customPrx := defaultCustomProxy

		if len(parts) == 3 && strings.Contains(parts[2], ":") {
			if cp, err := proxy.ParseLine(parts[2], "http"); err == nil {
				customPrx = proxy.NormalizeRaw(cp)
				prxID = "custom"
			}
		} else if sep == ":" && len(parts) >= 4 && strings.Contains(parts[2], ".") {
			if cp, err := proxy.ParseLine(strings.Join(parts[2:], ":"), "http"); err == nil {
				customPrx = proxy.NormalizeRaw(cp)
				prxID = "custom"
			}
		} else {
			if len(parts) >= 3 && strings.TrimSpace(parts[2]) != "" {
				svRaw := strings.TrimSpace(parts[2])
				if n, err := strconv.Atoi(svRaw); err == nil && n >= 1 && n <= 14 {
					svID = fmt.Sprintf("sv%d", n)
				} else {
					svID = svRaw
				}
			}
			if len(parts) >= 4 && strings.TrimSpace(parts[3]) != "" {
				if m, err := strconv.Atoi(strings.TrimSpace(parts[3])); err == nil {
					mapID = m
				}
			}
			if len(parts) >= 5 && strings.TrimSpace(parts[4]) != "" {
				if z, err := strconv.Atoi(strings.TrimSpace(parts[4])); err == nil {
					zoneID = z
					if z > 0 {
						zoneMode = "fixed"
					}
				}
			}
			if len(parts) >= 6 && strings.TrimSpace(parts[5]) != "" {
				tag = strings.TrimSpace(parts[5])
			}
			if len(parts) >= 7 && strings.TrimSpace(parts[6]) != "" {
				if cp, err := proxy.ParseLine(parts[6], "http"); err == nil {
					customPrx = proxy.NormalizeRaw(cp)
					prxID = "custom"
				}
			}
		}

		key := strings.ToLower(user) + "@" + svID
		if existingSet[key] {
			skipped++
			continue
		}
		existingSet[key] = true

		existing = append(existing, model.Account{
			ID:          newID("acc"),
			Tag:         tag,
			Username:    user,
			Password:    pass,
			ServerID:    svID,
			ProfileID:   req.ProfileID,
			MapID:       mapID,
			ZoneMode:    zoneMode,
			ZoneID:      zoneID,
			ProxyID:     prxID,
			CustomProxy: customPrx,
			CreatedAt:   model.NowUnixMs(),
		})
		added++
	}

	if added > 0 {
		if err := a.store.SaveAccounts(existing); err != nil {
			return BulkAddResult{}, err
		}
		a.bumpAndEmitNow()
	}
	return BulkAddResult{Added: added, Skipped: skipped}, nil
}

func (a *App) UpdateAccount(acc model.Account) error {
	acc.Username = strings.TrimSpace(acc.Username)
	if acc.Username == "" {
		return errors.New("tên tài khoản không được rỗng")
	}
	acc.CustomProxy = strings.TrimSpace(acc.CustomProxy)
	if acc.CustomProxy != "" {
		cp, err := proxy.ParseLine(acc.CustomProxy, "http")
		if err != nil {
			return fmt.Errorf("proxy riêng không hợp lệ: %v", err)
		}
		acc.CustomProxy = proxy.NormalizeRaw(cp)
		acc.ProxyID = "custom"
	} else if acc.ProxyID == "custom" {
		acc.ProxyID = "auto"
	}
	if prof, ok := a.store.GetProfileByID(acc.ProfileID); ok {
		acc.ProfileID = prof.ID
	}
	if sv, ok := a.store.GetServerByID(acc.ServerID); ok {
		acc.ServerID = sv.ID
	}

	oldAcc, hadOld := a.store.GetAccountByID(acc.ID)
	if acc.ID == "" {
		acc.ID = newID("acc")
		acc.CreatedAt = model.NowUnixMs()
	} else if hadOld {
		if !strings.EqualFold(oldAcc.Username, acc.Username) || oldAcc.ServerID != acc.ServerID {
			acc.TitleName = ""
			acc.TitleExpiryMs = 0
			acc.Done = false
			acc.DoneReason = ""
		}
	}
	if err := a.store.UpdateAccount(acc); err != nil {
		return err
	}
	if hadOld {
		a.mgr.SyncAccountUpdated(oldAcc, acc)
	}
	a.bumpAndEmitNow()
	return nil
}

type BulkAssignRequest struct {
	IDs         []string `json:"ids"`
	SetTag      bool     `json:"set_tag"`
	Tag         string   `json:"tag"`
	SetServer   bool     `json:"set_server"`
	ServerID    string   `json:"server_id"`
	SetProf     bool     `json:"set_profile"`
	ProfileID   string   `json:"profile_id"`
	SetMap      bool     `json:"set_map"`
	MapID       int      `json:"map_id"`
	SetZone     bool     `json:"set_zone"`
	ZoneMode    string   `json:"zone_mode"`
	ZoneID      int      `json:"zone_id"`
	SetProxy    bool     `json:"set_proxy"`
	ProxyID     string   `json:"proxy_id"`
	CustomProxy string   `json:"custom_proxy"`
}

func (a *App) BulkAssignAccounts(req BulkAssignRequest) error {
	customPrx := strings.TrimSpace(req.CustomProxy)
	if req.SetProxy && customPrx != "" {
		cp, err := proxy.ParseLine(customPrx, "http")
		if err != nil {
			return fmt.Errorf("proxy riêng không hợp lệ: %v", err)
		}
		customPrx = proxy.NormalizeRaw(cp)
		req.ProxyID = "custom"
	}
	idSet := make(map[string]bool, len(req.IDs))
	for _, id := range req.IDs {
		idSet[id] = true
	}
	list := a.store.GetAccounts()
	for i := range list {
		if !idSet[list[i].ID] {
			continue
		}
		if req.SetTag {
			list[i].Tag = strings.TrimSpace(req.Tag)
		}
		if req.SetServer && req.ServerID != "" {
			list[i].ServerID = req.ServerID
		}
		if req.SetProf && req.ProfileID != "" {
			list[i].ProfileID = req.ProfileID
		}
		if req.SetMap {
			list[i].MapID = req.MapID
		}
		if req.SetZone {
			list[i].ZoneMode = req.ZoneMode
			list[i].ZoneID = req.ZoneID
		}
		if req.SetProxy {
			if customPrx != "" {
				list[i].ProxyID = "custom"
				list[i].CustomProxy = customPrx
			} else if req.ProxyID != "" {
				list[i].ProxyID = req.ProxyID
				if req.ProxyID != "custom" {
					list[i].CustomProxy = ""
				}
			}
		}
	}
	if err := a.store.SaveAccounts(list); err != nil {
		return err
	}
	if req.SetProxy || req.SetServer {
		a.mgr.ResetCircuitBreaker(req.IDs)
	}
	if req.SetProf && req.ProfileID != "" {
		if prof, ok := a.store.GetProfileByID(req.ProfileID); ok {
			a.mgr.PushProfileUpdate(prof)
		}
	}
	a.bumpAndEmitNow()
	return nil
}

func (a *App) DeleteAccounts(ids []string) error {
	a.mgr.PurgeAccounts(ids)
	if err := a.store.DeleteAccounts(ids); err != nil {
		return err
	}
	a.bumpAndEmitNow()
	return nil
}

func (a *App) ResetDoneAccounts(ids []string) error {
	for _, id := range ids {
		_ = a.store.MarkAccountDone(id, false, "")
	}
	a.mgr.ResetCircuitBreaker(ids)
	a.scheduleUIEmit()
	return nil
}

func (a *App) ExportAccountsText(ids []string, onlyWithSKH bool, includePassword bool) string {
	rows := a.mgr.GetAccountRows()
	accAll := a.store.GetAccounts()
	passMap := make(map[string]string, len(accAll))
	for _, ac := range accAll {
		passMap[ac.ID] = ac.Password
	}

	filterIDs := len(ids) > 0
	idSet := make(map[string]bool, len(ids))
	for _, id := range ids {
		idSet[id] = true
	}

	var b strings.Builder
	for _, r := range rows {
		if filterIDs && !idSet[r.Account.ID] {
			continue
		}
		if onlyWithSKH && r.Runtime.SKHCount <= 0 {
			continue
		}
		if includePassword {
			fmt.Fprintf(&b, "%s|%s|%s|SKH:%d|Sao:%d\n", r.Account.Username, passMap[r.Account.ID], r.ServerName, r.Runtime.SKHCount, r.Runtime.StarCount)
		} else {
			fmt.Fprintf(&b, "%s|%s|SKH:%d|Sao:%d\n", r.Account.Username, r.ServerName, r.Runtime.SKHCount, r.Runtime.StarCount)
		}
	}
	return b.String()
}

// --- Điều Khiển Chạy & Lệnh Nóng ---

func (a *App) ToggleAutoRun(on bool) {
	a.mgr.SetAutoRunning(on)
}

func (a *App) StartSelected(ids []string) {
	a.mgr.ResetCircuitBreaker(ids)
	a.mgr.StartAccounts(ids)
}

func (a *App) StartHeadless(ids []string) {
	a.mgr.StartHeadless(ids)
}

func (a *App) StartVisible(ids []string) {
	a.mgr.StartVisible(ids)
}

func (a *App) StopSelected(ids []string) {
	a.mgr.StopAccounts(ids)
}

func (a *App) StopAllTabs() {
	a.mgr.SetAutoRunning(false)
	a.mgr.StopAllTabs()
}

func (a *App) RestartSelected(ids []string) {
	go func() {
		a.mgr.StopAccounts(ids)
		time.Sleep(600 * time.Millisecond)
		a.mgr.ResetCircuitBreaker(ids)
		a.mgr.StartAccounts(ids)
	}()
}

func (a *App) SendHotCommand(ids []string, cmd string, arg string) int {
	var data interface{}
	if cmd == "switch_zone" && strings.TrimSpace(arg) != "" {
		if z, err := strconv.Atoi(strings.TrimSpace(arg)); err == nil {
			data = map[string]int{"zone_id": z}
		}
	}
	return a.mgr.SendHotCommand(ids, cmd, data)
}

func (a *App) FocusWindow(accountID string) {
	a.mgr.FocusAccountWindow(accountID)
}

func (a *App) ArrangeWindows() {
	a.mgr.ArrangeAllWindows()
}

// --- Farm Profiles ---

func (a *App) SaveProfile(prof model.FarmProfile) (model.FarmProfile, error) {
	if strings.TrimSpace(prof.Name) == "" {
		return model.FarmProfile{}, errors.New("tên Profile không được trống")
	}
	if prof.ID == "" {
		prof.ID = newID("prof")
	}
	if prof.GameSpeed < 1.0 {
		prof.GameSpeed = 1.0
	}
	if prof.TargetFPS < 8 {
		prof.TargetFPS = 13
	}
	if err := a.store.SaveProfile(prof); err != nil {
		return model.FarmProfile{}, err
	}
	a.mgr.PushProfileUpdate(prof)
	a.scheduleUIEmit()
	return prof, nil
}

func (a *App) DeleteProfile(id string) error {
	if err := a.store.DeleteProfile(id); err != nil {
		return err
	}
	a.scheduleUIEmit()
	return nil
}

// --- Proxy Manager ---

func (a *App) ImportProxies(rawText string, defaultType string) (int, error) {
	existing := a.store.GetProxies()
	seen := make(map[string]bool, len(existing))
	for _, p := range existing {
		seen[fmt.Sprintf("%s:%d:%s", p.Host, p.Port, p.Username)] = true
	}

	added := 0
	for _, line := range strings.Split(rawText, "\n") {
		p, err := proxy.ParseLine(line, defaultType)
		if err != nil {
			continue
		}
		k := fmt.Sprintf("%s:%d:%s", p.Host, p.Port, p.Username)
		if seen[k] {
			continue
		}
		seen[k] = true
		p.ID = newID("prx")
		existing = append(existing, p)
		added++
	}
	if err := a.store.SaveProxies(existing); err != nil {
		return 0, err
	}
	a.scheduleUIEmit()
	return added, nil
}

func (a *App) CheckAllProxies() ([]model.ProxyItem, error) {
	list := a.store.GetProxies()
	if len(list) == 0 {
		return list, nil
	}
	targetHost := "dragon1.teamobi.com"
	targetPort := 14445
	if svs := a.store.GetServers(); len(svs) > 0 {
		targetHost = svs[0].Host
		targetPort = svs[0].Port
	}
	ctx, cancel := context.WithTimeout(context.Background(), 45*time.Second)
	defer cancel()
	checked := proxy.CheckBatch(ctx, list, targetHost, targetPort, 16)
	if err := a.store.SaveProxies(checked); err != nil {
		return nil, err
	}
	a.scheduleUIEmit()
	return checked, nil
}

func (a *App) DeleteDeadProxies() (int, error) {
	list := a.store.GetProxies()
	kept := make([]model.ProxyItem, 0, len(list))
	removed := 0
	for _, p := range list {
		if p.Status == "die" {
			removed++
			continue
		}
		kept = append(kept, p)
	}
	if err := a.store.SaveProxies(kept); err != nil {
		return 0, err
	}
	a.scheduleUIEmit()
	return removed, nil
}

func (a *App) DeleteProxies(ids []string) error {
	del := make(map[string]bool, len(ids))
	for _, id := range ids {
		del[id] = true
	}
	list := a.store.GetProxies()
	kept := make([]model.ProxyItem, 0, len(list))
	for _, p := range list {
		if !del[p.ID] {
			kept = append(kept, p)
		}
	}
	if err := a.store.SaveProxies(kept); err != nil {
		return err
	}
	a.scheduleUIEmit()
	return nil
}

// --- Servers, Settings & Utils ---

func (a *App) SaveServers(list []model.ServerConfig) error {
	for i := range list {
		if list[i].ID == "" {
			list[i].ID = newID("sv")
		}
		if list[i].Port <= 0 {
			list[i].Port = 14445
		}
	}
	if err := a.store.SaveServers(list); err != nil {
		return err
	}
	a.scheduleUIEmit()
	return nil
}

func (a *App) ResetDefaultServers() ([]model.ServerConfig, error) {
	def := store.DefaultOfficialServers()
	if err := a.store.SaveServers(def); err != nil {
		return nil, err
	}
	a.scheduleUIEmit()
	return def, nil
}

func (a *App) SaveSettings(cfg model.AppSettings) error {
	if err := a.store.SaveSettings(cfg); err != nil {
		return err
	}
	a.bumpAndEmitNow()
	return nil
}

func (a *App) TogglePrivacyMode(on bool) error {
	cfg := a.store.GetSettings()
	cfg.PrivacyMode = on
	if err := a.store.SaveSettings(cfg); err != nil {
		return err
	}
	a.mgr.SetPrivacyMode(on)
	return nil
}

func (a *App) TestTelegram() error {
	return a.mgr.TestTelegram()
}

func (a *App) ClearLogs() {
	a.mgr.ClearLogs()
}

func (a *App) OpenDataFolder() {
	dir, _ := filepath.Abs(a.store.DataDir())
	_ = exec.Command("explorer", dir).Start()
}

func (a *App) ShutdownApp() {
	go func() {
		time.Sleep(150 * time.Millisecond)
		launcher.RequestTrayQuit()
	}()
}

// --- HTTP + SSE Local Web UI Server (Zero WebView2, Win 2012 R2 -> Win 11) ---

type rpcRequest struct {
	Args []json.RawMessage `json:"args"`
}

type rpcResponse struct {
	Result interface{} `json:"result,omitempty"`
	Error  string      `json:"error,omitempty"`
}

func decodeArg[T any](args []json.RawMessage, idx int) (T, error) {
	var zero T
	if idx >= len(args) || len(args[idx]) == 0 || string(args[idx]) == "null" {
		return zero, nil
	}
	var out T
	if err := json.Unmarshal(args[idx], &out); err != nil {
		return zero, fmt.Errorf("tham số #%d không hợp lệ: %w", idx+1, err)
	}
	return out, nil
}

func writeJSONResp(w http.ResponseWriter, status int, resp rpcResponse) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Header().Set("Cache-Control", "no-store")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(resp)
}

func (a *App) HTTPHandler(frontendFS fs.FS, faviconICO []byte) http.Handler {
	mux := http.NewServeMux()
	fileServer := http.FileServer(http.FS(frontendFS))

	mux.HandleFunc("/api/events", func(w http.ResponseWriter, r *http.Request) {
		flusher, ok := w.(http.Flusher)
		if !ok {
			http.Error(w, "streaming unsupported", http.StatusInternalServerError)
			return
		}
		w.Header().Set("Content-Type", "text/event-stream")
		w.Header().Set("Cache-Control", "no-cache")
		w.Header().Set("Connection", "keep-alive")
		w.Header().Set("X-Accel-Buffering", "no")

		ch, unsub := a.SubscribeSSE()
		defer unsub()

		if b, err := json.Marshal(a.GetLiveSnapshot()); err == nil {
			_, _ = fmt.Fprintf(w, "event: state_update\ndata: %s\n\n", b)
			flusher.Flush()
		}

		keepalive := time.NewTicker(20 * time.Second)
		defer keepalive.Stop()

		for {
			select {
			case <-r.Context().Done():
				return
			case msg, alive := <-ch:
				if !alive {
					return
				}
				_, _ = fmt.Fprintf(w, "event: state_update\ndata: %s\n\n", msg)
				flusher.Flush()
			case <-keepalive.C:
				_, _ = fmt.Fprint(w, ": ping\n\n")
				flusher.Flush()
			}
		}
	})

	mux.HandleFunc("/api/", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			writeJSONResp(w, http.StatusMethodNotAllowed, rpcResponse{Error: "chỉ hỗ trợ POST"})
			return
		}
		method := strings.TrimPrefix(r.URL.Path, "/api/")
		r.Body = http.MaxBytesReader(w, r.Body, 10<<20)

		var req rpcRequest
		if r.ContentLength != 0 {
			if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
				writeJSONResp(w, http.StatusBadRequest, rpcResponse{Error: "JSON đầu vào không hợp lệ: " + err.Error()})
				return
			}
		}

		res, err := a.dispatchAPI(method, req.Args)
		if err != nil {
			writeJSONResp(w, http.StatusBadRequest, rpcResponse{Error: err.Error()})
			return
		}
		writeJSONResp(w, http.StatusOK, rpcResponse{Result: res})
	})

	mux.HandleFunc("/favicon.ico", func(w http.ResponseWriter, r *http.Request) {
		if len(faviconICO) > 0 {
			w.Header().Set("Content-Type", "image/x-icon")
			w.Header().Set("Cache-Control", "public, max-age=3600")
			_, _ = w.Write(faviconICO)
			return
		}
		http.NotFound(w, r)
	})

	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Cache-Control", "no-cache")
		fileServer.ServeHTTP(w, r)
	})

	return mux
}

func (a *App) dispatchAPI(method string, args []json.RawMessage) (interface{}, error) {
	switch method {
	case "GetBootstrap":
		return a.GetBootstrap(), nil
	case "GetLiveSnapshot":
		return a.GetLiveSnapshot(), nil
	case "CheckProxyString":
		raw, err := decodeArg[string](args, 0)
		if err != nil {
			return nil, err
		}
		svID, err := decodeArg[string](args, 1)
		if err != nil {
			return nil, err
		}
		return a.CheckProxyString(raw, svID), nil
	case "AddAccountsBulk":
		req, err := decodeArg[BulkAddRequest](args, 0)
		if err != nil {
			return nil, err
		}
		return a.AddAccountsBulk(req)
	case "UpdateAccount":
		acc, err := decodeArg[model.Account](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.UpdateAccount(acc)
	case "BulkAssignAccounts":
		req, err := decodeArg[BulkAssignRequest](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.BulkAssignAccounts(req)
	case "DeleteAccounts":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.DeleteAccounts(ids)
	case "ResetDoneAccounts":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.ResetDoneAccounts(ids)
	case "ExportAccountsText":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		onlySKH, err := decodeArg[bool](args, 1)
		if err != nil {
			return nil, err
		}
		incPass, err := decodeArg[bool](args, 2)
		if err != nil {
			return nil, err
		}
		return a.ExportAccountsText(ids, onlySKH, incPass), nil
	case "ToggleAutoRun":
		on, err := decodeArg[bool](args, 0)
		if err != nil {
			return nil, err
		}
		a.ToggleAutoRun(on)
		return true, nil
	case "StartSelected":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		a.StartSelected(ids)
		return true, nil
	case "StartHeadless":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		a.StartHeadless(ids)
		return true, nil
	case "StartVisible":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		a.StartVisible(ids)
		return true, nil
	case "StopSelected":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		a.StopSelected(ids)
		return true, nil
	case "StopAllTabs":
		a.StopAllTabs()
		return true, nil
	case "RestartSelected":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		a.RestartSelected(ids)
		return true, nil
	case "SendHotCommand":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		cmd, err := decodeArg[string](args, 1)
		if err != nil {
			return nil, err
		}
		arg, err := decodeArg[string](args, 2)
		if err != nil {
			return nil, err
		}
		return a.SendHotCommand(ids, cmd, arg), nil
	case "FocusWindow":
		id, err := decodeArg[string](args, 0)
		if err != nil {
			return nil, err
		}
		a.FocusWindow(id)
		return true, nil
	case "ArrangeWindows":
		a.ArrangeWindows()
		return true, nil
	case "SaveProfile":
		prof, err := decodeArg[model.FarmProfile](args, 0)
		if err != nil {
			return nil, err
		}
		return a.SaveProfile(prof)
	case "DeleteProfile":
		id, err := decodeArg[string](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.DeleteProfile(id)
	case "ImportProxies":
		raw, err := decodeArg[string](args, 0)
		if err != nil {
			return nil, err
		}
		defType, err := decodeArg[string](args, 1)
		if err != nil {
			return nil, err
		}
		return a.ImportProxies(raw, defType)
	case "CheckAllProxies":
		return a.CheckAllProxies()
	case "DeleteDeadProxies":
		return a.DeleteDeadProxies()
	case "DeleteProxies":
		ids, err := decodeArg[[]string](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.DeleteProxies(ids)
	case "SaveServers":
		list, err := decodeArg[[]model.ServerConfig](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.SaveServers(list)
	case "ResetDefaultServers":
		return a.ResetDefaultServers()
	case "SaveSettings":
		cfg, err := decodeArg[model.AppSettings](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.SaveSettings(cfg)
	case "TogglePrivacyMode":
		on, err := decodeArg[bool](args, 0)
		if err != nil {
			return nil, err
		}
		return nil, a.TogglePrivacyMode(on)
	case "TestTelegram":
		return nil, a.TestTelegram()
	case "ClearLogs":
		a.ClearLogs()
		return true, nil
	case "OpenDataFolder":
		a.OpenDataFolder()
		return true, nil
	case "ShutdownApp":
		a.ShutdownApp()
		return true, nil
	default:
		return nil, fmt.Errorf("không tìm thấy API: %s", method)
	}
}

func newID(prefix string) string {
	b := make([]byte, 5)
	_, _ = rand.Read(b)
	return fmt.Sprintf("%s_%s", prefix, hex.EncodeToString(b))
}
