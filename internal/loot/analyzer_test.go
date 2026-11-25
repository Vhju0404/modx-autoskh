package loot

import (
	"testing"

	"hteam/modx-autoskh/internal/model"
)

func TestNormalizeAndBuildReport(t *testing.T) {
	rows := []model.AccountRow{
		{
			Account:    model.Account{ID: "a1", Username: "clone1"},
			ServerName: "Vũ trụ 1",
			Runtime: model.AccountRuntime{
				Planet: "TĐ",
				Loot: []model.LootItem{
					{TemplateID: 0, Name: "Áo vải 3 lỗ", Type: 0, IsSKH: true, SetName: "Set Sôngôku", Stars: 2, Location: "box"},
					{TemplateID: 6, Name: "Quần vải đen", Type: 1, IsSKH: true, SetName: "Set Songoku", Stars: 0, Location: "box"},
					{TemplateID: 21, Name: "Găng vải đen", Type: 2, IsSKH: true, SetName: "Set Songoku", Stars: 1, Location: "bag"},
					{TemplateID: 27, Name: "Giày vải đen", Type: 3, IsSKH: false, Stars: 5, Location: "box"},
				},
			},
		},
		{
			Account:    model.Account{ID: "a2", Username: "clone2"},
			ServerName: "Vũ trụ 1",
			Runtime: model.AccountRuntime{
				Planet: "TĐ",
				Loot: []model.LootItem{
					{TemplateID: 27, Name: "Giày vải đen", Type: 3, IsSKH: true, SetName: "Set Songoku", Stars: 0, Location: "box"},
					{TemplateID: 12, Name: "Rada cấp 1", Type: 4, IsSKH: true, SetName: "Set Songoku", Stars: 3, Location: "box"},
					{TemplateID: 0, Name: "Áo vải", Type: 0, IsSKH: true, SetName: "Set Pikkoro Daimao", Stars: 0, Location: "box"},
				},
			},
		},
	}

	rep := BuildReport(rows)
	if rep.TotalSKH != 6 {
		t.Fatalf("expected 6 SKH items, got %d", rep.TotalSKH)
	}
	if rep.TotalStars != 1 {
		t.Fatalf("expected 1 non-SKH star item (5 sao), got %d", rep.TotalStars)
	}
	if rep.TotalCompleteSets != 1 {
		t.Fatalf("expected 1 complete 5/5 Songoku set across accounts, got %d", rep.TotalCompleteSets)
	}
	if len(rep.GlobalSets) != 14 {
		t.Fatalf("expected all 14 official SKH sets present, got %d", len(rep.GlobalSets))
	}

	var foundSGK, foundDaimao, foundKirin bool
	for _, s := range rep.GlobalSets {
		switch s.SetName {
		case "Songoku":
			foundSGK = true
			if s.HaveSlots != 5 || len(s.MissingSlots) != 0 || s.CompleteSets != 1 || s.Code != "1-1-1-1-1" || s.ShortName != "SGK" {
				t.Fatalf("Songoku set mismatch: %+v", s)
			}
		case "Pikkoro Daimao":
			foundDaimao = true
			if s.Planet != "NM" || s.Code != "1-0-0-0-0" {
				t.Fatalf("Daimao set mismatch: %+v", s)
			}
		case "Kirin":
			foundKirin = true
			if s.Code != "0-0-0-0-0" || s.TotalItems != 0 {
				t.Fatalf("expected empty Kirin set as 0-0-0-0-0, got %+v", s)
			}
		}
	}
	if !foundSGK || !foundDaimao || !foundKirin {
		t.Fatalf("missing expected sets: sgk=%v daimao=%v kirin=%v", foundSGK, foundDaimao, foundKirin)
	}
}

func TestOfficialNewSKHSetsAndZeroMatrix(t *testing.T) {
	// Kiểm tra chính tả thật từ NRitem0 Teamobi
	cases := map[string]string{
		"Set Thên Xin Hăng":          "Thiên Xin Hăng",
		"Set Cađic M":                "Cadic M",
		"Set Ca Đíc":                 "Cadic",
		"Set Nail chiến binh Namếc":  "Nail Chiến Binh Namếc",
		"Set Thần Vũ Trụ Kaio":       "Thần Vũ Trụ Kaio",
		"Set Gohan":                  "Gohan",
		"Set Thần hủy diệt Champa":   "Thần Hủy Diệt Champa",
	}
	for raw, want := range cases {
		if got := NormalizeSetName(raw); got != want {
			t.Fatalf("NormalizeSetName(%q) = %q, want %q", raw, got, want)
		}
	}

	rows := []model.AccountRow{
		{
			Account:    model.Account{ID: "a1", Username: "acc_td"},
			ServerName: "Vũ trụ 7",
			Runtime: model.AccountRuntime{
				Planet: "TĐ",
				Loot: []model.LootItem{
					{TemplateID: 0, Name: "Áo vải 3 lỗ", Type: 0, IsSKH: true, SetName: "Set Sôngôku", Location: "box"},
					{TemplateID: 27, Name: "Giày vải đen", Type: 3, IsSKH: true, SetName: "Set Sôngôku", Location: "bag"},
					{TemplateID: 21, Name: "Găng vải", Type: 2, IsSKH: true, SetName: "Set Thần Vũ Trụ Kaio", Location: "bag"},
				},
			},
		},
		{
			Account:    model.Account{ID: "a2", Username: "acc_xd"},
			ServerName: "Vũ trụ 7",
			Runtime: model.AccountRuntime{
				Planet: "XD",
				Loot: []model.LootItem{
					{TemplateID: 0, Name: "Áo vải thô", Type: 0, IsSKH: true, SetName: "Set Cađic M", Location: "box"},
				},
			},
		},
	}

	rep := BuildReport(rows)
	if len(rep.GlobalSets) != 14 {
		t.Fatalf("expected 14 sets always, got %d", len(rep.GlobalSets))
	}

	for _, acc := range rep.Accounts {
		if acc.Username == "acc_td" {
			// 4 bộ TĐ + 2 bộ ALL (Gohan, Champa) = 6 bộ
			if len(acc.Sets) != 6 {
				t.Fatalf("expected 6 sets (4 TĐ + 2 ALL) for acc_td, got %d", len(acc.Sets))
			}
			if acc.Sets[0].ShortName != "SGK" || acc.Sets[0].Code != "1-0-0-1-0" {
				t.Fatalf("expected SGK 1-0-0-1-0, got %s %s", acc.Sets[0].ShortName, acc.Sets[0].Code)
			}
			if acc.Sets[3].ShortName != "Kaio" || acc.Sets[3].Code != "0-0-1-0-0" {
				t.Fatalf("expected Kaio 0-0-1-0-0, got %s %s", acc.Sets[3].ShortName, acc.Sets[3].Code)
			}
			if acc.Sets[4].ShortName != "Gohan" || acc.Sets[4].Code != "0-0-0-0-0" {
				t.Fatalf("expected Gohan 0-0-0-0-0, got %s %s", acc.Sets[4].ShortName, acc.Sets[4].Code)
			}
		}
		if acc.Username == "acc_xd" {
			var foundCadicM, foundCadic bool
			for _, s := range acc.Sets {
				if s.SetName == "Cadic M" && s.Code == "1-0-0-0-0" {
					foundCadicM = true
				}
				if s.SetName == "Cadic" && s.Code == "0-0-0-0-0" {
					foundCadic = true
				}
			}
			if !foundCadicM || !foundCadic {
				t.Fatalf("Cadic M must not collide with Cadic: %+v", acc.Sets)
			}
		}
	}
}
