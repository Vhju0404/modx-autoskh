package manager

import (
	"context"
	"encoding/json"
	"fmt"
	"html"
	"io"
	"math/rand"
	"net"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"hteam/modx-autoskh/internal/launcher"
	"hteam/modx-autoskh/internal/loot"
	"hteam/modx-autoskh/internal/model"
	"hteam/modx-autoskh/internal/notify"
	"hteam/modx-autoskh/internal/protocol"
	"hteam/modx-autoskh/internal/proxy"
	"hteam/modx-autoskh/internal/store"
)

const maxLogEntries = 500

// Manager là bộ não điều phối đa tab, vòng đời tiến trình, Proxy, Watchdog & Telegram.
type Manager struct {
	store    *store.Store
	hub      *protocol.Hub
	telegram *notify.TelegramClient

	mu                 sync.RWMutex
	runtimes           map[string]*model.AccountRuntime
	desiredRunning     map[string]bool        // Giữ 24/7: mọi tab đã bấm Chạy tự mở lại khi tắt/lag/crash cho tới khi bấm Dừng!
	restartTimes       map[string][]time.Time // Circuit breaker: đếm số lần restart trong 10 phút
	fatalErrors        map[string]string      // Chặn auto-reopen khi sai mật khẩu / bị khóa acc
	knownLootCounts    map[string]map[string]int
	ramHighSince       map[string]time.Time // Mốc bắt đầu vượt ngưỡng RAM liên tục (chốt chặn 3 phút chống tắt oan)
	ramLastTrimAt      map[string]time.Time // Mốc gần nhất đã xả RAM nhẹ cho tab
	headlessLastTrimAt map[string]time.Time // Mốc gần nhất trim nhẹ WorkingSet cho tab Headless (mỗi 90s)
	launchModeOverride map[string]string    // Ghi đè tạm chế độ mở tab: "headless" | "visible" (giữ xuyên Watchdog tới khi bấm Dừng)
	launching          map[string]bool      // Khóa chống mở trùng 2 tiến trình cho cùng 1 acc (double-click / chuyển chế độ / Watchdog)
	lastRamSampleAt    time.Time
	autoRunning        bool
	inMaintenance      bool
	outsideSchedule    bool
	maintReason        string
	dynamicMaintActive bool
	dynamicMaintUntil  time.Time
	lastMaintProbeAt   time.Time
	nextLaunchAt       time.Time
	logs               []model.LogEntry
	onStateChanged     func()

	ctx    context.Context
	cancel context.CancelFunc
}

func New(st *store.Store, onStateChanged func()) *Manager {
	ctx, cancel := context.WithCancel(context.Background())
	m := &Manager{
		store:          st,
		telegram:       notify.NewTelegramClient(),
		runtimes:       make(map[string]*model.AccountRuntime),
		desiredRunning: make(map[string]bool),
		restartTimes:       make(map[string][]time.Time),
		fatalErrors:        make(map[string]string),
		knownLootCounts:    make(map[string]map[string]int),
		ramHighSince:       make(map[string]time.Time),
		ramLastTrimAt:      make(map[string]time.Time),
		headlessLastTrimAt: make(map[string]time.Time),
		launchModeOverride: make(map[string]string),
		launching:          make(map[string]bool),
		logs:               make([]model.LogEntry, 0, maxLogEntries),
		onStateChanged: onStateChanged,
		ctx:            ctx,
		cancel:         cancel,
	}

	cfg := st.GetSettings()
	m.hub = protocol.NewHub(cfg.SocketPort, protocol.Handlers{
		OnHello:      m.handleHello,
		OnTelemetry:  m.handleTelemetry,
		OnEvent:      m.handleEvent,
		OnDisconnect: m.handleDisconnect,
	})
	return m
}

func (m *Manager) Start() error {
	if err := m.hub.Start(m.ctx); err != nil {
		m.addLog("ERROR", "SYSTEM", err.Error())
		return err
	}
	cfg := m.store.GetSettings()
	m.addLog("INFO", "SYSTEM", fmt.Sprintf("Socket NDJSON sẵn sàng tại 127.0.0.1:%d", cfg.SocketPort))
	go m.supervisorLoop()
	return nil
}

func (m *Manager) Stop() {
	m.SetAutoRunning(false)
	m.StopAllTabs()
	m.cancel()
	m.hub.Close()
}

func (m *Manager) SetAutoRunning(on bool) {
	m.mu.Lock()
	m.autoRunning = on
	m.nextLaunchAt = time.Time{}
	m.mu.Unlock()
	if on {
		m.addLog("INFO", "SYSTEM", "Bật chế độ tự động quản lý & xoay vòng dàn acc")
	} else {
		m.addLog("INFO", "SYSTEM", "Tạm dừng tự động mở thêm tab")
	}
	m.notifyUI()
}

func (m *Manager) IsAutoRunning() bool {
	m.mu.RLock()
	defer m.mu.RUnlock()
	return m.autoRunning
}

// StartAccounts mở 1 hoặc nhiều tài khoản được chọn (giãn nhịp jitter trong goroutine, theo mặc định Profile).
func (m *Manager) StartAccounts(ids []string) {
	m.mu.Lock()
	for _, id := range ids {
		delete(m.launchModeOverride, id)
	}
	m.mu.Unlock()
	go func() {
		cfg := m.store.GetSettings()
		for i, id := range ids {
			if err := m.StartOne(id); err != nil {
				m.addLog("ERROR", id, err.Error())
			}
			if i < len(ids)-1 {
				delay := randomDelayMs(cfg.LoginDelayMinMs, cfg.LoginDelayMaxMs)
				select {
				case <-time.After(delay):
				case <-m.ctx.Done():
					return
				}
			}
		}
		if cfg.AutoArrangeGrid {
			time.Sleep(800 * time.Millisecond)
			m.ArrangeAllWindows()
		}
	}()
}

// StartHeadless mở (hoặc chuyển từ có hình sang) 1 hoặc nhiều tài khoản ở chế độ TREO NGẦM KHÔNG CỬA SỔ (Headless VPS).
func (m *Manager) StartHeadless(ids []string) {
	go func() {
		cfg := m.store.GetSettings()
		for i, id := range ids {
			m.mu.Lock()
			if m.launching[id] {
				m.mu.Unlock()
				continue
			}
			m.launching[id] = true
			m.launchModeOverride[id] = "headless"
			rt, ok := m.runtimes[id]
			wasVisibleRunning := ok && rt.Running && !rt.Headless
			if wasVisibleRunning {
				// Tạm tắt desiredRunning trong lúc đổi chế độ để Watchdog không đua mở song song
				delete(m.desiredRunning, id)
			}
			m.mu.Unlock()

			func() {
				defer func() {
					m.mu.Lock()
					delete(m.launching, id)
					m.mu.Unlock()
				}()
				if wasVisibleRunning {
					m.stopOneInternal(id, "Đang chuyển sang treo ngầm Headless...", true)
					m.mu.Lock()
					delete(m.desiredRunning, id)
					m.mu.Unlock()
					// Chờ 2.5s để server NRO xả hẳn phiên đăng nhập cũ (tránh văng "đang đăng nhập ở máy khác")
					select {
					case <-time.After(2500 * time.Millisecond):
					case <-m.ctx.Done():
						return
					}
				}
				m.ResetCircuitBreaker([]string{id})
				if err := m.startOneInternal(id, true); err != nil {
					m.addLog("ERROR", id, err.Error())
				}
			}()
			if i < len(ids)-1 {
				delay := randomDelayMs(cfg.LoginDelayMinMs, cfg.LoginDelayMaxMs)
				select {
				case <-time.After(delay):
				case <-m.ctx.Done():
					return
				}
			}
		}
	}()
}

// StartVisible mở (hoặc khởi động lại từ Headless) 1 hoặc nhiều tài khoản ở chế độ CÓ CỬA SỔ để người dùng cầm tay.
func (m *Manager) StartVisible(ids []string) {
	go func() {
		cfg := m.store.GetSettings()
		for i, id := range ids {
			m.mu.Lock()
			if m.launching[id] {
				m.mu.Unlock()
				continue
			}
			m.launching[id] = true
			m.launchModeOverride[id] = "visible"
			rt, ok := m.runtimes[id]
			wasHeadlessRunning := ok && rt.Running && rt.Headless
			if wasHeadlessRunning {
				// Tạm tắt desiredRunning trong lúc đổi chế độ để Watchdog không đua mở song song
				delete(m.desiredRunning, id)
			}
			m.mu.Unlock()

			func() {
				defer func() {
					m.mu.Lock()
					delete(m.launching, id)
					m.mu.Unlock()
				}()
				if wasHeadlessRunning {
					m.stopOneInternal(id, "Đang chuyển sang chế độ có hình...", true)
					m.mu.Lock()
					delete(m.desiredRunning, id)
					m.mu.Unlock()
					// Chờ 2.5s để server NRO xả hẳn phiên đăng nhập cũ (tránh văng "đang đăng nhập ở máy khác")
					select {
					case <-time.After(2500 * time.Millisecond):
					case <-m.ctx.Done():
						return
					}
				}
				m.ResetCircuitBreaker([]string{id})
				if err := m.startOneInternal(id, true); err != nil {
					m.addLog("ERROR", id, err.Error())
				}
			}()
			if i < len(ids)-1 {
				delay := randomDelayMs(cfg.LoginDelayMinMs, cfg.LoginDelayMaxMs)
				select {
				case <-time.After(delay):
				case <-m.ctx.Done():
					return
				}
			}
		}
		if cfg.AutoArrangeGrid {
			time.Sleep(800 * time.Millisecond)
			m.ArrangeAllWindows()
		}
	}()
}

// StartOne khởi chạy 1 tab tài khoản cụ thể.
func (m *Manager) StartOne(accountID string) error {
	return m.startOneInternal(accountID, false)
}

func (m *Manager) startOneInternal(accountID string, alreadyLockedLaunch bool) error {
	if !store.RuntimeIntegrityOK() {
		return fmt.Errorf("phát hiện bản cài đặt đã bị chỉnh sửa — dừng khởi chạy")
	}
	acc, ok := m.store.GetAccountByID(accountID)
	if !ok {
		return fmt.Errorf("không tìm thấy tài khoản %s", accountID)
	}
	m.mu.Lock()
	if !alreadyLockedLaunch {
		if m.launching[accountID] {
			m.mu.Unlock()
			return nil
		}
		m.launching[accountID] = true
		defer func() {
			m.mu.Lock()
			delete(m.launching, accountID)
			m.mu.Unlock()
		}()
	}
	rt := m.getOrCreateRuntimeLocked(accountID)
	if rt.Running && launcher.IsProcessAlive(rt.PID) {
		m.mu.Unlock()
		return nil
	}
	modeOverride := m.launchModeOverride[accountID]
	m.mu.Unlock()

	cfg := m.store.GetSettings()
	sv, _ := m.store.GetServerByID(acc.ServerID)
	prof, _ := m.store.GetProfileByID(acc.ProfileID)
	headless := prof.HeadlessMode
	if modeOverride == "headless" {
		headless = true
	} else if modeOverride == "visible" {
		headless = false
	}

	proxyStr, proxyType, proxyLabel := m.resolveProxyForAccount(acc, cfg)

	res, err := launcher.StartGame(launcher.LaunchSpec{
		GameExePath:  cfg.GameExePath,
		RuntimeDir:   m.store.RuntimeDir(),
		Account:      acc,
		Server:       sv,
		Profile:      prof,
		ProxyStr:     proxyStr,
		ProxyType:    proxyType,
		SocketPort:   cfg.SocketPort,
		WindowWidth:  cfg.WindowWidth,
		WindowHeight: cfg.WindowHeight,
		Headless:     headless,
		PrivacyMode:  cfg.PrivacyMode,
		DisplayAlias: m.accountDisplayAlias(acc.ID),
		TitlePrefix:  store.WindowTitlePrefix(),
	})
	if err != nil {
		m.mu.Lock()
		rt := m.getOrCreateRuntimeLocked(accountID)
		rt.LastError = err.Error()
		rt.FSMState = "ERROR"
		rt.StatusText = err.Error()
		m.mu.Unlock()
		m.notifyUI()
		return err
	}

	now := model.NowUnixMs()
	m.mu.Lock()
	delete(m.fatalErrors, accountID)
	m.desiredRunning[accountID] = true
	rt = m.getOrCreateRuntimeLocked(accountID)
	rt.Running = true
	rt.Connected = false
	rt.Headless = headless
	rt.PID = res.PID
	rt.HWND = res.HWND
	rt.ActiveProxy = proxyLabel
	rt.StartedAt = now
	rt.LastHeartbeat = now
	// Xóa sạch trạng thái phiên cũ để QLTK không bao giờ hiển thị nhầm Map/Khu/Túi cũ khi tab mới đang đăng nhập
	rt.CharName = ""
	rt.MapID = 0
	rt.MapName = ""
	rt.ZoneID = 0
	rt.ZonePlayers = 0
	rt.ZoneMax = 0
	rt.CX = 0
	rt.CY = 0
	rt.InPrivateMap = false
	rt.TicketRemain = ""
	rt.LastExpSec = -1
	rt.HitsPerMin = 0
	rt.TotalHits = 0
	rt.TargetMob = ""
	rt.ActiveSkill = ""
	rt.HP = 0
	rt.HPMax = 0
	rt.MP = 0
	rt.MPMax = 0
	rt.BagUsed = 0
	rt.BagMax = 0
	rt.BoxUsed = 0
	rt.BoxMax = 0
	rt.RamMB = 0
	rt.FSMState = "LOGIN"
	if headless {
		rt.StatusText = "👻 Đang khởi động ngầm & đăng nhập..."
	} else {
		rt.StatusText = "Đang khởi động & đăng nhập..."
	}
	rt.LastError = ""
	if headless {
		appendAccHistoryLocked(rt, "[SYSTEM] Mở tab 👻 Treo ngầm Headless ("+sv.Name+" · "+prof.Name+")")
	} else {
		appendAccHistoryLocked(rt, "[SYSTEM] Mở tab 👁 Có cửa sổ ("+sv.Name+" · "+prof.Name+")")
	}
	delete(m.headlessLastTrimAt, accountID)
	m.mu.Unlock()

	modeTag := ""
	if headless {
		modeTag = " · 👻 Headless"
	}
	m.addLog("INFO", acc.Username, fmt.Sprintf("Mở tab PID=%d (%s · %s%s · Proxy: %s)", res.PID, sv.Name, prof.Name, modeTag, proxyLabel))
	m.notifyUI()
	return nil
}

// StopAccounts tắt 1 danh sách tài khoản theo yêu cầu người dùng (hủy tự mở lại 24/7, chạy song song siêu nhanh).
func (m *Manager) StopAccounts(ids []string) {
	if len(ids) == 0 {
		return
	}
	if len(ids) == 1 {
		m.stopOneInternal(ids[0], "Người dùng dừng", false)
		return
	}
	sem := make(chan struct{}, 32)
	var wg sync.WaitGroup
	for _, id := range ids {
		accID := id
		wg.Add(1)
		go func() {
			defer wg.Done()
			sem <- struct{}{}
			defer func() { <-sem }()
			m.stopOneNoNotify(accID, "Người dùng dừng", false)
		}()
	}
	wg.Wait()
	m.notifyUI()
}

// RestartAccounts khởi động lại 1 hoặc nhiều tài khoản nhưng GIỮ NGUYÊN chế độ hiện tại (👻 Headless hoặc 👁 Có cửa sổ).
func (m *Manager) RestartAccounts(ids []string) {
	if len(ids) == 0 {
		return
	}
	go func() {
		cfg := m.store.GetSettings()
		anyVisible := false
		for i, id := range ids {
			m.mu.Lock()
			if m.launching[id] {
				m.mu.Unlock()
				continue
			}
			m.launching[id] = true
			savedMode := m.launchModeOverride[id]
			if savedMode == "" {
				if rt, ok := m.runtimes[id]; ok && rt.Running {
					if rt.Headless {
						savedMode = "headless"
					} else {
						savedMode = "visible"
					}
				}
			}
			wasRunning := false
			if rt, ok := m.runtimes[id]; ok && (rt.Running || rt.PID > 0) {
				wasRunning = true
			}
			delete(m.desiredRunning, id)
			m.mu.Unlock()

			func() {
				defer func() {
					m.mu.Lock()
					delete(m.launching, id)
					m.mu.Unlock()
				}()
				if wasRunning {
					m.stopOneNoNotify(id, "Đang khởi động lại tab...", false)
					select {
					case <-time.After(1500 * time.Millisecond):
					case <-m.ctx.Done():
						return
					}
				}
				m.mu.Lock()
				if savedMode != "" {
					m.launchModeOverride[id] = savedMode
				}
				m.mu.Unlock()
				m.ResetCircuitBreaker([]string{id})
				if err := m.startOneInternal(id, true); err != nil {
					m.addLog("ERROR", id, err.Error())
				} else if savedMode != "headless" {
					anyVisible = true
				}
			}()

			if i < len(ids)-1 {
				delay := randomDelayMs(cfg.LoginDelayMinMs, cfg.LoginDelayMaxMs)
				select {
				case <-time.After(delay):
				case <-m.ctx.Done():
					return
				}
			}
		}
		if anyVisible && cfg.AutoArrangeGrid {
			time.Sleep(800 * time.Millisecond)
			m.ArrangeAllWindows()
		}
	}()
}

// PurgeAccounts dừng tab, ngắt socket và xóa sạch mọi state RAM + file runtime của acc bị xóa.
func (m *Manager) PurgeAccounts(ids []string) {
	for _, id := range ids {
		m.stopOneInternal(id, "Đã xóa tài khoản", false)
		m.hub.Disconnect(id)
		_ = os.Remove(filepath.Join(m.store.RuntimeDir(), fmt.Sprintf("profile_%s.json", id)))
	}
	m.mu.Lock()
	for _, id := range ids {
		delete(m.runtimes, id)
		delete(m.desiredRunning, id)
		delete(m.restartTimes, id)
		delete(m.fatalErrors, id)
		delete(m.knownLootCounts, id)
		delete(m.ramHighSince, id)
		delete(m.ramLastTrimAt, id)
		delete(m.headlessLastTrimAt, id)
		delete(m.launchModeOverride, id)
		delete(m.launching, id)
	}
	m.mu.Unlock()
	m.notifyUI()
}

// SyncAccountUpdated đồng bộ sạch state runtime / profile / socket khi người dùng sửa tài khoản.
func (m *Manager) SyncAccountUpdated(oldAcc, newAcc model.Account) {
	identityChanged := !strings.EqualFold(oldAcc.Username, newAcc.Username) || oldAcc.ServerID != newAcc.ServerID
	if identityChanged {
		// Đổi sang tài khoản hoặc server khác -> dừng tab cũ & xóa sạch runtime/loot cũ để không lẫn data
		m.stopOneInternal(newAcc.ID, "Đã cập nhật tài khoản mới", false)
		m.hub.Disconnect(newAcc.ID)
		m.mu.Lock()
		delete(m.runtimes, newAcc.ID)
		delete(m.desiredRunning, newAcc.ID)
		delete(m.restartTimes, newAcc.ID)
		delete(m.fatalErrors, newAcc.ID)
		delete(m.knownLootCounts, newAcc.ID)
		m.mu.Unlock()
		_ = m.store.DeleteLootHistoryByAccounts([]string{newAcc.ID})
	} else {
		// Giữ nguyên acc -> xóa lỗi cũ (nếu vừa đổi pass/proxy) & đẩy profile mới xuống tab đang chạy
		m.ResetCircuitBreaker([]string{newAcc.ID})
	}

	if prof, ok := m.store.GetProfileByID(newAcc.ProfileID); ok {
		pFile := filepath.Join(m.store.RuntimeDir(), fmt.Sprintf("profile_%s.json", newAcc.ID))
		if b, err := json.Marshal(prof); err == nil {
			_ = os.WriteFile(pFile, b, 0o600)
		}
		if !identityChanged && m.hub.IsConnected(newAcc.ID) {
			_ = m.hub.SendTo(newAcc.ID, protocol.ServerCmd{
				Cmd:  "profile_update",
				Data: prof,
			})
		}
	}
	m.notifyUI()
}

func (m *Manager) StopOne(accountID string, reason string) {
	m.stopOneInternal(accountID, reason, false)
}

func (m *Manager) stopOneInternal(accountID string, reason string, keepDesired bool) {
	m.stopOneNoNotify(accountID, reason, keepDesired)
	m.notifyUI()
}

func (m *Manager) stopOneNoNotify(accountID string, reason string, keepDesired bool) {
	m.mu.Lock()
	if !keepDesired {
		delete(m.desiredRunning, accountID)
		delete(m.launchModeOverride, accountID)
	}
	delete(m.headlessLastTrimAt, accountID)
	rt, ok := m.runtimes[accountID]
	var pid int
	if ok {
		pid = rt.PID
		rt.Running = false
		rt.Connected = false
		rt.Headless = false
		rt.PID = 0
		rt.HWND = 0
		if rt.FSMState != "DONE" && rt.FSMState != "ERROR" {
			rt.FSMState = "IDLE"
			rt.StatusText = reason
		}
	}
	m.mu.Unlock()

	if m.hub.IsConnected(accountID) {
		_ = m.hub.SendTo(accountID, protocol.ServerCmd{Cmd: "exit"})
		// Chờ tối đa 700ms để client kịp đóng TCP sạch với server Teamobi trước khi ép KillProcess
		for waited := 0; waited < 700 && pid > 0 && launcher.IsProcessAlive(pid); waited += 50 {
			time.Sleep(50 * time.Millisecond)
		}
	}
	if pid > 0 && launcher.IsProcessAlive(pid) {
		_ = launcher.KillProcess(pid)
	}
	if acc, ok := m.store.GetAccountByID(accountID); ok && pid > 0 {
		m.addLog("INFO", acc.Username, fmt.Sprintf("Đã đóng tab (%s)", reason))
	}
}

func (m *Manager) StopAllTabs() {
	m.stopAllTabsInternal("Đóng toàn bộ tab", false)
}

func (m *Manager) stopAllTabsInternal(reason string, keepDesired bool) {
	m.mu.RLock()
	ids := make([]string, 0, len(m.runtimes))
	for id, rt := range m.runtimes {
		if rt.Running || rt.PID > 0 {
			ids = append(ids, id)
		}
	}
	m.mu.RUnlock()
	if !keepDesired {
		m.mu.Lock()
		m.desiredRunning = make(map[string]bool)
		m.launchModeOverride = make(map[string]string)
		m.mu.Unlock()
	}
	if len(ids) == 0 {
		return
	}
	// Đóng song song toàn bộ tab (30 tab hoàn tất trong <= 700ms thay vì nối đuôi 21 giây!)
	sem := make(chan struct{}, 32)
	var wg sync.WaitGroup
	for _, id := range ids {
		accID := id
		wg.Add(1)
		go func() {
			defer wg.Done()
			sem <- struct{}{}
			defer func() { <-sem }()
			m.stopOneNoNotify(accID, reason, keepDesired)
		}()
	}
	wg.Wait()
	m.notifyUI()
}

// SendHotCommand gửi lệnh nóng trực tiếp qua Socket không cần tắt game.
func (m *Manager) SendHotCommand(ids []string, cmd string, data interface{}) int {
	sent := 0
	for _, id := range ids {
		if err := m.hub.SendTo(id, protocol.ServerCmd{Cmd: cmd, Data: data}); err == nil {
			sent++
			if acc, ok := m.store.GetAccountByID(id); ok {
				m.addLog("INFO", acc.Username, fmt.Sprintf("Gửi lệnh nóng: %s", cmd))
			}
		}
	}
	return sent
}

// PushProfileUpdate đẩy cấu hình FarmProfile mới xuống mọi tab đang dùng Profile đó ngay lập tức.
func (m *Manager) PushProfileUpdate(prof model.FarmProfile) {
	accs := m.store.GetAccounts()
	for _, acc := range accs {
		if acc.ProfileID == prof.ID {
			// Cập nhật luôn file runtime để lần restart sau ăn ngay
			pFile := filepath.Join(m.store.RuntimeDir(), fmt.Sprintf("profile_%s.json", acc.ID))
			if b, err := json.Marshal(prof); err == nil {
				_ = os.WriteFile(pFile, b, 0o600)
			}
			if m.hub.IsConnected(acc.ID) {
				_ = m.hub.SendTo(acc.ID, protocol.ServerCmd{
					Cmd:  "profile_update",
					Data: prof,
				})
			}
		}
	}
	m.addLog("INFO", "SYSTEM", fmt.Sprintf("Đã đồng bộ live Profile '%s' xuống các tab đang chạy", prof.Name))
}

func (m *Manager) FocusAccountWindow(accountID string) {
	m.mu.RLock()
	rt, ok := m.runtimes[accountID]
	var hwnd uintptr
	var pid int
	if ok {
		hwnd = rt.HWND
		pid = rt.PID
	}
	m.mu.RUnlock()

	if hwnd == 0 && pid > 0 {
		hwnd = launcher.FindMainWindowByPID(pid)
	}
	if hwnd != 0 {
		launcher.FocusWindow(hwnd)
	}
}

func (m *Manager) ArrangeAllWindows() {
	cfg := m.store.GetSettings()
	type winEntry struct {
		id   string
		pid  int
		hwnd uintptr
	}
	m.mu.RLock()
	entries := make([]winEntry, 0, len(m.runtimes))
	for id, rt := range m.runtimes {
		if rt.Running && !rt.Headless && rt.PID > 0 {
			entries = append(entries, winEntry{id: id, pid: rt.PID, hwnd: rt.HWND})
		}
	}
	m.mu.RUnlock()

	hwnds := make([]uintptr, 0, len(entries))
	resolved := make(map[string]uintptr)
	for _, e := range entries {
		h := e.hwnd
		if h == 0 {
			h = launcher.FindMainWindowByPID(e.pid)
			if h != 0 {
				resolved[e.id] = h
			}
		}
		if h != 0 {
			hwnds = append(hwnds, h)
		}
	}
	if len(resolved) > 0 {
		m.mu.Lock()
		for id, h := range resolved {
			if rt, ok := m.runtimes[id]; ok {
				rt.HWND = h
			}
		}
		m.mu.Unlock()
	}
	launcher.ArrangeGrid(hwnds, cfg.WindowWidth, cfg.WindowHeight)
}

// GetAccountRows trả danh sách đầy đủ Account + Runtime + Server/Profile/Proxy label cho bảng QLTK.
func (m *Manager) GetAccountRows() []model.AccountRow {
	accs := m.store.GetAccounts()
	servers := m.store.GetServers()
	profiles := m.store.GetProfiles()
	proxies := m.store.GetProxies()
	cfg := m.store.GetSettings()

	svMap := make(map[string]string, len(servers))
	for _, s := range servers {
		svMap[s.ID] = s.Name
	}
	profMap := make(map[string]string, len(profiles))
	for _, p := range profiles {
		profMap[p.ID] = p.Name
	}
	prxMap := make(map[string]model.ProxyItem, len(proxies))
	for _, prx := range proxies {
		prxMap[prx.ID] = prx
	}

	m.mu.RLock()
	defer m.mu.RUnlock()

	rows := make([]model.AccountRow, 0, len(accs))
	for _, a := range accs {
		var rt model.AccountRuntime
		if existing, ok := m.runtimes[a.ID]; ok {
			rt = *existing
			if len(existing.RecentEvents) > 0 {
				rt.RecentEvents = append([]string(nil), existing.RecentEvents...)
			}
		} else {
			rt = model.AccountRuntime{
				AccountID: a.ID,
				FSMState:  "IDLE",
			}
			if a.Done {
				rt.FSMState = "DONE"
				rt.StatusText = a.DoneReason
			}
		}

		// Ẩn mật khẩu khỏi payload trả định kỳ về bảng
		safeAcc := a
		safeAcc.Password = ""
		safeAcc.PasswordEnc = ""

		prxLabel := "Home"
		prxStatus := "live"
		if strings.TrimSpace(a.CustomProxy) != "" {
			if cp, err := proxy.ParseLine(a.CustomProxy, "http"); err == nil {
				prxLabel = fmt.Sprintf("%s:%d (riêng)", cp.Host, cp.Port)
			} else {
				prxLabel = "Proxy riêng (lỗi)"
				prxStatus = "die"
			}
		} else if a.ProxyID == "home" {
			prxLabel = "Home (IP gốc)"
		} else if a.ProxyID == "" || a.ProxyID == "auto" {
			if rt.ActiveProxy != "" {
				prxLabel = rt.ActiveProxy
			} else if len(proxies) > 0 && cfg.MaxAccPerProxy > 0 {
				prxLabel = "Tự động"
			} else {
				prxLabel = "Home"
			}
		} else if p, ok := prxMap[a.ProxyID]; ok {
			prxLabel = fmt.Sprintf("%s:%d", p.Host, p.Port)
			prxStatus = p.Status
		}

		rows = append(rows, model.AccountRow{
			Account:     safeAcc,
			Runtime:     rt,
			ServerName:  svMap[a.ServerID],
			ProfileName: profMap[a.ProfileID],
			ProxyLabel:  prxLabel,
			ProxyStatus: prxStatus,
		})
	}
	return rows
}

func (m *Manager) GetSummary() model.DashboardSummary {
	rows := m.GetAccountRows()
	report := loot.BuildReport(rows)
	proxies := m.store.GetProxies()
	cfg := m.store.GetSettings()

	livePrx := 0
	for _, p := range proxies {
		if p.Status == "live" {
			livePrx++
		}
	}

	running := 0
	done := 0
	warn := 0
	for _, r := range rows {
		if r.Runtime.Running {
			running++
		}
		if r.Account.Done {
			done++
		}
		if r.Runtime.FSMState == "ERROR" || r.Runtime.LastError != "" || (r.ProxyStatus == "die") {
			warn++
		}
	}

	m.mu.RLock()
	autoRun := m.autoRunning
	inMaint := m.inMaintenance
	outSched := m.outsideSchedule
	mReason := m.maintReason
	m.mu.RUnlock()

	return model.DashboardSummary{
		AutoRunning:     autoRun,
		InMaintenance:   inMaint,
		OutsideSchedule: outSched,
		MaintReason:     mReason,
		UIPort:          cfg.UIPort,
		SyncLocked:      !store.RuntimeIntegrityOK(),
		PrivacyMode:     cfg.PrivacyMode,
		TotalAccounts:   len(rows),
		RunningTabs:     running,
		MaxTabs:         cfg.MaxTabs,
		DoneAccounts:    done,
		WarningAccounts: warn,
		TotalSKHItems:   report.TotalSKH,
		TotalStarItems:  report.TotalStars,
		CompleteSets:    report.TotalCompleteSets,
		LiveProxies:     livePrx,
		TotalProxies:    len(proxies),
		ServerTimestamp: model.NowUnixMs(),
	}
}

func (m *Manager) GetLootReport() loot.LootReport {
	return loot.BuildReport(m.GetAccountRows())
}

func (m *Manager) GetLogs() []model.LogEntry {
	m.mu.RLock()
	defer m.mu.RUnlock()
	out := make([]model.LogEntry, len(m.logs))
	copy(out, m.logs)
	return out
}

func (m *Manager) ClearLogs() {
	m.mu.Lock()
	m.logs = m.logs[:0]
	m.mu.Unlock()
	m.notifyUI()
}

func (m *Manager) TestTelegram() error {
	cfg := m.store.GetSettings()
	msg := fmt.Sprintf("<b>[%s]</b>\nKết nối Telegram thành công lúc %s!", store.NotifyHeaderText(), time.Now().Format("15:04:05 02/01/2006"))
	return m.telegram.Send(cfg.TelegramBotToken, cfg.TelegramChatID, "", 0, msg)
}

// --- Socket Handlers ---

func (m *Manager) handleHello(accountID, username string, pid int, sig string) {
	acc, exists := m.store.GetAccountByID(accountID)
	if !exists || (username != "" && !strings.EqualFold(acc.Username, username)) {
		_ = m.hub.SendTo(accountID, protocol.ServerCmd{Cmd: "exit"})
		m.hub.Disconnect(accountID)
		return
	}

	// Xác thực chữ ký đồng bộ chéo Go EXE <-> C# DLL (chống gắn DLL bị chỉnh sửa / rebrand)
	if !store.RuntimeIntegrityOK() || !strings.EqualFold(strings.TrimSpace(sig), store.ComputeSessionSig(accountID)) {
		reason := "Phát hiện file client bị chỉnh sửa chữ ký — dừng auto"
		m.mu.Lock()
		delete(m.desiredRunning, accountID)
		m.fatalErrors[accountID] = reason
		rt := m.getOrCreateRuntimeLocked(accountID)
		rt.FSMState = "ERROR"
		rt.LastError = reason
		rt.StatusText = reason
		m.mu.Unlock()
		_ = m.hub.SendTo(accountID, protocol.ServerCmd{Cmd: "pause"})
		_ = m.hub.SendTo(accountID, protocol.ServerCmd{Cmd: "exit"})
		m.hub.Disconnect(accountID)
		m.addLog("ERROR", acc.Username, reason)
		m.notifyUI()
		return
	}

	m.mu.Lock()
	rt := m.getOrCreateRuntimeLocked(accountID)
	// Chống tiến trình trùng (orphan/zombie) giành socket của tab chính đang sống
	if rt.Running && rt.PID > 0 && pid > 0 && rt.PID != pid && launcher.IsProcessAlive(rt.PID) {
		keepPID := rt.PID
		m.mu.Unlock()
		m.hub.Disconnect(accountID)
		go launcher.KillProcess(pid)
		m.addLog("WARN", acc.Username, fmt.Sprintf("Đã dọn tiến trình trùng PID=%d (giữ tab chính PID=%d)", pid, keepPID))
		return
	}
	if m.launching[accountID] && rt.PID == 0 && pid > 0 {
		m.mu.Unlock()
		m.hub.Disconnect(accountID)
		go launcher.KillProcess(pid)
		return
	}
	wasConnected := rt.Connected
	rt.Connected = true
	rt.Running = true
	if pid > 0 {
		rt.PID = pid
		if !rt.Headless && rt.HWND == 0 {
			rt.HWND = launcher.FindMainWindowByPID(pid)
		}
	}
	rt.LastHeartbeat = model.NowUnixMs()
	if !wasConnected {
		appendAccHistoryLocked(rt, fmt.Sprintf("[SOCKET] Client PID=%d đã kết nối", pid))
	}
	m.mu.Unlock()

	// Đẩy mã xác nhận phiên + Profile & trạng thái Che TK mới nhất xuống client
	_ = m.hub.SendTo(accountID, protocol.ServerCmd{
		Cmd: "session_sync",
		Data: map[string]string{
			"ack": store.ComputeServerAck(accountID),
			"tag": store.HudHeaderLabel(),
		},
	})
	cfg := m.store.GetSettings()
	if prof, ok2 := m.store.GetProfileByID(acc.ProfileID); ok2 {
		_ = m.hub.SendTo(accountID, protocol.ServerCmd{Cmd: "profile_update", Data: prof})
	}
	_ = m.hub.SendTo(accountID, protocol.ServerCmd{
		Cmd:  "set_privacy",
		Data: map[string]bool{"enabled": cfg.PrivacyMode},
	})
	sv, _ := m.store.GetServerByID(acc.ServerID)
	m.mu.RLock()
	var hwnd uintptr
	if rt, ok3 := m.runtimes[accountID]; ok3 {
		hwnd = rt.HWND
	}
	m.mu.RUnlock()
	if hwnd != 0 {
		launcher.SetWindowTitle(hwnd, m.windowTitleForAccount(acc, sv, cfg.PrivacyMode))
	}
	m.addLog("INFO", acc.Username, "Client đã kết nối Socket NDJSON")
	m.notifyUI()
}

func (m *Manager) accountDisplayAlias(accountID string) string {
	accs := m.store.GetAccounts()
	for i, a := range accs {
		if a.ID == accountID {
			return fmt.Sprintf("Acc #%d", i+1)
		}
	}
	return "Acc ***"
}

func (m *Manager) windowTitleForAccount(acc model.Account, sv model.ServerConfig, privacy bool) string {
	name := acc.Username
	if privacy {
		name = m.accountDisplayAlias(acc.ID)
	}
	return fmt.Sprintf("%s | %s | %s", store.WindowTitlePrefix(), name, sv.Name)
}

// SetPrivacyMode bật/tắt chế độ Che TK ngay lập tức trên tiêu đề mọi cửa sổ game và HUD trong game.
func (m *Manager) SetPrivacyMode(on bool) {
	accs := m.store.GetAccounts()
	accMap := make(map[string]model.Account, len(accs))
	aliasMap := make(map[string]string, len(accs))
	for i, a := range accs {
		accMap[a.ID] = a
		aliasMap[a.ID] = fmt.Sprintf("Acc #%d", i+1)
	}

	type targetWin struct {
		id   string
		hwnd uintptr
		pid  int
	}
	m.mu.RLock()
	targets := make([]targetWin, 0, len(m.runtimes))
	for id, rt := range m.runtimes {
		if (rt.Running || rt.PID > 0 || m.hub.IsConnected(id)) {
			h := rt.HWND
			p := rt.PID
			if rt.Headless {
				h = 0
				p = 0
			}
			targets = append(targets, targetWin{id: id, hwnd: h, pid: p})
		}
	}
	m.mu.RUnlock()

	for _, t := range targets {
		hwnd := t.hwnd
		if hwnd == 0 && t.pid > 0 {
			hwnd = launcher.FindMainWindowByPID(t.pid)
			if hwnd != 0 {
				m.mu.Lock()
				if rt, ok := m.runtimes[t.id]; ok {
					rt.HWND = hwnd
				}
				m.mu.Unlock()
			}
		}
		if acc, ok := accMap[t.id]; ok && hwnd != 0 {
			sv, _ := m.store.GetServerByID(acc.ServerID)
			name := acc.Username
			if on {
				name = aliasMap[t.id]
			}
			launcher.SetWindowTitle(hwnd, fmt.Sprintf("%s | %s | %s", store.WindowTitlePrefix(), name, sv.Name))
		}
		if m.hub.IsConnected(t.id) {
			_ = m.hub.SendTo(t.id, protocol.ServerCmd{
				Cmd:  "set_privacy",
				Data: map[string]bool{"enabled": on},
			})
		}
	}
	m.notifyUI()
}

func (m *Manager) handleTelemetry(accountID string, t protocol.TelemetryPayload) {
	if _, exists := m.store.GetAccountByID(accountID); !exists {
		_ = m.hub.SendTo(accountID, protocol.ServerCmd{Cmd: "exit"})
		m.hub.Disconnect(accountID)
		return
	}
	normLoot := loot.NormalizeItems(t.Loot)
	skhCount := 0
	starCount := 0
	for _, it := range normLoot {
		if it.IsSKH {
			skhCount++
		} else if it.Stars > 0 {
			starCount++
		}
	}

	planet := "TĐ"
	switch t.Gender {
	case 1:
		planet = "NM"
	case 2:
		planet = "XD"
	}

	var newSKH []model.LootItem
	m.mu.Lock()
	rt := m.getOrCreateRuntimeLocked(accountID)
	prevState := rt.FSMState
	prevStatus := rt.StatusText
	prevMap := rt.MapName
	prevPriv := rt.InPrivateMap
	rt.Connected = true
	rt.Running = true
	rt.CharName = t.CharName
	rt.Gender = t.Gender
	rt.Planet = planet
	rt.MapID = t.MapID
	rt.MapName = t.MapName
	rt.ZoneID = t.ZoneID
	rt.ZonePlayers = t.ZonePlayers
	rt.ZoneMax = t.ZoneMax
	rt.CX = t.CX
	rt.CY = t.CY
	rt.InPrivateMap = t.InPrivateMap
	rt.TicketRemain = t.TicketRemain
	rt.LastExpSec = t.LastExpSec
	rt.HitsPerMin = t.HitsPerMin
	rt.TotalHits = t.TotalHits
	rt.TargetMob = t.TargetMob
	rt.ActiveSkill = t.ActiveSkill
	rt.HP = t.HP
	rt.HPMax = t.HPMax
	rt.MP = t.MP
	rt.MPMax = t.MPMax
	rt.Power = t.Power
	rt.Gold = t.Gold
	rt.Gem = t.Gem
	rt.GemLock = t.GemLock
	rt.TDLTMinutes = t.TDLTMinutes
	rt.HasTDLTActive = t.HasTDLTActive
	rt.HasLuckyGrass = t.HasLuckyGrass
	rt.IsNewMember = t.IsNewMember
	rt.MamRemain = t.MamRemain
	rt.LuckPercent = t.LuckPercent
	rt.LuckDetail = t.LuckDetail
	rt.TitleName = t.TitleName
	rt.TitleRemain = t.TitleRemain
	rt.TitleExpiryMs = t.TitleExpiryMs
	rt.BagUsed = t.BagUsed
	rt.BagMax = t.BagMax
	rt.BoxUsed = t.BoxUsed
	rt.BoxMax = t.BoxMax
	rt.FSMState = t.FSMState
	rt.StatusText = t.StatusText
	rt.Paused = t.Paused
	rt.BlackScreen = t.BlackScreen
	if t.ActiveProxy != "" {
		rt.ActiveProxy = t.ActiveProxy
	}
	rt.Loot = normLoot
	rt.SKHCount = skhCount
	rt.StarCount = starCount
	rt.LastHeartbeat = model.NowUnixMs()
	if prevState != t.FSMState || prevStatus != t.StatusText || prevMap != t.MapName || prevPriv != t.InPrivateMap {
		locTag := t.MapName
		if t.InPrivateMap {
			locTag += " (Map Riêng)"
		} else if t.MapName != "" {
			locTag += fmt.Sprintf(" K%d", t.ZoneID)
		}
		appendAccHistoryLocked(rt, fmt.Sprintf("[%s] %s · %s", t.FSMState, t.StatusText, locTag))
	}

	// Phát hiện món SKH mới bằng mốc đếm cao nhất (gộp chung hành trang + rương nhà):
	// - Cất đồ từ túi vào rương (kể cả khi server gửi tách 2 gói BAG/BOX) không bao giờ báo nổ giả lần 2.
	// - Nhặt thêm món SKH thứ 2 trùng hệt tên/sao vẫn tăng tổng và báo nổ chuẩn 100%.
	maxSeen, initialized := m.knownLootCounts[accountID]
	if !initialized {
		m.knownLootCounts[accountID] = countSKHBySig(normLoot)
	} else {
		newSKH = collectNewlyDroppedSKH(maxSeen, normLoot)
	}
	m.mu.Unlock()

	if t.TitleExpiryMs > 0 {
		_ = m.store.UpdateAccountTitleInfo(accountID, t.TitleName, t.TitleExpiryMs)
	}
	if len(newSKH) > 0 {
		m.onNewSKHDropped(accountID, newSKH)
	}
	m.notifyUI()
}

func (m *Manager) handleEvent(accountID string, e protocol.EventPayload) {
	acc, hasAcc := m.store.GetAccountByID(accountID)
	if !hasAcc {
		_ = m.hub.SendTo(accountID, protocol.ServerCmd{Cmd: "exit"})
		m.hub.Disconnect(accountID)
		return
	}
	uname := acc.Username
	cfg := m.store.GetSettings()

	switch e.Kind {
	case "box_full":
		_ = m.store.MarkAccountDone(accountID, true, "Full rương nhà")
		m.mu.Lock()
		delete(m.desiredRunning, accountID)
		rt := m.getOrCreateRuntimeLocked(accountID)
		rt.FSMState = "DONE"
		rt.StatusText = "Full rương nhà"
		appendAccHistoryLocked(rt, "[DONE] Rương nhà đã đầy — đánh dấu hoàn thành")
		m.mu.Unlock()
		m.addLog("LOOT", uname, "Rương nhà đã đầy — đánh dấu hoàn thành!")
		if cfg.TelegramEnabled && cfg.NotifyOnBoxFull {
			go m.telegram.Send(cfg.TelegramBotToken, cfg.TelegramChatID, "boxfull_"+accountID, 10*time.Minute,
				fmt.Sprintf("<b>[%s]</b>\n📦 <b>Full Rương Nhà!</b>\nAcc: <code>%s</code>\n%s",
					html.EscapeString(store.NotifyHeaderText()), html.EscapeString(uname), html.EscapeString(e.Message)))
		}
	case "low_gem":
		m.mu.Lock()
		appendAccHistoryLocked(m.getOrCreateRuntimeLocked(accountID), "[WARN] "+e.Message)
		m.mu.Unlock()
		m.addLog("WARN", uname, e.Message)
		if cfg.TelegramEnabled && cfg.NotifyOnLowGem {
			go m.telegram.Send(cfg.TelegramBotToken, cfg.TelegramChatID, "lowgem_"+accountID, 30*time.Minute,
				fmt.Sprintf("<b>[%s]</b>\n💎 <b>Sắp hết Ngọc!</b>\nAcc: <code>%s</code>\n%s",
					html.EscapeString(store.NotifyHeaderText()), html.EscapeString(uname), html.EscapeString(e.Message)))
		}
	case "skh_drop":
		if e.Item != nil {
			norm := loot.NormalizeItems([]model.LootItem{*e.Item})
			m.mu.Lock()
			if seen, ok := m.knownLootCounts[accountID]; ok {
				for _, it := range norm {
					if it.IsSKH {
						seen[lootItemSig(it)]++
					}
				}
			}
			m.mu.Unlock()
			m.onNewSKHDropped(accountID, norm)
		}
	case "stuck":
		m.mu.Lock()
		appendAccHistoryLocked(m.getOrCreateRuntimeLocked(accountID), "[STUCK] "+e.Message)
		m.mu.Unlock()
		m.addLog("WARN", uname, "Chống kẹt: "+e.Message)
	case "login_status":
		m.mu.Lock()
		rt := m.getOrCreateRuntimeLocked(accountID)
		if rt.FSMState != "DONE" && rt.FSMState != "ERROR" {
			rt.FSMState = "LOGIN"
			rt.StatusText = e.Message
		}
		rt.LastHeartbeat = model.NowUnixMs()
		if e.Message != "" {
			appendAccHistoryLocked(rt, "[LOGIN] "+e.Message)
		}
		m.mu.Unlock()
		if e.Message != "" {
			m.addLog("WARN", uname, e.Message)
		}
	case "login_heartbeat":
		m.mu.Lock()
		rt := m.getOrCreateRuntimeLocked(accountID)
		rt.LastHeartbeat = model.NowUnixMs()
		if e.Message != "" && rt.FSMState == "LOGIN" {
			rt.StatusText = e.Message
		}
		m.mu.Unlock()
	case "title_acquired":
		m.mu.Lock()
		appendAccHistoryLocked(m.getOrCreateRuntimeLocked(accountID), "[TITLE] "+e.Message)
		m.mu.Unlock()
		m.addLog("LOOT", uname, "🏅 "+e.Message)
	case "maint_notice":
		if e.Message != "" {
			m.mu.Lock()
			appendAccHistoryLocked(m.getOrCreateRuntimeLocked(accountID), "[MAINT] "+e.Message)
			m.mu.Unlock()
			m.addLog("WARN", uname, "📢 Server thông báo bảo trì: "+e.Message+" → tiếp tục farm tới mốc ≤ 1 phút!")
		}
	case "server_maintenance":
		// Server báo còn <= 1 phút bảo trì HOẶC đang bảo trì -> đóng hết tab ngay (không cộng Circuit Breaker), tự dò bắt tay NRO mỗi 60s!
		rawMsg := strings.TrimSpace(e.Message)
		isImminent := strings.HasPrefix(rawMsg, "imminent|")
		dispMsg := strings.TrimPrefix(rawMsg, "imminent|")
		waitBeforeProbe := 60 * time.Second
		reasonText := "Server đang bảo trì — tự dò bắt tay NRO mỗi 60s"
		if isImminent {
			// Server vừa báo còn <= 1 phút nữa mới tắt -> chờ 2 phút cho server sập hẳn rồi mới bắt đầu dò mỗi 60s
			waitBeforeProbe = 2 * time.Minute
			reasonText = "Server bảo trì (≤ 1p) — đã đóng tab, bắt đầu dò mỗi 60s sau 2p"
		}
		m.mu.Lock()
		appendAccHistoryLocked(m.getOrCreateRuntimeLocked(accountID), "[MAINT] "+dispMsg)
		alreadyDyn := m.dynamicMaintActive
		m.dynamicMaintActive = true
		m.inMaintenance = true
		m.dynamicMaintUntil = time.Now().Add(waitBeforeProbe)
		m.lastMaintProbeAt = time.Time{}
		m.maintReason = reasonText
		m.mu.Unlock()
		if !alreadyDyn {
			if isImminent {
				m.addLog("WARN", uname, "⏰ Server báo bảo trì còn ≤ 1 phút ("+dispMsg+") → tự đóng toàn bộ tab nghỉ, bắt đầu dò bắt tay NRO mỗi 60s sau 2 phút!")
			} else {
				m.addLog("WARN", uname, "🌙 Server đang bảo trì ("+dispMsg+") → tự đóng toàn bộ tab cho VPS nghỉ, dò bắt tay NRO mỗi 60s!")
			}
			go m.stopAllTabsInternal("Server bảo trì (tự dò mở lại mỗi 60s)", true)
		}
	case "fatal_error":
		reason := strings.TrimSpace(e.Message)
		if reason == "" {
			reason = "Lỗi đăng nhập nghiêm trọng (sai mật khẩu / khóa acc)"
		}
		m.mu.Lock()
		delete(m.desiredRunning, accountID)
		m.fatalErrors[accountID] = reason
		rt := m.getOrCreateRuntimeLocked(accountID)
		rt.FSMState = "ERROR"
		rt.LastError = reason
		rt.StatusText = reason
		appendAccHistoryLocked(rt, "[ERROR] "+reason)
		m.mu.Unlock()
		m.addLog("ERROR", uname, "Dừng auto-relog: "+reason)
		if cfg.TelegramEnabled {
			go m.telegram.Send(cfg.TelegramBotToken, cfg.TelegramChatID, "fatal_"+accountID, 30*time.Minute,
				fmt.Sprintf("<b>[%s]</b>\n⛔ <b>Lỗi Đăng Nhập (Đã dừng tự relog)</b>\nAcc: <code>%s</code>\nLý do: %s",
					html.EscapeString(store.NotifyHeaderText()), html.EscapeString(uname), html.EscapeString(reason)))
		}
		go func() {
			time.Sleep(1500 * time.Millisecond)
			m.StopOne(accountID, reason)
		}()
	default:
		if e.Message != "" {
			m.addLog("INFO", uname, e.Message)
		}
	}
	m.notifyUI()
}

func (m *Manager) handleDisconnect(accountID string) {
	_, exists := m.store.GetAccountByID(accountID)
	m.mu.Lock()
	if !exists {
		delete(m.runtimes, accountID)
		delete(m.desiredRunning, accountID)
		delete(m.restartTimes, accountID)
		delete(m.fatalErrors, accountID)
		delete(m.knownLootCounts, accountID)
	} else if rt, ok := m.runtimes[accountID]; ok {
		rt.Connected = false
		if !launcher.IsProcessAlive(rt.PID) {
			rt.Running = false
			rt.PID = 0
			rt.HWND = 0
			if rt.FSMState != "DONE" {
				rt.FSMState = "IDLE"
				rt.StatusText = "Đã ngắt kết nối"
			}
		}
	}
	m.mu.Unlock()
	m.notifyUI()
}

func (m *Manager) onNewSKHDropped(accountID string, items []model.LootItem) {
	acc, _ := m.store.GetAccountByID(accountID)
	sv, _ := m.store.GetServerByID(acc.ServerID)
	cfg := m.store.GetSettings()

	for _, it := range items {
		_ = m.store.AppendLootHistory(model.LootHistoryEntry{
			ID:         fmt.Sprintf("%d_%s", model.NowUnixMs(), accountID),
			AccountID:  accountID,
			Username:   acc.Username,
			ServerName: sv.Name,
			Item:       it,
			FoundAt:    model.NowUnixMs(),
		})
		starTxt := ""
		if it.Stars > 0 {
			starTxt = fmt.Sprintf(" (%d sao)", it.Stars)
		}
		m.addLog("LOOT", acc.Username, fmt.Sprintf("NỔ SKH: %s — Set %s%s", it.Name, it.SetName, starTxt))
		if cfg.TelegramEnabled && cfg.NotifyOnSKH {
			msg := fmt.Sprintf("<b>[%s]</b>\n🔥 <b>NỔ ĐỒ SET KÍCH HOẠT!</b>\nAcc: <code>%s</code> (%s)\nMón: <b>%s</b>\nBộ: <b>Set %s</b>%s",
				html.EscapeString(store.NotifyHeaderText()),
				html.EscapeString(acc.Username),
				html.EscapeString(sv.Name),
				html.EscapeString(it.Name),
				html.EscapeString(it.SetName),
				html.EscapeString(starTxt))
			go m.telegram.Send(cfg.TelegramBotToken, cfg.TelegramChatID, "", 0, msg)
		}
	}

	if cfg.TelegramEnabled && cfg.NotifyOnFullSet {
		rep := m.GetLootReport()
		for _, gs := range rep.GlobalSets {
			if gs.CompleteSets > 0 {
				go m.telegram.Send(cfg.TelegramBotToken, cfg.TelegramChatID,
					fmt.Sprintf("fullset_%s_%d", gs.SetName, gs.CompleteSets), 1*time.Hour,
					fmt.Sprintf("<b>[%s]</b>\n🏆 <b>ĐỦ 5/5 SET %s (%s)!</b>\nTổng bộ hoàn chỉnh hiện có: <b>%d bộ</b>",
						html.EscapeString(store.NotifyHeaderText()),
						html.EscapeString(gs.SetName),
						html.EscapeString(gs.Planet),
						gs.CompleteSets))
			}
		}
	}
}

// --- Supervisor Loop (Watchdog + Auto Rotation + Maintenance Window) ---

func (m *Manager) supervisorLoop() {
	ticker := time.NewTicker(2 * time.Second)
	defer ticker.Stop()

	for {
		select {
		case <-m.ctx.Done():
			return
		case <-ticker.C:
			m.tickSupervisor()
		}
	}
}

func (m *Manager) tickSupervisor() {
	if !store.RuntimeIntegrityOK() {
		m.stopAllTabsInternal("Phát hiện bản cài đặt bị chỉnh sửa", false)
		return
	}
	cfg := m.store.GetSettings()
	launcher.CloseCrashDialogs()
	nowTime := time.Now()

	// 0. Kiểm tra Lịch Khung Giờ Chạy Tự Chọn (nhiều ca trong ngày)
	outsideSched := cfg.ScheduleEnabled && len(cfg.ScheduleWindows) > 0 && !IsInAnyTimeWindow(nowTime, cfg.ScheduleWindows)
	m.mu.Lock()
	prevOutSched := m.outsideSchedule
	m.outsideSchedule = outsideSched
	if outsideSched {
		m.maintReason = "Nghỉ ngoài khung giờ ca chạy"
	} else if prevOutSched && !m.inMaintenance {
		m.maintReason = ""
	}
	m.mu.Unlock()

	if outsideSched {
		if !prevOutSched {
			m.addLog("INFO", "SYSTEM", "Hết ca chạy theo khung giờ → tự đóng toàn bộ tab nghỉ & xả RAM (chờ ca kế tiếp)!")
			m.stopAllTabsInternal("Nghỉ ngoài khung giờ chơi", true)
			m.notifyUI()
		}
		return
	} else if prevOutSched {
		m.addLog("INFO", "SYSTEM", "⏰ Đến khung giờ ca chạy → tự động mở lại dàn acc!")
		m.notifyUI()
	}

	// 1. Bảo trì thông minh (khung định kỳ + bảo trì đột xuất + tự dò cổng TCP mỗi 60s trước khi mở lại)
	inSchedMaint := cfg.MaintenanceEnabled && isInTimeWindow(nowTime, cfg.MaintenanceStart, cfg.MaintenanceEnd)
	m.mu.Lock()
	prevMaint := m.inMaintenance
	dynActive := m.dynamicMaintActive
	dynUntil := m.dynamicMaintUntil
	lastProbe := m.lastMaintProbeAt
	m.mu.Unlock()

	if inSchedMaint {
		m.mu.Lock()
		m.inMaintenance = true
		m.maintReason = fmt.Sprintf("Nghỉ bảo trì định kỳ (%s–%s)", cfg.MaintenanceStart, cfg.MaintenanceEnd)
		m.mu.Unlock()
		if !prevMaint {
			m.addLog("WARN", "SYSTEM", fmt.Sprintf("Vào khung giờ nghỉ bảo trì (%s–%s) → tạm đóng tab xả RAM (hết giờ tự dò cổng mở lại)!", cfg.MaintenanceStart, cfg.MaintenanceEnd))
			m.stopAllTabsInternal("Nghỉ khung giờ bảo trì", true)
			m.notifyUI()
		}
		return
	}

	// Vừa hết khung bảo trì định kỳ hoặc đang trong trạng thái bảo trì đột xuất -> dò cổng TCP server NRO trước khi bung tab!
	if dynActive || prevMaint {
		if dynActive && nowTime.Before(dynUntil) {
			return
		}
		if prevMaint && !dynActive {
			// Vừa kết thúc khung giờ bảo trì định kỳ -> dò cổng ngay lập tức
			lastProbe = time.Time{}
		}
		if nowTime.Sub(lastProbe) < 60*time.Second {
			return
		}
		m.mu.Lock()
		m.lastMaintProbeAt = nowTime
		m.mu.Unlock()

		if !m.probeAnyServerPort() {
			m.mu.Lock()
			m.inMaintenance = true
			m.dynamicMaintActive = true
			m.maintReason = "Server NRO chưa mở (đã dò bắt tay) — thử lại sau 60s"
			m.mu.Unlock()
			m.addLog("WARN", "SYSTEM", "Dò bắt tay server NRO chưa mở → đợi tiếp 60s...")
			m.notifyUI()
			return
		}

		m.mu.Lock()
		m.inMaintenance = false
		m.dynamicMaintActive = false
		m.maintReason = ""
		m.mu.Unlock()
		m.addLog("INFO", "SYSTEM", "✅ Server NRO đã mở lại & bắt tay thành công → tự động bung lại dàn acc!")
		m.notifyUI()
	}

	// 2. Watchdog kiểm tra PID sống/chết, treo cửa sổ, timeout kết nối Socket & giám sát RAM 3 chốt chặn
	now := model.NowUnixMs()
	startupTimeoutMs := int64(cfg.StartupTimeoutSec) * 1000
	if startupTimeoutMs < 20000 {
		startupTimeoutMs = 45000
	}

	m.mu.Lock()
	shouldSampleRam := nowTime.Sub(m.lastRamSampleAt) >= 10*time.Second
	if shouldSampleRam {
		m.lastRamSampleAt = nowTime
	}
	m.mu.Unlock()

	var deadOrHung []string
	var exitedUnexpectedly []string
	var ramTrimTargets []struct {
		id  string
		pid int
		mb  int
	}
	var ramSoftRestartTargets []struct {
		id string
		mb int
	}
	var headlessTrimPIDs []int
	validAccs := make(map[string]bool)
	for _, a := range m.store.GetAccounts() {
		validAccs[a.ID] = true
	}
	m.mu.Lock()
	for id, rt := range m.runtimes {
		if !validAccs[id] {
			delete(m.runtimes, id)
			delete(m.desiredRunning, id)
			delete(m.restartTimes, id)
			delete(m.fatalErrors, id)
			delete(m.knownLootCounts, id)
			delete(m.ramHighSince, id)
			delete(m.ramLastTrimAt, id)
			delete(m.headlessLastTrimAt, id)
			delete(m.launchModeOverride, id)
			delete(m.launching, id)
			continue
		}
		if !rt.Running {
			rt.RamMB = 0
			delete(m.ramHighSince, id)
			continue
		}
		if rt.PID > 0 && !launcher.IsProcessAlive(rt.PID) {
			rt.Running = false
			rt.Connected = false
			rt.PID = 0
			rt.HWND = 0
			rt.RamMB = 0
			delete(m.ramHighSince, id)
			if rt.FSMState != "DONE" && rt.FSMState != "ERROR" {
				rt.FSMState = "IDLE"
				if m.desiredRunning[id] {
					rt.StatusText = "Tab vừa thoát — Watchdog chuẩn bị mở lại..."
					exitedUnexpectedly = append(exitedUnexpectedly, id)
				} else {
					rt.StatusText = "Tiến trình đã thoát"
				}
			}
			continue
		}
		// Kẹt lúc mới mở quá lâu không nối được socket
		if !rt.Connected && now-rt.StartedAt > startupTimeoutMs {
			deadOrHung = append(deadOrHung, id)
			continue
		}
		// Treo cửa sổ Win32 (bỏ qua với tab Headless không có cửa sổ)
		if !rt.Headless && rt.HWND != 0 && launcher.IsWindowHung(rt.HWND) && now-rt.LastHeartbeat > 25000 {
			deadOrHung = append(deadOrHung, id)
			continue
		}
		// Mất nhịp tim quá 75s
		if rt.Connected && now-rt.LastHeartbeat > 75000 {
			deadOrHung = append(deadOrHung, id)
			continue
		}

		// Trim nhẹ WorkingSet mỗi 90s cho tab Headless (tránh spam pagefile SSD, giữ RAM thực ~10-25MB)
		if rt.Headless && rt.Connected && rt.PID > 0 && now-rt.StartedAt > 15000 {
			if lastT := m.headlessLastTrimAt[id]; lastT.IsZero() || nowTime.Sub(lastT) >= 90*time.Second {
				m.headlessLastTrimAt[id] = nowTime
				headlessTrimPIDs = append(headlessTrimPIDs, rt.PID)
			}
		}

		// Giám sát RAM thật (psapi.dll) + 3 chốt chặn chống tắt oan
		if shouldSampleRam && rt.PID > 0 {
			mb := launcher.GetProcessMemoryMB(rt.PID)
			if mb > 0 {
				rt.RamMB = mb
			}
			if cfg.MaxTabRamMB > 0 && rt.Connected {
				if rt.RamMB >= cfg.MaxTabRamMB {
					since, tracking := m.ramHighSince[id]
					if !tracking {
						m.ramHighSince[id] = nowTime
						since = nowTime
					}
					lastTrim := m.ramLastTrimAt[id]
					if nowTime.Sub(since) >= 3*time.Minute && !lastTrim.IsZero() && nowTime.Sub(lastTrim) >= 60*time.Second {
						// Chốt 2 (vượt ngưỡng liên tục >= 3p) + Chốt 3 (CHỈ restart khi đang FARM yên ổn, cấm cắt STORE/SELL/BUFF/TRAVEL!)
						if rt.FSMState == "FARM" {
							delete(m.ramHighSince, id)
							ramSoftRestartTargets = append(ramSoftRestartTargets, struct {
								id string
								mb int
							}{id: id, mb: rt.RamMB})
						}
					} else if nowTime.Sub(lastTrim) >= 90*time.Second {
						// Chốt 1: Thử xả RAM nhẹ trước (Unity UnloadUnusedAssets + Win32 EmptyWorkingSet)
						m.ramLastTrimAt[id] = nowTime
						ramTrimTargets = append(ramTrimTargets, struct {
							id  string
							pid int
							mb  int
						}{id: id, pid: rt.PID, mb: rt.RamMB})
					}
				} else if rt.RamMB < cfg.MaxTabRamMB-20 {
					delete(m.ramHighSince, id)
				}
			}
		}
	}
	m.mu.Unlock()

	for _, pid := range headlessTrimPIDs {
		launcher.TrimProcessWorkingSet(pid)
	}

	for _, t := range ramTrimTargets {
		_ = m.hub.SendTo(t.id, protocol.ServerCmd{Cmd: "trim_memory"})
		launcher.TrimProcessWorkingSet(t.pid)
		if acc, ok := m.store.GetAccountByID(t.id); ok {
			m.addLog("INFO", acc.Username, fmt.Sprintf("🧹 RAM đạt %dMB (>= %dMB) → đã xả rác nhẹ (chờ 60–90s đo lại)", t.mb, cfg.MaxTabRamMB))
		}
	}

	for _, t := range ramSoftRestartTargets {
		if acc, ok := m.store.GetAccountByID(t.id); ok {
			m.addLog("WARN", acc.Username, fmt.Sprintf("♻️ RAM %dMB vượt ngưỡng liên tục > 3 phút lúc FARM → restart mềm xả sạch 100%% RAM!", t.mb))
		}
		// Không gọi recordRestart -> không bao giờ làm bật Circuit Breaker!
		m.stopOneInternal(t.id, fmt.Sprintf("Restart mềm xả RAM (%dMB)", t.mb), true)
	}

	for _, id := range exitedUnexpectedly {
		m.recordRestart(id)
		if acc, ok := m.store.GetAccountByID(id); ok {
			m.addLog("WARN", acc.Username, "Phát hiện tab tự thoát / crash → Watchdog 24/7 sẽ mở lại!")
		}
		m.notifyUI()
	}

	for _, id := range deadOrHung {
		m.recordRestart(id)
		m.stopOneInternal(id, "Watchdog diệt tab treo / mất nhịp tim (sẽ mở lại)", true)
	}

	// 2.5. Watchdog 24/7 Auto-Reopen: mọi tab người dùng đã bấm Chạy mà bị tắt/lag/crash sẽ TỰ ĐỘNG MỞ LẠI!
	m.mu.RLock()
	canLaunchNow := !time.Now().Before(m.nextLaunchAt)
	desiredIDs := make([]string, 0, len(m.desiredRunning))
	for id, want := range m.desiredRunning {
		if want {
			desiredIDs = append(desiredIDs, id)
		}
	}
	m.mu.RUnlock()

	if canLaunchNow && len(desiredIDs) > 0 {
		for _, id := range desiredIDs {
			acc, exists := m.store.GetAccountByID(id)
			if !exists || acc.Done {
				m.mu.Lock()
				delete(m.desiredRunning, id)
				m.mu.Unlock()
				continue
			}
			m.mu.RLock()
			rt, ok := m.runtimes[id]
			isRunning := (ok && rt.Running) || m.launching[id]
			fatalReason, isFatal := m.fatalErrors[id]
			m.mu.RUnlock()
			if isRunning {
				continue
			}
			if isFatal {
				m.mu.Lock()
				delete(m.desiredRunning, id)
				r := m.getOrCreateRuntimeLocked(id)
				r.FSMState = "ERROR"
				r.LastError = fatalReason
				r.StatusText = fatalReason
				m.mu.Unlock()
				continue
			}
			if m.isCircuitOpen(id, cfg.MaxRestarts10Min) {
				m.mu.Lock()
				delete(m.desiredRunning, id)
				r := m.getOrCreateRuntimeLocked(id)
				r.FSMState = "ERROR"
				r.LastError = fmt.Sprintf("Tạm ngưng (tự mở lại quá %d lần/10p)", cfg.MaxRestarts10Min)
				r.StatusText = r.LastError
				m.mu.Unlock()
				m.addLog("ERROR", acc.Username, "Circuit breaker kích hoạt — dừng mở lại để tránh spam")
				m.notifyUI()
				continue
			}

			m.addLog("INFO", acc.Username, "🔄 Watchdog 24/7 tự mở lại tab...")
			if err := m.StartOne(id); err == nil {
				delay := randomDelayMs(cfg.LoginDelayMinMs, cfg.LoginDelayMaxMs)
				m.mu.Lock()
				m.nextLaunchAt = time.Now().Add(delay)
				m.mu.Unlock()
				if cfg.AutoArrangeGrid {
					go func() {
						time.Sleep(1200 * time.Millisecond)
						m.ArrangeAllWindows()
					}()
				}
			}
			return
		}
	}

	// 3. Nếu đang bật AutoRunning -> tự xoay vòng giữ đủ MaxTabs (có giãn nhịp jitter & Circuit Breaker)
	if !m.IsAutoRunning() {
		return
	}
	m.mu.RLock()
	if time.Now().Before(m.nextLaunchAt) {
		m.mu.RUnlock()
		return
	}
	runningCount := 0
	for _, rt := range m.runtimes {
		if rt.Running {
			runningCount++
		}
	}
	m.mu.RUnlock()

	if runningCount >= cfg.MaxTabs {
		return
	}

	accs := m.store.GetAccounts()
	for _, acc := range accs {
		if acc.Done {
			continue
		}
		m.mu.RLock()
		rt, exists := m.runtimes[acc.ID]
		isRunning := (exists && rt.Running) || m.launching[acc.ID]
		m.mu.RUnlock()
		if isRunning {
			continue
		}
		// Nếu acc dính lỗi đăng nhập chí mạng (sai mật khẩu / bị khóa) -> không tự mở lại
		m.mu.RLock()
		fatalReason, isFatal := m.fatalErrors[acc.ID]
		m.mu.RUnlock()
		if isFatal {
			m.mu.Lock()
			r := m.getOrCreateRuntimeLocked(acc.ID)
			r.FSMState = "ERROR"
			r.LastError = fatalReason
			r.StatusText = fatalReason
			m.mu.Unlock()
			continue
		}
		// Circuit breaker: quá MaxRestarts10Min lần trong 10 phút -> bỏ qua + báo lỗi đỏ
		if m.isCircuitOpen(acc.ID, cfg.MaxRestarts10Min) {
			m.mu.Lock()
			r := m.getOrCreateRuntimeLocked(acc.ID)
			r.FSMState = "ERROR"
			r.LastError = "Tạm ngưng (restart liên tiếp quá ngưỡng)"
			r.StatusText = r.LastError
			m.mu.Unlock()
			continue
		}

		// Mở đúng 1 tab mỗi nhịp để đảm bảo giãn cách jitter
		if err := m.StartOne(acc.ID); err == nil {
			delay := randomDelayMs(cfg.LoginDelayMinMs, cfg.LoginDelayMaxMs)
			m.mu.Lock()
			m.nextLaunchAt = time.Now().Add(delay)
			m.mu.Unlock()
			if cfg.AutoArrangeGrid {
				go func() {
					time.Sleep(1200 * time.Millisecond)
					m.ArrangeAllWindows()
				}()
			}
		}
		break
	}
}

func (m *Manager) resolveProxyItemForAccount(acc model.Account, cfg model.AppSettings) (model.ProxyItem, bool, string) {
	if strings.TrimSpace(acc.CustomProxy) != "" {
		if cp, err := proxy.ParseLine(acc.CustomProxy, "http"); err == nil {
			return cp, true, fmt.Sprintf("%s:%d (riêng)", cp.Host, cp.Port)
		}
	}
	if acc.ProxyID == "home" {
		return model.ProxyItem{}, false, "Home"
	}
	proxies := m.store.GetProxies()
	if len(proxies) == 0 {
		return model.ProxyItem{}, false, "Home"
	}

	// Nếu chỉ định proxy cố định
	if acc.ProxyID != "" && acc.ProxyID != "auto" {
		for _, p := range proxies {
			if p.ID == acc.ProxyID {
				if p.Status != "die" || !cfg.AutoRotateProxy {
					return p, true, fmt.Sprintf("%s:%d", p.Host, p.Port)
				}
				break
			}
		}
	}

	// Phân bổ tự động theo MaxAccPerProxy (ưu tiên proxy live hoặc unknown, bỏ qua proxy die)
	maxPer := cfg.MaxAccPerProxy
	if maxPer <= 0 {
		return model.ProxyItem{}, false, "Home"
	}
	candidates := make([]model.ProxyItem, 0, len(proxies))
	for _, p := range proxies {
		if p.Status != "die" {
			candidates = append(candidates, p)
		}
	}
	if len(candidates) == 0 {
		return model.ProxyItem{}, false, "Home (Hết proxy live)"
	}

	// Tìm vị trí acc trong danh sách để phân đều ổn định
	accs := m.store.GetAccounts()
	accIdx := 0
	for i, a := range accs {
		if a.ID == acc.ID {
			accIdx = i
			break
		}
	}
	chosen := candidates[(accIdx/maxPer)%len(candidates)]
	return chosen, true, fmt.Sprintf("%s:%d", chosen.Host, chosen.Port)
}

func (m *Manager) resolveProxyForAccount(acc model.Account, cfg model.AppSettings) (string, string, string) {
	item, ok, label := m.resolveProxyItemForAccount(acc, cfg)
	if !ok {
		return "home", "http", label
	}
	return proxy.FormatForClient(item), item.Type, label
}

func (m *Manager) recordRestart(accountID string) {
	m.mu.Lock()
	defer m.mu.Unlock()
	now := time.Now()
	cutoff := now.Add(-10 * time.Minute)
	var recent []time.Time
	for _, ts := range m.restartTimes[accountID] {
		if ts.After(cutoff) {
			recent = append(recent, ts)
		}
	}
	recent = append(recent, now)
	m.restartTimes[accountID] = recent
	rt := m.getOrCreateRuntimeLocked(accountID)
	rt.RestartCount = len(recent)
}

func (m *Manager) isCircuitOpen(accountID string, limit int) bool {
	if limit <= 0 {
		limit = 5
	}
	m.mu.RLock()
	defer m.mu.RUnlock()
	cutoff := time.Now().Add(-10 * time.Minute)
	cnt := 0
	for _, ts := range m.restartTimes[accountID] {
		if ts.After(cutoff) {
			cnt++
		}
	}
	return cnt >= limit
}

// ResetCircuitBreaker xóa bộ đếm lỗi để cho phép acc chạy lại.
func (m *Manager) ResetCircuitBreaker(ids []string) {
	m.mu.Lock()
	for _, id := range ids {
		delete(m.restartTimes, id)
		delete(m.fatalErrors, id)
		if rt, ok := m.runtimes[id]; ok {
			rt.RestartCount = 0
			rt.LastError = ""
			if rt.FSMState == "ERROR" {
				rt.FSMState = "IDLE"
				rt.StatusText = ""
			}
		}
	}
	m.mu.Unlock()
	m.notifyUI()
}

func (m *Manager) getOrCreateRuntimeLocked(accountID string) *model.AccountRuntime {
	rt, ok := m.runtimes[accountID]
	if !ok {
		rt = &model.AccountRuntime{AccountID: accountID, FSMState: "IDLE", LastExpSec: -1}
		m.runtimes[accountID] = rt
	}
	return rt
}

func appendAccHistoryLocked(rt *model.AccountRuntime, msg string) {
	if rt == nil || strings.TrimSpace(msg) == "" {
		return
	}
	if len(rt.RecentEvents) > 0 && strings.HasSuffix(rt.RecentEvents[0], " "+msg) {
		return
	}
	line := time.Now().Format("15:04:05") + " " + msg
	rt.RecentEvents = append([]string{line}, rt.RecentEvents...)
	if len(rt.RecentEvents) > 25 {
		rt.RecentEvents = rt.RecentEvents[:25]
	}
}

func (m *Manager) addLog(level, account, msg string) {
	entry := model.LogEntry{
		Time:      time.Now().Format("15:04:05"),
		Level:     level,
		Account:   account,
		Message:   msg,
		Timestamp: model.NowUnixMs(),
	}
	m.mu.Lock()
	m.logs = append([]model.LogEntry{entry}, m.logs...)
	if len(m.logs) > maxLogEntries {
		m.logs = m.logs[:maxLogEntries]
	}
	m.mu.Unlock()
}

func (m *Manager) notifyUI() {
	if m.onStateChanged != nil {
		m.onStateChanged()
	}
}

func lootItemSig(it model.LootItem) string {
	return fmt.Sprintf("%d|%s|%s|%d", it.TemplateID, it.SetName, it.Slot, it.Stars)
}

func countSKHBySig(items []model.LootItem) map[string]int {
	counts := make(map[string]int)
	for _, it := range items {
		if it.IsSKH {
			counts[lootItemSig(it)]++
		}
	}
	return counts
}

func collectNewlyDroppedSKH(maxSeen map[string]int, items []model.LootItem) []model.LootItem {
	curCounts := make(map[string]int)
	var newlyDropped []model.LootItem
	for _, it := range items {
		if !it.IsSKH {
			continue
		}
		sig := lootItemSig(it)
		curCounts[sig]++
		if curCounts[sig] > maxSeen[sig] {
			maxSeen[sig] = curCounts[sig]
			newlyDropped = append(newlyDropped, it)
		}
	}
	return newlyDropped
}

func randomDelayMs(minMs, maxMs int) time.Duration {
	if minMs < 500 {
		minMs = 1500
	}
	if maxMs < minMs {
		maxMs = minMs + 1000
	}
	delta := maxMs - minMs
	if delta == 0 {
		return time.Duration(minMs) * time.Millisecond
	}
	return time.Duration(minMs+rand.Intn(delta+1)) * time.Millisecond
}

// probeAnyServerPort dò nhẹ cổng TCP của máy chủ NRO đang được dàn acc dùng (hỗ trợ xuyên qua Proxy nếu IP gốc VPS bị chặn).
func (m *Manager) probeAnyServerPort() bool {
	type probeTarget struct {
		host     string
		port     int
		prx      model.ProxyItem
		useProxy bool
	}
	cfg := m.store.GetSettings()
	seen := make(map[string]bool)
	targets := make([]probeTarget, 0, 6)

	for _, acc := range m.store.GetAccounts() {
		sv, ok := m.store.GetServerByID(acc.ServerID)
		if !ok || sv.Host == "" {
			continue
		}
		port := sv.Port
		if port <= 0 {
			port = 14445
		}
		prxItem, hasPrx, _ := m.resolveProxyItemForAccount(acc, cfg)
		if hasPrx {
			key := fmt.Sprintf("%s:%d@%s:%d", sv.Host, port, prxItem.Host, prxItem.Port)
			if !seen[key] && len(targets) < 6 {
				seen[key] = true
				targets = append(targets, probeTarget{host: sv.Host, port: port, prx: prxItem, useProxy: true})
			}
		}
		directKey := fmt.Sprintf("direct|%s:%d", sv.Host, port)
		if !seen[directKey] && len(targets) < 8 {
			seen[directKey] = true
			targets = append(targets, probeTarget{host: sv.Host, port: port})
		}
	}
	if len(targets) == 0 {
		for _, sv := range m.store.GetServers() {
			if sv.Host != "" {
				port := sv.Port
				if port <= 0 {
					port = 14445
				}
				targets = append(targets, probeTarget{host: sv.Host, port: port})
				break
			}
		}
	}
	if len(targets) == 0 {
		return true
	}
	for _, t := range targets {
		if t.useProxy {
			if ProbeNROHandshakeViaProxy(t.prx, t.host, t.port, 5*time.Second) {
				return true
			}
		} else {
			addr := fmt.Sprintf("%s:%d", t.host, t.port)
			if ProbeNROHandshake(addr, 3500*time.Millisecond) {
				return true
			}
		}
	}
	return false
}

// ProbeNROHandshakeViaProxy kiểm tra bắt tay cmd -27 của máy chủ NRO xuyên qua Proxy HTTP/SOCKS5.
func ProbeNROHandshakeViaProxy(p model.ProxyItem, host string, port int, timeout time.Duration) bool {
	if timeout <= 0 {
		timeout = 5 * time.Second
	}
	ctx, cancel := context.WithTimeout(context.Background(), timeout)
	defer cancel()
	conn, err := proxy.DialTarget(ctx, p, host, port, timeout)
	if err != nil {
		return false
	}
	defer conn.Close()
	return ProbeNROHandshakeOnConn(conn, timeout)
}

// ProbeNROHandshake kiểm tra TCP + bắt tay chuẩn giao thức NRO (cmd -27 = 0xE5 xin session key).
// Chỉ trả true khi máy chủ NRO thực sự đã khởi động xong và nhả khóa mã hóa (không bị đánh lừa khi cổng mở nhưng server chưa sẵn sàng).
func ProbeNROHandshake(addr string, timeout time.Duration) bool {
	conn, err := net.DialTimeout("tcp", addr, timeout)
	if err != nil {
		return false
	}
	defer conn.Close()
	return ProbeNROHandshakeOnConn(conn, timeout)
}

// ProbeNROHandshakeOnConn thực thi trao đổi gói cmd -27 trên 1 kết nối TCP/Tunnel đã mở sẵn.
func ProbeNROHandshakeOnConn(conn net.Conn, timeout time.Duration) bool {
	_ = conn.SetDeadline(time.Now().Add(timeout))

	// Gửi gói cmd -27 (GET_SESSION_ID): [0xE5, 0x00, 0x00]
	if _, err := conn.Write([]byte{0xE5, 0x00, 0x00}); err != nil {
		return false
	}

	var hdr [3]byte
	if _, err := io.ReadFull(conn, hdr[:]); err != nil {
		return false
	}
	if hdr[0] != 0xE5 {
		return false
	}
	bodyLen := int(hdr[1])<<8 | int(hdr[2])
	if bodyLen < 2 || bodyLen > 4096 {
		return false
	}
	body := make([]byte, bodyLen)
	if _, err := io.ReadFull(conn, body); err != nil {
		return false
	}
	keyLen := int(body[0])
	return keyLen >= 1 && keyLen <= bodyLen-1
}

// IsInAnyTimeWindow kiểm tra thời điểm hiện tại có nằm trong bất kỳ khung giờ ca chạy nào không.
func IsInAnyTimeWindow(now time.Time, windows []model.TimeWindow) bool {
	validAny := false
	for _, w := range windows {
		if _, ok1 := parseHHMM(w.Start); !ok1 {
			continue
		}
		if _, ok2 := parseHHMM(w.End); !ok2 {
			continue
		}
		validAny = true
		if isInTimeWindow(now, w.Start, w.End) {
			return true
		}
	}
	// Nếu người dùng bật lịch nhưng để trống/sai định dạng hết các ô -> cho chạy bình thường tránh khóa nhầm
	return !validAny
}

// isInTimeWindow kiểm tra giờ hiện tại có nằm trong khoảng HH:MM .. HH:MM (hỗ trợ cả qua nửa đêm).
func isInTimeWindow(now time.Time, startStr, endStr string) bool {
	sMin, ok1 := parseHHMM(startStr)
	eMin, ok2 := parseHHMM(endStr)
	if !ok1 || !ok2 || sMin == eMin {
		return false
	}
	cur := now.Hour()*60 + now.Minute()
	if sMin < eMin {
		return cur >= sMin && cur < eMin
	}
	return cur >= sMin || cur < eMin
}

func parseHHMM(s string) (int, bool) {
	parts := strings.Split(strings.TrimSpace(s), ":")
	if len(parts) != 2 {
		return 0, false
	}
	var h, m int
	if _, err := fmt.Sscanf(parts[0], "%d", &h); err != nil || h < 0 || h > 23 {
		return 0, false
	}
	if _, err := fmt.Sscanf(parts[1], "%d", &m); err != nil || m < 0 || m > 59 {
		return 0, false
	}
	return h*60 + m, true
}
