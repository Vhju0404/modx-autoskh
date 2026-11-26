package manager

import (
	"net"
	"testing"
	"time"

	"hteam/modx-autoskh/internal/model"
)

func TestIsInAnyTimeWindowMultiShiftAndOvernight(t *testing.T) {
	shifts := []model.TimeWindow{
		{Start: "04:30", End: "12:30"},
		{Start: "16:30", End: "00:30"},
	}

	at := func(h, m int) time.Time {
		return time.Date(2026, 5, 10, h, m, 0, 0, time.Local)
	}

	cases := []struct {
		h, m int
		want bool
	}{
		{4, 29, false},
		{4, 30, true},
		{10, 0, true},
		{12, 29, true},
		{12, 30, false},
		{14, 15, false},
		{16, 29, false},
		{16, 30, true},
		{23, 59, true},
		{0, 0, true},
		{0, 29, true},
		{0, 30, false},
		{3, 15, false},
	}

	for _, tc := range cases {
		got := IsInAnyTimeWindow(at(tc.h, tc.m), shifts)
		if got != tc.want {
			t.Errorf("at %02d:%02d: got %v, want %v", tc.h, tc.m, got, tc.want)
		}
	}

	// Không có ca hợp lệ nào -> fallback cho phép chạy để không khóa oan acc
	if !IsInAnyTimeWindow(at(14, 0), nil) {
		t.Error("expected empty shift list to allow running")
	}
	if !IsInAnyTimeWindow(at(14, 0), []model.TimeWindow{{Start: "bad", End: "time"}}) {
		t.Error("expected invalid shift list to allow running")
	}
}

func TestProbeNROHandshake(t *testing.T) {
	// 1. Mock server NRO hợp lệ (nhận cmd -27 [0xE5,0,0] và trả key reply)
	lnOK, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen ok server: %v", err)
	}
	defer lnOK.Close()
	go func() {
		conn, err := lnOK.Accept()
		if err != nil {
			return
		}
		defer conn.Close()
		buf := make([]byte, 3)
		if _, err := conn.Read(buf); err != nil {
			return
		}
		if buf[0] == 0xE5 {
			_, _ = conn.Write([]byte{0xE5, 0x00, 0x05, 0x04, 11, 22, 33, 44})
		}
	}()
	if !ProbeNROHandshake(lnOK.Addr().String(), 2*time.Second) {
		t.Error("expected ProbeNROHandshake to return true for valid NRO -27 reply")
	}

	// 2. Mock server mở cổng nhưng đóng ngay / trả rác (server chưa boot xong)
	lnBad, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen bad server: %v", err)
	}
	defer lnBad.Close()
	go func() {
		conn, err := lnBad.Accept()
		if err != nil {
			return
		}
		_, _ = conn.Write([]byte("HTTP/1.1 503"))
		_ = conn.Close()
	}()
	if ProbeNROHandshake(lnBad.Addr().String(), 2*time.Second) {
		t.Error("expected ProbeNROHandshake to return false for non-NRO reply")
	}
}

func TestCollectNewlyDroppedSKHNoFalseAlertOnStoreAndDetectsSecondCopy(t *testing.T) {
	aoKakarotBag := model.LootItem{TemplateID: 2, Name: "Áo vải", Slot: "Áo", IsSKH: true, SetName: "Kakarot", Stars: 0, Location: "bag"}
	aoKakarotBox := model.LootItem{TemplateID: 2, Name: "Áo vải", Slot: "Áo", IsSKH: true, SetName: "Kakarot", Stars: 0, Location: "box"}
	quanKakarotBag := model.LootItem{TemplateID: 8, Name: "Quần vải", Slot: "Quần", IsSKH: true, SetName: "Kakarot", Stars: 1, Location: "bag"}

	// 1. Khởi tạo lúc mới vào game: đang có sẵn 1 Áo Kakarot trong túi -> baseline = 1
	maxSeen := countSKHBySig([]model.LootItem{aoKakarotBag})
	if len(maxSeen) != 1 {
		t.Fatalf("expected 1 baseline sig, got %d", len(maxSeen))
	}

	// 2. Về nhà cất Áo Kakarot sang rương (giả lập cả trường hợp gói BAG về trước làm trống 1 nhịp rồi gói BOX mới tới)
	if got := collectNewlyDroppedSKH(maxSeen, nil); len(got) != 0 {
		t.Fatalf("expected 0 drops on transient empty tick, got %d", len(got))
	}
	if got := collectNewlyDroppedSKH(maxSeen, []model.LootItem{aoKakarotBox}); len(got) != 0 {
		t.Fatalf("expected 0 false alerts when moving SKH from bag to box, got %d", len(got))
	}

	// 3. Đi farm nhặt thêm cái Áo Kakarot thứ 2 giống hệt (1 trong rương + 1 mới trong túi = tổng 2) -> phải báo đúng 1 món mới!
	gotSecond := collectNewlyDroppedSKH(maxSeen, []model.LootItem{aoKakarotBox, aoKakarotBag})
	if len(gotSecond) != 1 || gotSecond[0].Slot != "Áo" {
		t.Fatalf("expected 1 new drop for 2nd identical SKH item, got %+v", gotSecond)
	}

	// 4. Cất nốt cái thứ 2 vào rương (2 cái trong rương) -> 0 báo giả!
	if got := collectNewlyDroppedSKH(maxSeen, []model.LootItem{aoKakarotBox, aoKakarotBox}); len(got) != 0 {
		t.Fatalf("expected 0 false alerts when storing 2nd item into box, got %d", len(got))
	}

	// 5. Nhặt thêm Quần Kakarot 1 sao mới -> báo đúng 1 món Quần!
	gotPants := collectNewlyDroppedSKH(maxSeen, []model.LootItem{aoKakarotBox, aoKakarotBox, quanKakarotBag})
	if len(gotPants) != 1 || gotPants[0].Slot != "Quần" {
		t.Fatalf("expected 1 new drop for Quần Kakarot, got %+v", gotPants)
	}
}
