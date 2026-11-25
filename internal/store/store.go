package store

import (
	"crypto/aes"
	"crypto/cipher"
	"crypto/rand"
	"encoding/base64"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"sync"

	"hteam/modx-autoskh/internal/model"
)

// Store quản lý toàn bộ dữ liệu bền vững dạng JSON atomic trong thư mục data/.
type Store struct {
	mu      sync.RWMutex
	dataDir string
	aead    cipher.AEAD

	Accounts    []model.Account          `json:"-"`
	Profiles    []model.FarmProfile      `json:"-"`
	Proxies     []model.ProxyItem        `json:"-"`
	Servers     []model.ServerConfig     `json:"-"`
	Settings    model.AppSettings        `json:"-"`
	LootHistory []model.LootHistoryEntry `json:"-"`
}

// New mở hoặc khởi tạo thư mục data/ với cấu hình mặc định chuẩn NRO chính.
func New(dataDir string) (*Store, error) {
	if err := os.MkdirAll(filepath.Join(dataDir, "runtime"), 0o755); err != nil {
		return nil, err
	}
	aead, err := loadOrCreateKey(filepath.Join(dataDir, ".vault.key"))
	if err != nil {
		return nil, err
	}

	s := &Store{
		dataDir: dataDir,
		aead:    aead,
	}
	if err := s.LoadAll(); err != nil {
		return nil, err
	}
	return s, nil
}

func (s *Store) DataDir() string {
	return s.dataDir
}

func (s *Store) RuntimeDir() string {
	return filepath.Join(s.dataDir, "runtime")
}

func loadOrCreateKey(path string) (cipher.AEAD, error) {
	var rawKey []byte
	if b, err := os.ReadFile(path); err == nil && len(b) == 32 {
		rawKey = b
	} else {
		rawKey = make([]byte, 32)
		if _, err := io.ReadFull(rand.Reader, rawKey); err != nil {
			return nil, err
		}
		if err := atomicWriteFile(path, rawKey); err != nil {
			return nil, err
		}
	}
	block, err := aes.NewCipher(rawKey)
	if err != nil {
		return nil, err
	}
	return cipher.NewGCM(block)
}

func (s *Store) encrypt(plain string) (string, error) {
	if plain == "" {
		return "", nil
	}
	nonce := make([]byte, s.aead.NonceSize())
	if _, err := io.ReadFull(rand.Reader, nonce); err != nil {
		return "", err
	}
	ct := s.aead.Seal(nonce, nonce, []byte(plain), nil)
	return base64.StdEncoding.EncodeToString(ct), nil
}

func (s *Store) decrypt(enc string) (string, error) {
	if enc == "" {
		return "", nil
	}
	raw, err := base64.StdEncoding.DecodeString(enc)
	if err != nil {
		return "", err
	}
	ns := s.aead.NonceSize()
	if len(raw) < ns {
		return "", errors.New("ciphertext quá ngắn")
	}
	pt, err := s.aead.Open(nil, raw[:ns], raw[ns:], nil)
	if err != nil {
		return "", err
	}
	return string(pt), nil
}

// LoadAll nạp dữ liệu từ đĩa, tự seed mặc định nếu chưa có file.
func (s *Store) LoadAll() error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if err := readJSON(filepath.Join(s.dataDir, "servers.json"), &s.Servers); err != nil || len(s.Servers) == 0 {
		s.Servers = DefaultOfficialServers()
		_ = writeJSONAtomic(filepath.Join(s.dataDir, "servers.json"), s.Servers)
	}

	if err := readJSON(filepath.Join(s.dataDir, "profiles.json"), &s.Profiles); err != nil || len(s.Profiles) == 0 {
		s.Profiles = DefaultFarmProfiles()
		_ = writeJSONAtomic(filepath.Join(s.dataDir, "profiles.json"), s.Profiles)
	} else {
		// Tự bổ sung Profile mẫu VPS 1-1 / 1-2 Siêu Nhẹ & chuẩn hóa AttackSkillMode mặc định cho dữ liệu cũ
		dirtyProf := false
		hasVps := false
		for i := range s.Profiles {
			if s.Profiles[i].ID == "prof_vps_lite" {
				hasVps = true
			}
			if s.Profiles[i].AttackSkillMode == "" {
				s.Profiles[i].AttackSkillMode = "punch_only"
				if len(s.Profiles[i].AttackSkillSlots) == 0 {
					s.Profiles[i].AttackSkillSlots = []int{1}
				}
				dirtyProf = true
			}
		}
		if !hasVps {
			for _, dp := range DefaultFarmProfiles() {
				if dp.ID == "prof_vps_lite" {
					s.Profiles = append(s.Profiles, dp)
					dirtyProf = true
					break
				}
			}
		}
		if dirtyProf {
			_ = writeJSONAtomic(filepath.Join(s.dataDir, "profiles.json"), s.Profiles)
		}
	}

	if err := readJSON(filepath.Join(s.dataDir, "settings.json"), &s.Settings); err != nil || s.Settings.SocketPort == 0 {
		s.Settings = DefaultAppSettings()
		_ = writeJSONAtomic(filepath.Join(s.dataDir, "settings.json"), s.Settings)
	} else {
		dirty := false
		if s.Settings.UIPort <= 0 {
			s.Settings.UIPort = 20269
			if s.Settings.MaxTabRamMB == 0 {
				s.Settings.MaxTabRamMB = 380
			}
			dirty = true
		}
		if s.Settings.ScheduleWindows == nil {
			s.Settings.ScheduleWindows = DefaultScheduleWindows()
			dirty = true
		}
		if dirty {
			_ = writeJSONAtomic(filepath.Join(s.dataDir, "settings.json"), s.Settings)
		}
	}

	_ = readJSON(filepath.Join(s.dataDir, "proxies.json"), &s.Proxies)
	if s.Proxies == nil {
		s.Proxies = []model.ProxyItem{}
	}

	var diskAccounts []model.Account
	_ = readJSON(filepath.Join(s.dataDir, "accounts.json"), &diskAccounts)
	s.Accounts = make([]model.Account, 0, len(diskAccounts))
	for _, a := range diskAccounts {
		if a.PasswordEnc != "" {
			if pw, err := s.decrypt(a.PasswordEnc); err == nil {
				a.Password = pw
			}
		}
		s.Accounts = append(s.Accounts, a)
	}

	_ = readJSON(filepath.Join(s.dataDir, "loot_history.json"), &s.LootHistory)
	if s.LootHistory == nil {
		s.LootHistory = []model.LootHistoryEntry{}
	}
	s.pruneOrphanDataLocked()
	return nil
}

// pruneOrphanDataLocked dọn sạch file runtime/profile_*.json và lịch sử loot mồ côi của các acc đã xóa.
func (s *Store) pruneOrphanDataLocked() {
	validIDs := make(map[string]bool, len(s.Accounts))
	for _, a := range s.Accounts {
		validIDs[a.ID] = true
	}

	// Dọn loot_history của các acc không còn trong accounts.json
	if len(s.LootHistory) > 0 {
		keptHist := make([]model.LootHistoryEntry, 0, len(s.LootHistory))
		for _, h := range s.LootHistory {
			if h.AccountID == "" || validIDs[h.AccountID] {
				keptHist = append(keptHist, h)
			}
		}
		if len(keptHist) != len(s.LootHistory) {
			s.LootHistory = keptHist
			_ = writeJSONAtomic(filepath.Join(s.dataDir, "loot_history.json"), s.LootHistory)
		}
	}

	// Dọn data/runtime/profile_<deletedID>.json
	entries, err := os.ReadDir(s.RuntimeDir())
	if err == nil {
		for _, e := range entries {
			if e.IsDir() {
				continue
			}
			name := e.Name()
			if len(name) > len("profile_.json") && name[:8] == "profile_" && name[len(name)-5:] == ".json" {
				id := name[8 : len(name)-5]
				if !validIDs[id] {
					_ = os.Remove(filepath.Join(s.RuntimeDir(), name))
				}
			}
		}
	}
}

func (s *Store) GetAccounts() []model.Account {
	s.mu.RLock()
	defer s.mu.RUnlock()
	out := make([]model.Account, len(s.Accounts))
	copy(out, s.Accounts)
	return out
}

func (s *Store) GetAccountByID(id string) (model.Account, bool) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	for _, a := range s.Accounts {
		if a.ID == id {
			return a, true
		}
	}
	return model.Account{}, false
}

func (s *Store) SaveAccounts(list []model.Account) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.Accounts = make([]model.Account, len(list))
	copy(s.Accounts, list)
	return s.persistAccountsLocked()
}

func (s *Store) UpdateAccount(acc model.Account) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	for i := range s.Accounts {
		if s.Accounts[i].ID == acc.ID {
			if acc.Password == "" {
				acc.Password = s.Accounts[i].Password
			}
			s.Accounts[i] = acc
			return s.persistAccountsLocked()
		}
	}
	s.Accounts = append(s.Accounts, acc)
	return s.persistAccountsLocked()
}

func (s *Store) DeleteAccounts(ids []string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	del := make(map[string]bool, len(ids))
	for _, id := range ids {
		del[id] = true
		_ = os.Remove(filepath.Join(s.RuntimeDir(), fmt.Sprintf("profile_%s.json", id)))
	}
	kept := make([]model.Account, 0, len(s.Accounts))
	for _, a := range s.Accounts {
		if !del[a.ID] {
			kept = append(kept, a)
		}
	}
	s.Accounts = kept

	// Dọn luôn lịch sử loot của các acc vừa xóa để Kho SKH & tổng quan đồng bộ 100%
	if len(s.LootHistory) > 0 {
		keptHist := make([]model.LootHistoryEntry, 0, len(s.LootHistory))
		for _, h := range s.LootHistory {
			if !del[h.AccountID] {
				keptHist = append(keptHist, h)
			}
		}
		if len(keptHist) != len(s.LootHistory) {
			s.LootHistory = keptHist
			_ = writeJSONAtomic(filepath.Join(s.dataDir, "loot_history.json"), s.LootHistory)
		}
	}

	return s.persistAccountsLocked()
}

func (s *Store) DeleteLootHistoryByAccounts(ids []string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if len(ids) == 0 || len(s.LootHistory) == 0 {
		return nil
	}
	del := make(map[string]bool, len(ids))
	for _, id := range ids {
		del[id] = true
	}
	kept := make([]model.LootHistoryEntry, 0, len(s.LootHistory))
	for _, h := range s.LootHistory {
		if !del[h.AccountID] {
			kept = append(kept, h)
		}
	}
	if len(kept) == len(s.LootHistory) {
		return nil
	}
	s.LootHistory = kept
	return writeJSONAtomic(filepath.Join(s.dataDir, "loot_history.json"), s.LootHistory)
}

func (s *Store) MarkAccountDone(id string, done bool, reason string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	for i := range s.Accounts {
		if s.Accounts[i].ID == id {
			s.Accounts[i].Done = done
			s.Accounts[i].DoneReason = reason
			return s.persistAccountsLocked()
		}
	}
	return nil
}

// UpdateAccountTitleInfo lưu tên & mốc hết hạn danh hiệu xuống accounts.json để nhớ xuyên các lần mở lại game.
func (s *Store) UpdateAccountTitleInfo(id string, titleName string, expiryMs int64) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	for i := range s.Accounts {
		if s.Accounts[i].ID == id {
			if s.Accounts[i].TitleName == titleName && s.Accounts[i].TitleExpiryMs == expiryMs {
				return nil
			}
			s.Accounts[i].TitleName = titleName
			s.Accounts[i].TitleExpiryMs = expiryMs
			return s.persistAccountsLocked()
		}
	}
	return nil
}

func (s *Store) persistAccountsLocked() error {
	disk := make([]model.Account, 0, len(s.Accounts))
	for _, a := range s.Accounts {
		enc, err := s.encrypt(a.Password)
		if err != nil {
			return err
		}
		copyAcc := a
		copyAcc.Password = ""
		copyAcc.PasswordEnc = enc
		disk = append(disk, copyAcc)
	}
	return writeJSONAtomic(filepath.Join(s.dataDir, "accounts.json"), disk)
}

func (s *Store) GetProfiles() []model.FarmProfile {
	s.mu.RLock()
	defer s.mu.RUnlock()
	out := make([]model.FarmProfile, len(s.Profiles))
	copy(out, s.Profiles)
	return out
}

func (s *Store) GetProfileByID(id string) (model.FarmProfile, bool) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	for _, p := range s.Profiles {
		if p.ID == id {
			return p, true
		}
	}
	if len(s.Profiles) > 0 {
		return s.Profiles[0], true
	}
	return model.FarmProfile{}, false
}

func (s *Store) SaveProfile(p model.FarmProfile) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	for i := range s.Profiles {
		if s.Profiles[i].ID == p.ID {
			s.Profiles[i] = p
			return writeJSONAtomic(filepath.Join(s.dataDir, "profiles.json"), s.Profiles)
		}
	}
	s.Profiles = append(s.Profiles, p)
	return writeJSONAtomic(filepath.Join(s.dataDir, "profiles.json"), s.Profiles)
}

func (s *Store) DeleteProfile(id string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if len(s.Profiles) <= 1 {
		return errors.New("cần giữ lại ít nhất 1 Profile")
	}
	kept := make([]model.FarmProfile, 0, len(s.Profiles))
	for _, p := range s.Profiles {
		if p.ID != id {
			kept = append(kept, p)
		}
	}
	s.Profiles = kept
	if err := writeJSONAtomic(filepath.Join(s.dataDir, "profiles.json"), s.Profiles); err != nil {
		return err
	}
	// Chuyển các tài khoản đang trỏ vào Profile vừa xóa sang Profile mặc định đầu tiên
	if len(s.Profiles) > 0 {
		fallbackID := s.Profiles[0].ID
		changed := false
		for i := range s.Accounts {
			if s.Accounts[i].ProfileID == id {
				s.Accounts[i].ProfileID = fallbackID
				changed = true
			}
		}
		if changed {
			_ = s.persistAccountsLocked()
		}
	}
	return nil
}

func (s *Store) GetProxies() []model.ProxyItem {
	s.mu.RLock()
	defer s.mu.RUnlock()
	out := make([]model.ProxyItem, len(s.Proxies))
	copy(out, s.Proxies)
	return out
}

func (s *Store) SaveProxies(list []model.ProxyItem) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.Proxies = make([]model.ProxyItem, len(list))
	copy(s.Proxies, list)
	return writeJSONAtomic(filepath.Join(s.dataDir, "proxies.json"), s.Proxies)
}

func (s *Store) GetServers() []model.ServerConfig {
	s.mu.RLock()
	defer s.mu.RUnlock()
	out := make([]model.ServerConfig, len(s.Servers))
	copy(out, s.Servers)
	return out
}

func (s *Store) GetServerByID(id string) (model.ServerConfig, bool) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	for _, sv := range s.Servers {
		if sv.ID == id {
			return sv, true
		}
	}
	if len(s.Servers) > 0 {
		return s.Servers[0], true
	}
	return model.ServerConfig{}, false
}

func (s *Store) SaveServers(list []model.ServerConfig) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.Servers = make([]model.ServerConfig, len(list))
	copy(s.Servers, list)
	return writeJSONAtomic(filepath.Join(s.dataDir, "servers.json"), s.Servers)
}

func (s *Store) GetSettings() model.AppSettings {
	s.mu.RLock()
	defer s.mu.RUnlock()
	return s.Settings
}

func (s *Store) SaveSettings(cfg model.AppSettings) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if cfg.SocketPort <= 0 {
		cfg.SocketPort = 20268
	}
	if cfg.UIPort <= 0 || cfg.UIPort > 65535 {
		cfg.UIPort = 20269
	}
	if cfg.MaxTabs <= 0 {
		cfg.MaxTabs = 5
	}
	if cfg.MaxTabRamMB < 0 {
		cfg.MaxTabRamMB = 0
	}
	if cfg.WindowWidth < 180 {
		cfg.WindowWidth = 380
	}
	if cfg.WindowHeight < 140 {
		cfg.WindowHeight = 240
	}
	if cfg.ScheduleWindows == nil {
		cfg.ScheduleWindows = []model.TimeWindow{}
	}
	s.Settings = cfg
	return writeJSONAtomic(filepath.Join(s.dataDir, "settings.json"), s.Settings)
}

func (s *Store) AppendLootHistory(entry model.LootHistoryEntry) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.LootHistory = append([]model.LootHistoryEntry{entry}, s.LootHistory...)
	if len(s.LootHistory) > 1000 {
		s.LootHistory = s.LootHistory[:1000]
	}
	return writeJSONAtomic(filepath.Join(s.dataDir, "loot_history.json"), s.LootHistory)
}

func (s *Store) GetLootHistory() []model.LootHistoryEntry {
	s.mu.RLock()
	defer s.mu.RUnlock()
	out := make([]model.LootHistoryEntry, len(s.LootHistory))
	copy(out, s.LootHistory)
	return out
}

func readJSON(path string, target interface{}) error {
	b, err := os.ReadFile(path)
	if err != nil {
		return err
	}
	return json.Unmarshal(b, target)
}

func writeJSONAtomic(path string, v interface{}) error {
	b, err := json.MarshalIndent(v, "", "  ")
	if err != nil {
		return err
	}
	return atomicWriteFile(path, b)
}

func atomicWriteFile(path string, data []byte) error {
	tmp := path + ".tmp"
	if err := os.WriteFile(tmp, data, 0o600); err != nil {
		return err
	}
	if err := os.Rename(tmp, path); err != nil {
		// Windows fallback nếu đích đang được mở đọc
		_ = os.Remove(path)
		if err2 := os.Rename(tmp, path); err2 != nil {
			_ = os.Remove(tmp)
			return fmt.Errorf("atomic rename failed: %w", err2)
		}
	}
	return nil
}

// DefaultOfficialServers nạp sẵn toàn bộ cụm Server NRO chính Teamobi (có thể chỉnh tay trong tab Cài Đặt).
func DefaultOfficialServers() []model.ServerConfig {
	return []model.ServerConfig{
		{ID: "sv1", Name: "Vũ trụ 1", Host: "dragon1.teamobi.com", Port: 14445},
		{ID: "sv2", Name: "Vũ trụ 2", Host: "dragon2.teamobi.com", Port: 14445},
		{ID: "sv3", Name: "Vũ trụ 3", Host: "dragon3.teamobi.com", Port: 14445},
		{ID: "sv4", Name: "Vũ trụ 4", Host: "dragon4.teamobi.com", Port: 14445},
		{ID: "sv5", Name: "Vũ trụ 5", Host: "dragon5.teamobi.com", Port: 14445},
		{ID: "sv6", Name: "Vũ trụ 6", Host: "dragon6.teamobi.com", Port: 14445},
		{ID: "sv7", Name: "Vũ trụ 7", Host: "dragon7.teamobi.com", Port: 14445},
		{ID: "sv8", Name: "Vũ trụ 8", Host: "dragon8.teamobi.com", Port: 14445},
		{ID: "sv9", Name: "Vũ trụ 9", Host: "dragon9.teamobi.com", Port: 14445},
		{ID: "sv10", Name: "Vũ trụ 10", Host: "dragon10.teamobi.com", Port: 14445},
		{ID: "sv11", Name: "Vũ trụ 11", Host: "dragon11.teamobi.com", Port: 14445},
		{ID: "sv12", Name: "Vũ trụ 12", Host: "dragon12.teamobi.com", Port: 14445},
		{ID: "sv13", Name: "Vũ trụ 13", Host: "dragon13.teamobi.com", Port: 14445},
		{ID: "sv14", Name: "Vũ trụ 14", Host: "dragon14.teamobi.com", Port: 14445},
		{ID: "super1", Name: "Super 1", Host: "super1.teamobi.com", Port: 14445},
		{ID: "super2", Name: "Super 2", Host: "super2.teamobi.com", Port: 14445},
	}
}

// DefaultFarmProfiles nạp sẵn 4 bộ Profile Up SKH chuẩn NRO chính.
func DefaultFarmProfiles() []model.FarmProfile {
	allSets := []string{
		"Songoku", "Kirin", "Thiên Xin Hăng", "Thần Vũ Trụ Kaio",
		"Ốc Tiêu", "Pikkoro Daimao", "Picolo", "Nail Chiến Binh Namếc",
		"Kakarot", "Cadic M", "Cadic", "Nappa",
		"Gohan", "Thần Hủy Diệt Champa",
	}
	return []model.FarmProfile{
		{
			ID:                "prof_tdlt",
			Name:              "TDLT — Up SKH Chuẩn",
			Description:       "Sư phụ dùng TDLT + Cỏ 4 lá, tự cất mọi SKH & đồ >= 3 sao, full túi bán rác, đổi khu vắng chống KS.",
			FarmMode:          "sp_tdlt",
			DefaultMapTD:      1,  // Đồi hoa cúc
			DefaultMapNM:      8,  // Đồi nấm tím
			DefaultMapXD:      15, // Đồi hoang
			ZoneStrategy:      "empty",
			FixedZoneID:       0,
			MaxPlayersInZone:  3,
			NoExpSwitchSec:    35,
			NoExpRestartSec:   95,
			AutoBuyTDLT:       true,
			TDLTItemID:        521,
			TDLTMinMinutes:    20,
			AutoBuyLuckyGrass: true,
			LuckyGrassQty:     1,
			AutoBuyGoldBar:    true,
			MinGemKeep:        5,
			AutoBuyCharm:      false,
			CharmDuration:     "1h",
			CharmIDs:          []int{},
			AutoBeanHP:        25,
			AutoBeanMP:        20,
			AutoPurpleGrape:   10,
			AutoAskClanBean:   true,
			AutoUpPotential:   false,
			TargetHP:          0,
			TargetKI:          0,
			TargetSD:          0,
			KeepAllSKH:        true,
			KeepSKHSets:       allSets,
			MinKeepStars:      3,
			AutoStoreHouse:    true,
			OnBoxFull:         "keep_farming",
			AutoSellTrash:     true,
			PickAll:           false,
			PickItemIDs:       []int{77, 861},
			SellItemIDs:       []int{},
			DropItemIDs:       []int{},
			GameSpeed:         2.0,
			CharSpeed:         6,
			TargetFPS:         15,
			BlackScreenVPS:    false,
			ShowMobGraphics:   true,
			ShowCharGraphics:  true,
			FastAttackNoAnim:  true,
			AttackSkillMode:   "punch_only",
			AttackSkillSlots:  []int{1},
			TeleToMobTDLT:     false,
			FreezeMobs:        true,
		},
		{
			ID:                "prof_free",
			Name:              "Sư Phụ Đánh Thường (Free Ngọc)",
			Description:       "Không tốn ngọc mua TDLT/Cỏ, tự đánh quái nhặt đồ SKH + >= 3 sao, tối ưu cày dàn clone đông.",
			FarmMode:          "sp_normal",
			DefaultMapTD:      0,
			DefaultMapNM:      7,
			DefaultMapXD:      14,
			ZoneStrategy:      "empty",
			FixedZoneID:       0,
			MaxPlayersInZone:  4,
			NoExpSwitchSec:    40,
			NoExpRestartSec:   100,
			AutoBuyTDLT:       false,
			TDLTItemID:        521,
			TDLTMinMinutes:    0,
			AutoBuyLuckyGrass: false,
			LuckyGrassQty:     1,
			MinGemKeep:        0,
			AutoBeanHP:        25,
			AutoBeanMP:        20,
			AutoPurpleGrape:   10,
			AutoAskClanBean:   true,
			KeepAllSKH:        true,
			KeepSKHSets:       allSets,
			MinKeepStars:      3,
			AutoStoreHouse:    true,
			OnBoxFull:         "keep_farming",
			AutoSellTrash:     true,
			PickAll:           false,
			PickItemIDs:       []int{77, 861},
			GameSpeed:         2.0,
			CharSpeed:         6,
			TargetFPS:         13,
			BlackScreenVPS:    false,
			ShowMobGraphics:   true,
			ShowCharGraphics:  true,
			FastAttackNoAnim:  true,
			AttackSkillMode:   "punch_only",
			AttackSkillSlots:  []int{1},
			TeleToMobTDLT:     false,
			FreezeMobs:        true,
		},
		{
			ID:                "prof_pet",
			Name:              "Up Đệ Tử Săn SKH",
			Description:       "Bật đệ bảo vệ, tự fix khi đệ kêu 'Sao sư phụ không đánh đi', đứng tọa độ cố định, ăn đậu cho đệ.",
			FarmMode:          "up_pet",
			DefaultMapTD:      1,
			DefaultMapNM:      8,
			DefaultMapXD:      15,
			ZoneStrategy:      "empty",
			MaxPlayersInZone:  2,
			NoExpSwitchSec:    45,
			NoExpRestartSec:   120,
			AutoBuyTDLT:       false,
			TDLTItemID:        521,
			AutoBuyLuckyGrass: false,
			LuckyGrassQty:     1,
			PetWiggle:         true,
			AutoBeanHP:        30,
			AutoBeanMP:        25,
			AutoPurpleGrape:   10,
			AutoAskClanBean:   true,
			KeepAllSKH:        true,
			KeepSKHSets:       allSets,
			MinKeepStars:      3,
			AutoStoreHouse:    true,
			OnBoxFull:         "keep_farming",
			AutoSellTrash:     true,
			PickAll:           false,
			PickItemIDs:       []int{77, 861},
			GameSpeed:         2.0,
			CharSpeed:         6,
			TargetFPS:         15,
			BlackScreenVPS:    false,
			ShowMobGraphics:   true,
			ShowCharGraphics:  true,
			FastAttackNoAnim:  false,
			AttackSkillMode:   "punch_only",
			AttackSkillSlots:  []int{1},
			TeleToMobTDLT:     false,
			FreezeMobs:        false,
		},
		{
			ID:                   "prof_ticket",
			Name:                 "Map Riêng (Vé 1823/1825)",
			Description:          "Tự mua vé 1823 (nếu còn mầm) & dùng vé vào map riêng farm SKH không sợ KS, dư vàng mua thỏi 457.",
			FarmMode:             "private_ticket",
			DefaultMapTD:         3,
			DefaultMapNM:         11,
			DefaultMapXD:         17,
			ZoneStrategy:         "empty",
			MaxPlayersInZone:     15,
			NoExpSwitchSec:       0,
			NoExpRestartSec:      100,
			AutoBuyTDLT:          true,
			TDLTItemID:           521,
			TDLTMinMinutes:       20,
			AutoBuyLuckyGrass:    true,
			LuckyGrassQty:        1,
			AutoBuyPrivateTicket: true,
			AutoBuyGoldBar:       true,
			MinGemKeep:           5,
			AutoBeanHP:        25,
			AutoBeanMP:        20,
			AutoPurpleGrape:   10,
			AutoAskClanBean:   true,
			KeepAllSKH:        true,
			KeepSKHSets:       allSets,
			MinKeepStars:      3,
			AutoStoreHouse:    true,
			OnBoxFull:         "keep_farming",
			AutoSellTrash:     true,
			PickAll:           false,
			PickItemIDs:       []int{77, 861},
			GameSpeed:         2.0,
			CharSpeed:         6,
			TargetFPS:         15,
			BlackScreenVPS:    false,
			ShowMobGraphics:   true,
			ShowCharGraphics:  true,
			FastAttackNoAnim:  true,
			AttackSkillMode:   "punch_only",
			AttackSkillSlots:  []int{1},
			TeleToMobTDLT:     false,
			FreezeMobs:        true,
		},
		{
			ID:                "prof_vps_lite",
			Name:              "VPS 1-1 / 1-2 Siêu Nhẹ",
			Description:       "Tối ưu riêng cho VPS Win 2012 R2 (1 Core / 1-2GB RAM): Treo ngầm Headless 0 cửa sổ (~0% CPU, giảm 3× RAM), ẩn NV & quái, tự cất SKH & bán rác 24/7.",
			FarmMode:          "sp_tdlt",
			DefaultMapTD:      1,
			DefaultMapNM:      8,
			DefaultMapXD:      15,
			ZoneStrategy:      "empty",
			FixedZoneID:       0,
			MaxPlayersInZone:  3,
			NoExpSwitchSec:    35,
			NoExpRestartSec:   95,
			AutoBuyTDLT:       true,
			TDLTItemID:        521,
			TDLTMinMinutes:    20,
			AutoBuyLuckyGrass: true,
			LuckyGrassQty:     1,
			AutoBuyGoldBar:    true,
			MinGemKeep:        5,
			AutoBeanHP:        25,
			AutoBeanMP:        20,
			AutoPurpleGrape:   10,
			AutoAskClanBean:   true,
			KeepAllSKH:        true,
			KeepSKHSets:       allSets,
			MinKeepStars:      3,
			AutoStoreHouse:    true,
			OnBoxFull:         "keep_farming",
			AutoSellTrash:     true,
			PickAll:           false,
			PickItemIDs:       []int{77, 861},
			GameSpeed:         2.0,
			CharSpeed:         6,
			TargetFPS:         10,
			BlackScreenVPS:    true,
			ShowMobGraphics:   false,
			ShowCharGraphics:  false,
			FastAttackNoAnim:  true,
			AttackSkillMode:   "punch_only",
			AttackSkillSlots:  []int{1},
			TeleToMobTDLT:     false,
			FreezeMobs:        true,
			HeadlessMode:      true,
		},
	}
}

func DefaultScheduleWindows() []model.TimeWindow {
	return []model.TimeWindow{
		{Start: "04:30", End: "12:30"},
		{Start: "16:30", End: "00:30"},
	}
}

func DefaultAppSettings() model.AppSettings {
	return model.AppSettings{
		SocketPort:         20268,
		UIPort:             20269,
		GameExePath:        `game\Dragonboy_vn_v231.exe`,
		MaxTabs:            5,
		WindowWidth:        380,
		WindowHeight:       240,
		AutoArrangeGrid:    true,
		LoginDelayMinMs:    2000,
		LoginDelayMaxMs:    4000,
		MaxAccPerProxy:     2,
		AutoRotateProxy:    true,
		StartupTimeoutSec:  45,
		MaxRestarts10Min:   5,
		MaxTabRamMB:        380,
		ScheduleEnabled:    false,
		ScheduleWindows:    DefaultScheduleWindows(),
		MaintenanceEnabled: false,
		MaintenanceStart:   "02:55",
		MaintenanceEnd:     "04:05",
		TelegramEnabled:    false,
		NotifyOnSKH:        true,
		NotifyOnFullSet:    true,
		NotifyOnBoxFull:    true,
		NotifyOnLowGem:     true,
		NotifyOnProxyDie:   true,
	}
}

// PresetSKHMaps danh sách map farm SKH phổ biến trên NRO chính để hiển thị dropdown chọn nhanh.
type MapPreset struct {
	ID     int    `json:"id"`
	Name   string `json:"name"`
	Planet string `json:"planet"`
}

func PresetSKHMaps() []MapPreset {
	return []MapPreset{
		{ID: -1, Name: "Tự động theo Profile & Hành tinh", Planet: "ALL"},
		{ID: 0, Name: "0 — Làng Aru (Trái Đất)", Planet: "TĐ"},
		{ID: 1, Name: "1 — Đồi hoa cúc (Trái Đất)", Planet: "TĐ"},
		{ID: 2, Name: "2 — Thung lũng tre (Trái Đất)", Planet: "TĐ"},
		{ID: 3, Name: "3 — Rừng nấm (Trái Đất)", Planet: "TĐ"},
		{ID: 4, Name: "4 — Rừng xương (Trái Đất)", Planet: "TĐ"},
		{ID: 7, Name: "7 — Làng Mori (Namek)", Planet: "NM"},
		{ID: 8, Name: "8 — Đồi nấm tím (Namek)", Planet: "NM"},
		{ID: 9, Name: "9 — Thị trấn Moori (Namek)", Planet: "NM"},
		{ID: 11, Name: "11 — Thung lũng Maima (Namek)", Planet: "NM"},
		{ID: 12, Name: "12 — Vực Maima (Namek)", Planet: "NM"},
		{ID: 14, Name: "14 — Làng Kakarot (Xayda)", Planet: "XD"},
		{ID: 15, Name: "15 — Đồi hoang (Xayda)", Planet: "XD"},
		{ID: 16, Name: "16 — Làng Plant (Xayda)", Planet: "XD"},
		{ID: 17, Name: "17 — Rừng nguyên sinh (Xayda)", Planet: "XD"},
		{ID: 18, Name: "18 — Rừng thông Xayda (Xayda)", Planet: "XD"},
	}
}
