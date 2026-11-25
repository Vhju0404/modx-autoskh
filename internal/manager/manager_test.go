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
