package loot

import (
	"fmt"
	"sort"
	"strings"

	"hteam/modx-autoskh/internal/model"
)

// AllSKHSets là toàn bộ 14 bộ Set Kích Hoạt chuẩn NRO chính (đối chiếu trực tiếp NRitem0 Teamobi:
// 127..135, 233, 237, 241, 245, 254 — gồm 4 bộ TĐ, 4 bộ NM, 4 bộ XD và 2 bộ dùng chung ALL).
// Lưu ý thứ tự: Pikkoro Daimao đứng trước Picolo, Cadic M đứng trước Cadic để match chuẩn không lẫn!
var AllSKHSets = []SetMeta{
	// Trái Đất (4 bộ)
	{Name: "Songoku", ShortName: "SGK", Planet: "TĐ", Aliases: []string{"songoku", "sôngôku", "son goku", "sgk"}},
	{Name: "Kirin", ShortName: "Kirin", Planet: "TĐ", Aliases: []string{"kirin"}},
	{Name: "Thiên Xin Hăng", ShortName: "TXH", Planet: "TĐ", Aliases: []string{"thên xin hăng", "thiên xin hăng", "thien xin hang", "then xin hang", "txh"}},
	{Name: "Thần Vũ Trụ Kaio", ShortName: "Kaio", Planet: "TĐ", Aliases: []string{"thần vũ trụ kaio", "than vu tru kaio", "vũ trụ kaio", "vu tru kaio", "kaio"}},
	// Namek (4 bộ)
	{Name: "Ốc Tiêu", ShortName: "Ốc Tiêu", Planet: "NM", Aliases: []string{"ốc tiêu", "oc tieu"}},
	{Name: "Pikkoro Daimao", ShortName: "Daimao", Planet: "NM", Aliases: []string{"daimao", "pikkoro daimao", "picolo daimao"}},
	{Name: "Picolo", ShortName: "Picolo", Planet: "NM", Aliases: []string{"picolo", "pikkoro"}},
	{Name: "Nail Chiến Binh Namếc", ShortName: "Nail", Planet: "NM", Aliases: []string{"nail chiến binh", "nail chien binh", "chiến binh namếc", "chien binh namec", "nail"}},
	// Xayda (4 bộ)
	{Name: "Kakarot", ShortName: "Kakarot", Planet: "XD", Aliases: []string{"kakarot", "ca ca rot"}},
	{Name: "Cadic M", ShortName: "Cadic M", Planet: "XD", Aliases: []string{"cađic m", "cadic m", "ca đíc m", "ca dic m", "cađíc m"}},
	{Name: "Cadic", ShortName: "Cadic", Planet: "XD", Aliases: []string{"ca đíc", "cađic", "cadic", "ca dic"}},
	{Name: "Nappa", ShortName: "Nappa", Planet: "XD", Aliases: []string{"nappa"}},
	// Dùng chung 3 hành tinh (2 bộ mới)
	{Name: "Gohan", ShortName: "Gohan", Planet: "ALL", Aliases: []string{"gohan"}},
	{Name: "Thần Hủy Diệt Champa", ShortName: "Champa", Planet: "ALL", Aliases: []string{"thần hủy diệt champa", "than huy diet champa", "hủy diệt champa", "huy diet champa", "champa"}},
}

// SlotOrder thứ tự 5 món ghép đủ 1 bộ SKH: Áo - Quần - Găng - Giày - Rada.
var SlotOrder = []string{"Áo", "Quần", "Găng", "Giày", "Rada"}

type SetMeta struct {
	Name      string   `json:"name"`
	ShortName string   `json:"short_name"`
	Planet    string   `json:"planet"` // "TĐ" | "NM" | "XD" | "ALL"
	Aliases   []string `json:"aliases"`
}

// SetProgress thể hiện tiến độ ghép 5 món của 1 Set (kèm mã 0-0-0-0-0 theo Áo-Quần-Găng-Giày-Rada).
type SetProgress struct {
	SetName      string              `json:"set_name"`
	ShortName    string              `json:"short_name"`
	Planet       string              `json:"planet"`
	Counts       [5]int              `json:"counts"` // [Áo, Quần, Găng, Giày, Rada]
	Code         string              `json:"code"`   // vd "1-0-0-1-0" hoặc "0-0-0-0-0"
	Slots        map[string]int      `json:"slots"`  // "Áo": 1, "Quần": 0...
	SlotOwners   map[string][]string `json:"slot_owners"`
	HaveSlots    int                 `json:"have_slots"` // 0..5
	MissingSlots []string            `json:"missing_slots"`
	TotalItems   int                 `json:"total_items"`
	CompleteSets int                 `json:"complete_sets"` // số bộ 5/5 hoàn chỉnh ghép được
}

// AccountLootSummary gom nhóm đồ SKH & sao của 1 tài khoản (luôn có bảng 0-0-0-0-0 cho các bộ hành tinh + chung).
type AccountLootSummary struct {
	AccountID  string           `json:"account_id"`
	Username   string           `json:"username"`
	ServerName string           `json:"server_name"`
	Planet     string           `json:"planet"`
	SKHCount   int              `json:"skh_count"`
	StarCount  int              `json:"star_count"`
	Items      []model.LootItem `json:"items"`
	Sets       []SetProgress    `json:"sets"`
}

// LootReport tổng hợp cho tab Kho SKH & Ghép Set.
type LootReport struct {
	GlobalSets        []SetProgress        `json:"global_sets"`
	Accounts          []AccountLootSummary `json:"accounts"`
	TotalSKH          int                  `json:"total_skh"`
	TotalStars        int                  `json:"total_stars"`
	TotalCompleteSets int                  `json:"total_complete_sets"`
}

// NormalizeSetName chuẩn hóa tên set từ dòng option game về tên chuẩn.
func NormalizeSetName(raw string) string {
	s := strings.TrimSpace(strings.ToLower(raw))
	if s == "" {
		return ""
	}
	s = strings.TrimPrefix(s, "set ")
	for _, meta := range AllSKHSets {
		for _, alias := range meta.Aliases {
			if strings.Contains(s, alias) {
				return meta.Name
			}
		}
	}
	return strings.TrimSpace(raw)
}

// SlotFromType chuẩn hóa loại trang bị NRO (0..4) sang tên món.
func SlotFromType(itemType int, itemName string) string {
	switch itemType {
	case 0:
		return "Áo"
	case 1:
		return "Quần"
	case 2:
		return "Găng"
	case 3:
		return "Giày"
	case 4:
		return "Rada"
	}
	lower := strings.ToLower(itemName)
	switch {
	case strings.Contains(lower, "áo") || strings.Contains(lower, "ao "):
		return "Áo"
	case strings.Contains(lower, "quần") || strings.Contains(lower, "quan "):
		return "Quần"
	case strings.Contains(lower, "găng") || strings.Contains(lower, "gang "):
		return "Găng"
	case strings.Contains(lower, "giày") || strings.Contains(lower, "giay "):
		return "Giày"
	case strings.Contains(lower, "rada") || strings.Contains(lower, "nhẫn") || strings.Contains(lower, "ra đa"):
		return "Rada"
	default:
		return "Khác"
	}
}

// PlanetOfSet trả hành tinh của Set SKH.
func PlanetOfSet(setName string) string {
	for _, m := range AllSKHSets {
		if strings.EqualFold(m.Name, setName) {
			return m.Planet
		}
	}
	return "?"
}

// ShortNameOfSet trả tên rút gọn của Set SKH (SGK, Kirin, TXH, Kaio, Daimao, Nail, Cadic M, Gohan, Champa...).
func ShortNameOfSet(setName string) string {
	for _, m := range AllSKHSets {
		if strings.EqualFold(m.Name, setName) {
			return m.ShortName
		}
	}
	return setName
}

// NormalizeItems chuẩn hóa slot và set_name cho danh sách item từ client.
func NormalizeItems(items []model.LootItem) []model.LootItem {
	out := make([]model.LootItem, 0, len(items))
	for _, it := range items {
		it.Slot = SlotFromType(it.Type, it.Name)
		if it.IsSKH || it.SetName != "" {
			it.IsSKH = true
			it.SetName = NormalizeSetName(it.SetName)
		}
		out = append(out, it)
	}
	return out
}

// BuildReport tổng hợp tiến độ ghép 5 món theo từng acc và toàn dàn acc.
// Luôn hiện đủ 14 bộ SKH (không có vẫn ghi 0-0-0-0-0 theo Áo-Quần-Găng-Giày-Rada).
func BuildReport(rows []model.AccountRow) LootReport {
	globalBySet := make(map[string]*SetProgress, len(AllSKHSets))
	for _, meta := range AllSKHSets {
		ensureSetEntry(globalBySet, meta.Name)
	}

	accSummaries := make([]AccountLootSummary, 0, len(rows))
	totalSKH := 0
	totalStars := 0

	for _, r := range rows {
		items := NormalizeItems(r.Runtime.Loot)
		accBySet := make(map[string]*SetProgress)

		// Luôn khởi tạo sẵn 4 bộ theo hành tinh + 2 bộ dùng chung (Gohan, Champa) ở mức 0-0-0-0-0
		if r.Runtime.Planet != "" {
			for _, meta := range AllSKHSets {
				if meta.Planet == r.Runtime.Planet || meta.Planet == "ALL" {
					ensureSetEntry(accBySet, meta.Name)
				}
			}
		}

		accSKH := 0
		accStar := 0
		for _, it := range items {
			if it.IsSKH && it.SetName != "" {
				accSKH++
				totalSKH++
				addToSetMap(accBySet, it.SetName, it.Slot, r.Account.Username)
				addToSetMap(globalBySet, it.SetName, it.Slot, r.Account.Username)
			} else if it.Stars > 0 {
				accStar++
				totalStars++
			}
		}

		accSummaries = append(accSummaries, AccountLootSummary{
			AccountID:  r.Account.ID,
			Username:   r.Account.Username,
			ServerName: r.ServerName,
			Planet:     r.Runtime.Planet,
			SKHCount:   accSKH,
			StarCount:  accStar,
			Items:      items,
			Sets:       finalizeSetList(accBySet, false),
		})
	}

	globalSets := finalizeSetList(globalBySet, true)
	completeTotal := 0
	for _, s := range globalSets {
		completeTotal += s.CompleteSets
	}

	sort.Slice(accSummaries, func(i, j int) bool {
		if accSummaries[i].SKHCount != accSummaries[j].SKHCount {
			return accSummaries[i].SKHCount > accSummaries[j].SKHCount
		}
		if accSummaries[i].StarCount != accSummaries[j].StarCount {
			return accSummaries[i].StarCount > accSummaries[j].StarCount
		}
		return accSummaries[i].Username < accSummaries[j].Username
	})

	return LootReport{
		GlobalSets:        globalSets,
		Accounts:          accSummaries,
		TotalSKH:          totalSKH,
		TotalStars:        totalStars,
		TotalCompleteSets: completeTotal,
	}
}

func ensureSetEntry(m map[string]*SetProgress, setName string) *SetProgress {
	sp, ok := m[setName]
	if !ok {
		slots := make(map[string]int, len(SlotOrder))
		for _, sl := range SlotOrder {
			slots[sl] = 0
		}
		sp = &SetProgress{
			SetName:    setName,
			ShortName:  ShortNameOfSet(setName),
			Planet:     PlanetOfSet(setName),
			Slots:      slots,
			SlotOwners: make(map[string][]string),
		}
		m[setName] = sp
	}
	return sp
}

func addToSetMap(m map[string]*SetProgress, setName, slot, owner string) {
	sp := ensureSetEntry(m, setName)
	sp.TotalItems++
	if isCoreSlot(slot) {
		sp.Slots[slot]++
		if !containsStr(sp.SlotOwners[slot], owner) {
			sp.SlotOwners[slot] = append(sp.SlotOwners[slot], owner)
		}
	}
}

func canonicalSetOrder(setName string) int {
	for i, m := range AllSKHSets {
		if strings.EqualFold(m.Name, setName) {
			return i
		}
	}
	return 999
}

func finalizeSetList(m map[string]*SetProgress, floatNonEmpty bool) []SetProgress {
	out := make([]SetProgress, 0, len(m))
	for _, sp := range m {
		have := 0
		minSlot := -1
		var missing []string
		var counts [5]int
		for idx, slot := range SlotOrder {
			cnt := sp.Slots[slot]
			sp.Slots[slot] = cnt
			counts[idx] = cnt
			if cnt > 0 {
				have++
			} else {
				missing = append(missing, slot)
			}
			if minSlot == -1 || cnt < minSlot {
				minSlot = cnt
			}
		}
		if minSlot < 0 {
			minSlot = 0
		}
		sp.Counts = counts
		sp.Code = fmt.Sprintf("%d-%d-%d-%d-%d", counts[0], counts[1], counts[2], counts[3], counts[4])
		sp.HaveSlots = have
		sp.MissingSlots = missing
		sp.CompleteSets = minSlot
		out = append(out, *sp)
	}
	sort.Slice(out, func(i, j int) bool {
		if floatNonEmpty {
			if out[i].HaveSlots != out[j].HaveSlots {
				return out[i].HaveSlots > out[j].HaveSlots
			}
			if out[i].TotalItems != out[j].TotalItems {
				return out[i].TotalItems > out[j].TotalItems
			}
		}
		oi, oj := canonicalSetOrder(out[i].SetName), canonicalSetOrder(out[j].SetName)
		if oi != oj {
			return oi < oj
		}
		return out[i].SetName < out[j].SetName
	})
	return out
}

func isCoreSlot(slot string) bool {
	for _, s := range SlotOrder {
		if s == slot {
			return true
		}
	}
	return false
}

func containsStr(list []string, target string) bool {
	for _, s := range list {
		if s == target {
			return true
		}
	}
	return false
}
