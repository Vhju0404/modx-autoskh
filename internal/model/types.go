package model

import "time"

// ServerConfig lưu thông tin cụm máy chủ NRO chính (hoặc tự thêm IP:Port).
type ServerConfig struct {
	ID   string `json:"id"`
	Name string `json:"name"`
	Host string `json:"host"`
	Port int    `json:"port"`
}

// Account lưu cấu hình 1 tài khoản (mật khẩu lưu mã hóa trong PasswordEnc, không serialize plaintext ra disk).
type Account struct {
	ID          string `json:"id"`
	Tag         string `json:"tag"`
	Username    string `json:"username"`
	Password    string `json:"password,omitempty"`     // chỉ dùng khi giao tiếp UI/RAM, store tự mã hóa trước khi lưu
	PasswordEnc string `json:"password_enc,omitempty"` // lưu trên đĩa
	ServerID    string `json:"server_id"`
	ProfileID   string `json:"profile_id"`
	MapID       int    `json:"map_id"`        // -1 = dùng map mặc định theo Profile/Hành tinh
	ZoneMode    string `json:"zone_mode"`     // "profile" | "empty" | "fixed"
	ZoneID      int    `json:"zone_id"`       // dùng khi zone_mode == "fixed"
	CoordX      int    `json:"coord_x"`                 // dùng cho up đệ đứng tọa độ
	CoordY      int    `json:"coord_y"`
	ProxyID       string `json:"proxy_id"`                  // "auto" | "home" | "custom" | proxy.ID
	CustomProxy   string `json:"custom_proxy,omitempty"`    // Proxy riêng gắn trực tiếp cho acc (nhận đủ định dạng)
	TitleName     string `json:"title_name,omitempty"`      // Tên danh hiệu gần nhất (vd: Tay nhanh hơn não)
	TitleExpiryMs int64  `json:"title_expiry_ms,omitempty"` // Mốc Unix ms hết hạn danh hiệu để tự đi nhận lại
	Done          bool   `json:"done"`
	DoneReason  string `json:"done_reason"`
	Note        string `json:"note"`
	CreatedAt   int64  `json:"created_at"`
}

// FarmProfile định nghĩa 1 bộ luật farm độc lập (TDLT, Sư phụ thường, Up đệ, Map riêng...).
type FarmProfile struct {
	ID          string `json:"id"`
	Name        string `json:"name"`
	Description string `json:"description"`

	// Chế độ farm: "sp_tdlt" | "sp_normal" | "up_pet" | "private_ticket"
	FarmMode string `json:"farm_mode"`

	// Map & Khu
	DefaultMapTD     int    `json:"default_map_td"`     // Mặc định Trái Đất (vd 0: Làng Aru / 1: Đồi hoa cúc / 2: Thung lũng tre)
	DefaultMapNM     int    `json:"default_map_nm"`     // Mặc định Namek (vd 7: Làng Mori / 8: Đồi nấm tím / 9: Thị trấn Moori)
	DefaultMapXD     int    `json:"default_map_xd"`     // Mặc định Xayda (vd 14: Làng Kakarot / 15: Đồi hoang / 16: Làng Plant)
	ZoneStrategy     string `json:"zone_strategy"`      // "empty" | "fixed"
	FixedZoneID      int    `json:"fixed_zone_id"`      // khu cố định nếu ZoneStrategy=="fixed"
	MaxPlayersInZone int    `json:"max_players_zone"`   // khu đông hơn mức này -> đổi khu vắng
	NoExpSwitchSec   int    `json:"no_exp_switch_sec"`  // X giây không nhảy TN -> đổi khu trước
	NoExpRestartSec  int    `json:"no_exp_restart_sec"` // Y giây vẫn không nhảy TN -> thoát tab mở lại

	// Buff & Tài nguyên
	AutoBuyTDLT          bool   `json:"auto_buy_tdlt"`
	TDLTItemID           int    `json:"tdlt_item_id"`         // 521 (10p), 1523 (100p), 1524 (300p)
	TDLTMinMinutes       int    `json:"tdlt_min_minutes"`     // mua tới khi đạt ít nhất N phút
	AutoBuyLuckyGrass    bool   `json:"auto_buy_lucky"`       // Cỏ 4 lá (1635)
	LuckyGrassQty        int    `json:"lucky_grass_qty"`      // số lượng mua mỗi lần
	AutoBuyPrivateTicket bool   `json:"auto_buy_ticket"`      // Tự mua vé map riêng (1823) khi còn mầm
	AutoBuyGoldBar       bool   `json:"auto_buy_goldbar"`     // Tự đổi thỏi vàng (457) khi vàng >= 1.5 tỷ
	AutoGetTitle         bool   `json:"auto_get_title"`       // Tự mua 10 loa (1653) + nhận danh hiệu Tay nhanh hơn não (1673, +20% MM, 7 ngày)
	PetWiggle            bool   `json:"pet_wiggle"`           // Nhích qua lại chống đứng im khi Up Đệ
	MinGemKeep           int    `json:"min_gem_keep"`         // không mua nếu ngọc < mức này
	AutoBuyCharm      bool   `json:"auto_buy_charm"`    // Bùa Hạt Mít
	CharmDuration     string `json:"charm_duration"`    // "1h" | "8h" | "1m"
	CharmIDs          []int  `json:"charm_ids"`         // 213..219, 671, 672
	AutoBeanHP        int    `json:"auto_bean_hp"`      // % HP ăn đậu
	AutoBeanMP        int    `json:"auto_bean_mp"`      // % MP ăn đậu
	AutoPurpleGrape   int    `json:"auto_purple_grape"` // % thể lực ăn nho tím (211/212)
	AutoAskClanBean   bool   `json:"auto_ask_clan_bean"`
	AutoUpPotential   bool   `json:"auto_up_potential"`
	TargetHP          int    `json:"target_hp"`
	TargetKI          int    `json:"target_ki"`
	TargetSD          int    `json:"target_sd"`

	// Lọc đồ SKH & Đồ Sao
	KeepAllSKH     bool     `json:"keep_all_skh"`     // true = giữ mọi SKH; false = chỉ giữ KeepSKHSets
	KeepSKHSets    []string `json:"keep_skh_sets"`    // vd ["Songoku","Kirin","Kakarot","Cadic","Picolo"]
	MinKeepStars   int      `json:"min_keep_stars"`   // giữ đồ >= N sao (vd 3 => giữ 3,4,5,6,7 sao; 0 = không giữ đồ sao thường)
	AutoStoreHouse bool     `json:"auto_store_house"` // về nhà cất đồ đạt chuẩn vào rương
	OnBoxFull      string   `json:"on_box_full"`      // "keep_farming" | "mark_done_rotate" | "stop_at_home"
	AutoSellTrash  bool     `json:"auto_sell_trash"`  // full hành trang đi bán đồ rác ở map 24
	PickAll        bool     `json:"pick_all"`
	PickItemIDs    []int    `json:"pick_item_ids"`
	SellItemIDs    []int    `json:"sell_item_ids"`
	DropItemIDs    []int    `json:"drop_item_ids"`

	// Tối ưu hiệu năng client & chiến đấu
	GameSpeed        float64 `json:"game_speed"`          // Time.timeScale (1.0 - 3.0)
	CharSpeed        int     `json:"char_speed"`          // tốc độ chạy (mặc định 6)
	TargetFPS        int     `json:"target_fps"`          // mặc định 15 cho VPS
	BlackScreenVPS   bool    `json:"black_screen_vps"`    // chế độ màn đen siêu nhẹ
	ShowMobGraphics  bool    `json:"show_mob_graphics"`   // true = hiện hình ảnh quái; false = ẩn quái cho nhẹ VPS
	ShowCharGraphics bool    `json:"show_char_graphics"`  // true = hiện hình nhân vật & NPC; false = khung nhẹ
	FastAttackNoAnim    bool    `json:"fast_attack_no_anim"` // true = Xóa Anim (gửi gói 54 trực tiếp không doFire/anim, TDLT đứng chỗ đánh toàn map)
	AttackSkillMode     string  `json:"attack_skill_mode"`   // "punch_only" | "melee_only" | "auto_all" | "slots"
	AttackSkillSlots    []int   `json:"attack_skill_slots"`  // Danh sách ô phím 1..5 khi AttackSkillMode == "slots"
	TeleToMobTDLT       bool    `json:"tele_to_mob_tdlt"`    // false = đứng 1 chỗ đánh toàn map khi có TDLT (max tốc độ); true = dịch tới quái
	FreezeMobs          bool    `json:"freeze_mobs"`          // true = đóng băng quái tại (xFirst, yFirst) không chạy lung tung
	HorizontalInventory bool    `json:"horizontal_inventory"` // true = Hành trang ngang lưới 5 cột; false = Hành trang dọc chi tiết
	HeadlessMode        bool    `json:"headless_mode"`        // true = Unity -batchmode -nographics (treo ngầm 0 cửa sổ siêu nhẹ VPS)
}

// ProxyItem lưu thông tin 1 proxy HTTP hoặc SOCKS5.
type ProxyItem struct {
	ID          string `json:"id"`
	Raw         string `json:"raw"`
	Type        string `json:"type"` // "http" | "socks5"
	Host        string `json:"host"`
	Port        int    `json:"port"`
	Username    string `json:"username"`
	Password    string `json:"password"`
	Status      string `json:"status"` // "unknown" | "live" | "die"
	PingMs      int64  `json:"ping_ms"`
	LastCheckAt int64  `json:"last_check_at"`
}

// LootItem mô tả 1 món đồ SKH hoặc đồ Sao nằm trong rương / hành trang của acc.
type LootItem struct {
	TemplateID int      `json:"template_id"`
	Name       string   `json:"name"`
	Type       int      `json:"type"`       // 0=Áo, 1=Quần, 2=Găng, 3=Giày, 4=Rada
	Slot       string   `json:"slot"`       // "Áo" | "Quần" | "Găng" | "Giày" | "Rada"
	IsSKH      bool     `json:"is_skh"`
	SetName    string   `json:"set_name"`   // Chuẩn hóa: Songoku, Kirin, Thiên Xin Hăng, Kakarot, Cadic, Nappa, Ốc Tiêu, Pikkoro Daimao, Picolo
	Stars      int      `json:"stars"`      // Số sao pha lê (option 107)
	Location   string   `json:"location"`   // "bag" | "box"
	Options    []string `json:"options,omitempty"`
}

// LootHistoryEntry ghi nhận lịch sử rớt đồ SKH/Sao để theo dõi theo ngày.
type LootHistoryEntry struct {
	ID         string   `json:"id"`
	AccountID  string   `json:"account_id"`
	Username   string   `json:"username"`
	ServerName string   `json:"server_name"`
	Item       LootItem `json:"item"`
	FoundAt    int64    `json:"found_at"`
}

// AccountRuntime là trạng thái live nhận từ client game qua TCP NDJSON.
type AccountRuntime struct {
	AccountID     string     `json:"account_id"`
	Running       bool       `json:"running"`
	Connected     bool       `json:"connected"`
	Paused        bool       `json:"paused"`
	PID           int        `json:"pid"`
	HWND          uintptr    `json:"hwnd"`
	CharName      string     `json:"char_name"`
	Planet        string     `json:"planet"` // "TĐ" | "NM" | "XD"
	Gender        int        `json:"gender"` // 0=TĐ, 1=NM, 2=XD
	MapID         int        `json:"map_id"`
	MapName       string     `json:"map_name"`
	ZoneID        int        `json:"zone_id"`
	ZonePlayers   int        `json:"zone_players"`
	ZoneMax       int        `json:"zone_max"`
	CX            int        `json:"cx"`
	CY            int        `json:"cy"`
	InPrivateMap  bool       `json:"in_private_map"`
	TicketRemain  string     `json:"ticket_remain"`
	LastExpSec    int        `json:"last_exp_sec"`
	HitsPerMin    int        `json:"hits_per_min"`
	TotalHits     int        `json:"total_hits"`
	TargetMob     string     `json:"target_mob"`
	ActiveSkill   string     `json:"active_skill"`
	HP            int64      `json:"hp"`
	HPMax         int64      `json:"hp_max"`
	MP            int64      `json:"mp"`
	MPMax         int64      `json:"mp_max"`
	Power         int64      `json:"power"`
	Gold          int64      `json:"gold"`
	Gem           int        `json:"gem"`
	GemLock       int        `json:"gem_lock"`
	TDLTMinutes   int        `json:"tdlt_minutes"`
	HasTDLTActive bool       `json:"has_tdlt_active"`
	HasLuckyGrass bool       `json:"has_lucky_grass"`
	IsNewMember   bool       `json:"is_new_member"` // Còn mầm tìm SKH (isNewMember == 1)
	MamRemain     string     `json:"mam_remain"`    // Đếm ngược hạn mầm ("2 ngày 14 giờ")
	LuckPercent   int        `json:"luck_percent"`  // Tổng % May Mắn (phụ kiện + danh hiệu + Gohan + Cỏ)
	LuckDetail    string     `json:"luck_detail"`   // Chi tiết vd "PK:0% DH:20% Co:100%"
	TitleName     string     `json:"title_name"`    // Danh hiệu đang đeo (vd: Tay nhanh hơn não)
	TitleRemain   string     `json:"title_remain"`  // Thời gian còn lại của danh hiệu (vd: "6n 23g")
	TitleExpiryMs int64      `json:"title_expiry_ms"`
	BagUsed       int        `json:"bag_used"`
	BagMax        int        `json:"bag_max"`
	BoxUsed       int        `json:"box_used"`
	BoxMax        int        `json:"box_max"`
	FSMState      string     `json:"fsm_state"` // IDLE | LOGIN | TRAVEL | ZONE | FARM | STORE | SELL | BUFF | DEAD | PAUSED | DONE | ERROR
	StatusText    string     `json:"status_text"`
	ActiveProxy   string     `json:"active_proxy"`
	BlackScreen   bool       `json:"black_screen"`
	Headless      bool       `json:"headless"`
	Loot          []LootItem `json:"loot"`
	SKHCount      int        `json:"skh_count"`
	StarCount     int        `json:"star_count"`
	StartedAt     int64      `json:"started_at"`
	LastHeartbeat int64      `json:"last_heartbeat"`
	RestartCount  int        `json:"restart_count"`
	RamMB         int        `json:"ram_mb"`
	LastError     string     `json:"last_error"`
	RecentEvents  []string   `json:"recent_events,omitempty"`
}

// TimeWindow mô tả 1 ca chạy tự chọn trong ngày (HH:MM -> HH:MM, hỗ trợ vắt qua nửa đêm).
type TimeWindow struct {
	Start string `json:"start"`
	End   string `json:"end"`
}

// AccountRow ghép Account (config) + AccountRuntime (live) để trả 1 cục sạch cho UI.
type AccountRow struct {
	Account     Account        `json:"account"`
	Runtime     AccountRuntime `json:"runtime"`
	ServerName  string         `json:"server_name"`
	ProfileName string         `json:"profile_name"`
	ProxyLabel  string         `json:"proxy_label"`
	ProxyStatus string         `json:"proxy_status"`
}

// AppSettings lưu cài đặt toàn cục (VPS, Socket, Lịch ca, Bảo trì, Xả RAM, Telegram).
type AppSettings struct {
	SocketPort        int    `json:"socket_port"`
	UIPort            int    `json:"ui_port"`
	GameExePath       string `json:"game_exe_path"`
	MaxTabs           int    `json:"max_tabs"`
	WindowWidth       int    `json:"window_width"`
	WindowHeight      int    `json:"window_height"`
	AutoArrangeGrid   bool   `json:"auto_arrange_grid"`
	LoginDelayMinMs   int    `json:"login_delay_min_ms"`
	LoginDelayMaxMs   int    `json:"login_delay_max_ms"`
	MaxAccPerProxy    int    `json:"max_acc_per_proxy"`
	AutoRotateProxy   bool   `json:"auto_rotate_proxy"`
	StartupTimeoutSec int    `json:"startup_timeout_sec"`
	MaxRestarts10Min  int    `json:"max_restarts_10min"`
	MaxTabRamMB       int    `json:"max_tab_ram_mb"` // 0 = tắt; >0 = tự xả nhẹ & restart mềm khi vượt ngưỡng liên tục >= 3p lúc FARM

	ScheduleEnabled bool         `json:"schedule_enabled"`
	ScheduleWindows []TimeWindow `json:"schedule_windows"`

	MaintenanceEnabled bool   `json:"maintenance_enabled"`
	MaintenanceStart   string `json:"maintenance_start"` // "HH:MM"
	MaintenanceEnd     string `json:"maintenance_end"`   // "HH:MM"

	TelegramEnabled  bool   `json:"telegram_enabled"`
	TelegramBotToken string `json:"telegram_bot_token"`
	TelegramChatID   string `json:"telegram_chat_id"`
	NotifyOnSKH      bool   `json:"notify_on_skh"`
	NotifyOnFullSet  bool   `json:"notify_on_full_set"`
	NotifyOnBoxFull  bool   `json:"notify_on_box_full"`
	NotifyOnLowGem   bool   `json:"notify_on_low_gem"`
	NotifyOnProxyDie bool   `json:"notify_on_proxy_die"`
	PrivacyMode      bool   `json:"privacy_mode"`
}

// LogEntry 1 dòng log sự kiện cho UI (không spam frame).
type LogEntry struct {
	Time      string `json:"time"`
	Level     string `json:"level"` // INFO | WARN | ERROR | LOOT
	Account   string `json:"account"`
	Message   string `json:"message"`
	Timestamp int64  `json:"timestamp"`
}

// DashboardSummary thống kê KPI trên thanh đầu trang.
type DashboardSummary struct {
	AutoRunning     bool   `json:"auto_running"`
	InMaintenance   bool   `json:"in_maintenance"`
	OutsideSchedule bool   `json:"outside_schedule"`
	MaintReason     string `json:"maint_reason"`
	UIPort          int    `json:"ui_port"`
	SyncLocked      bool   `json:"sync_locked,omitempty"`
	PrivacyMode     bool   `json:"privacy_mode"`
	TotalAccounts   int    `json:"total_accounts"`
	RunningTabs     int    `json:"running_tabs"`
	MaxTabs         int    `json:"max_tabs"`
	DoneAccounts    int    `json:"done_accounts"`
	WarningAccounts int    `json:"warning_accounts"`
	TotalSKHItems   int    `json:"total_skh_items"`
	TotalStarItems  int    `json:"total_star_items"`
	CompleteSets    int    `json:"complete_sets"`
	LiveProxies     int    `json:"live_proxies"`
	TotalProxies    int    `json:"total_proxies"`
	ServerTimestamp int64  `json:"server_timestamp"`
}

// NowUnixMs trả Unix millisecond hiện tại.
func NowUnixMs() int64 {
	return time.Now().UnixMilli()
}
