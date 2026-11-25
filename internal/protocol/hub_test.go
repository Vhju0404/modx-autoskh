package protocol

import (
	"bufio"
	"context"
	"encoding/json"
	"fmt"
	"net"
	"sync"
	"testing"
	"time"
)

func TestHubConcurrentNDJSONNoCorruption(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	var mu sync.Mutex
	received := make(map[string]int)

	// Tìm port trống ngẫu nhiên
	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	port := ln.Addr().(*net.TCPAddr).Port
	_ = ln.Close()

	hub := NewHub(port, Handlers{
		OnTelemetry: func(accountID string, tp TelemetryPayload) {
			mu.Lock()
			received[accountID]++
			mu.Unlock()
		},
	})
	if err := hub.Start(ctx); err != nil {
		t.Fatal(err)
	}
	defer hub.Close()

	// Mô phỏng 10 tab game bắn song song 20 gói NDJSON cùng lúc + nhận lệnh nóng
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func(idx int) {
			defer wg.Done()
			accID := fmt.Sprintf("acc_%d", idx)
			conn, err := net.Dial("tcp", fmt.Sprintf("127.0.0.1:%d", port))
			if err != nil {
				t.Errorf("dial failed: %v", err)
				return
			}
			defer conn.Close()

			hello := fmt.Sprintf(`{"type":"hello","account_id":"%s","username":"u%d","pid":%d}`+"\n", accID, idx, 1000+idx)
			_, _ = conn.Write([]byte(hello))

			for j := 0; j < 20; j++ {
				line := fmt.Sprintf(`{"type":"telemetry","account_id":"%s","payload":{"map_id":1,"zone_id":%d,"fsm_state":"FARM"}}`+"\n", accID, j)
				_, _ = conn.Write([]byte(line))
			}

			// Kiểm tra nhận lệnh ngược lại từ server
			time.Sleep(80 * time.Millisecond)
			_ = hub.SendTo(accID, ServerCmd{Cmd: "pause"})
			_ = conn.SetReadDeadline(time.Now().Add(2 * time.Second))
			rd := bufio.NewReader(conn)
			cmdLine, err := rd.ReadString('\n')
			if err != nil {
				t.Errorf("read cmd failed: %v", err)
				return
			}
			var got ServerCmd
			if err := json.Unmarshal([]byte(cmdLine), &got); err != nil || got.Cmd != "pause" {
				t.Errorf("unexpected cmd: %q err=%v", cmdLine, err)
			}
		}(i)
	}
	wg.Wait()
	time.Sleep(100 * time.Millisecond)

	mu.Lock()
	defer mu.Unlock()
	for i := 0; i < 10; i++ {
		accID := fmt.Sprintf("acc_%d", i)
		if received[accID] != 20 {
			t.Fatalf("acc %s expected 20 telemetry packets, got %d", accID, received[accID])
		}
	}
}
