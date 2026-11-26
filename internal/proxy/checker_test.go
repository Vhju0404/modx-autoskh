package proxy

import (
	"context"
	"io"
	"net"
	"strconv"
	"strings"
	"testing"
	"time"

	"hteam/modx-autoskh/internal/model"
)

func TestParseLine(t *testing.T) {
	cases := []struct {
		raw      string
		defType  string
		wantType string
		wantHost string
		wantPort int
		wantUser string
		wantPass string
	}{
		{"103.1.2.3:8080:u1:p1", "http", "http", "103.1.2.3", 8080, "u1", "p1"},
		{"103.1.2.4|1080|user|pass", "socks5", "socks5", "103.1.2.4", 1080, "user", "pass"},
		{"socks5://alice:secret@45.5.6.7:1080", "http", "socks5", "45.5.6.7", 1080, "alice", "secret"},
		{"http://user2:pass2@proxy.vn:3128/", "http", "http", "proxy.vn", 3128, "user2", "pass2"},
		{"https://45.5.6.7:8888", "http", "http", "45.5.6.7", 8888, "", ""},
		{"192.168.1.10:3128", "http", "http", "192.168.1.10", 3128, "", ""},
		// Định dạng người dùng hay dán ngược / đa dạng
		{"u99:p99:103.9.8.7:9090", "http", "http", "103.9.8.7", 9090, "u99", "p99"},
		{"103.9.8.7:9090@u88:p88", "http", "http", "103.9.8.7", 9090, "u88", "p88"},
		{"socks5 103.2.3.4 1080 myu myp", "http", "socks5", "103.2.3.4", 1080, "myu", "myp"},
		{"socks5:103.2.3.4:1080:myu:myp", "http", "socks5", "103.2.3.4", 1080, "myu", "myp"},
		{"103.5.6.7;8000;acc;pw", "http", "http", "103.5.6.7", 8000, "acc", "pw"},
		{"socks5h://bob:1234@1.2.3.4:1080", "http", "socks5", "1.2.3.4", 1080, "bob", "1234"},
	}
	for _, tc := range cases {
		got, err := ParseLine(tc.raw, tc.defType)
		if err != nil {
			t.Fatalf("ParseLine(%q) error: %v", tc.raw, err)
		}
		if got.Type != tc.wantType || got.Host != tc.wantHost || got.Port != tc.wantPort || got.Username != tc.wantUser || got.Password != tc.wantPass {
			t.Fatalf("ParseLine(%q) = %+v, want type=%s host=%s port=%d user=%s pass=%s",
				tc.raw, got, tc.wantType, tc.wantHost, tc.wantPort, tc.wantUser, tc.wantPass)
		}
	}
}

func TestDialTargetHTTPDrainsHeadersAndCheckBatchAutoDetectsSOCKS5(t *testing.T) {
	// 1. Mock HTTP CONNECT proxy có trả kèm header phụ trước payload nhị phân 0xE5
	httpLn, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen http proxy: %v", err)
	}
	defer httpLn.Close()
	go func() {
		conn, err := httpLn.Accept()
		if err != nil {
			return
		}
		defer conn.Close()
		buf := make([]byte, 512)
		_, _ = conn.Read(buf)
		// Gửi liền HTTP 200 kèm header phụ VÀ gói nhị phân NRO ngay lập tức trong cùng 1 segment TCP
		_, _ = conn.Write([]byte("HTTP/1.1 200 Connection established\r\nProxy-Agent: ModXTest/1.0\r\n\r\n\xE5\x00\x05"))
	}()

	hHost, hPortStr, _ := net.SplitHostPort(httpLn.Addr().String())
	hPort, _ := strconv.Atoi(hPortStr)
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()

	tunnel, err := DialTarget(ctx, model.ProxyItem{Type: "http", Host: hHost, Port: hPort}, "127.0.0.1", 14445, 2*time.Second)
	if err != nil {
		t.Fatalf("DialTarget HTTP error: %v", err)
	}
	defer tunnel.Close()
	var nroHdr [3]byte
	if _, err := io.ReadFull(tunnel, nroHdr[:]); err != nil {
		t.Fatalf("read NRO bytes after HTTP CONNECT: %v", err)
	}
	if nroHdr[0] != 0xE5 || nroHdr[1] != 0x00 || nroHdr[2] != 0x05 {
		t.Fatalf("expected clean NRO frame [0xE5 0x00 0x05], got %v", nroHdr)
	}

	// 2. Mock SOCKS5 server: người dùng lỡ nhập với Type="http" (không có scheme trong Raw) -> CheckBatch phải tự nhận ra SOCKS5!
	s5Ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen socks5: %v", err)
	}
	defer s5Ln.Close()
	go func() {
		for {
			conn, err := s5Ln.Accept()
			if err != nil {
				return
			}
			go func(c net.Conn) {
				defer c.Close()
				var first [3]byte
				if _, err := io.ReadFull(c, first[:]); err != nil {
					return
				}
				if first[0] != 0x05 {
					// Từ chối nếu gọi nhầm HTTP CONNECT
					_, _ = c.Write([]byte("HTTP/1.1 400 Bad Request\r\n\r\n"))
					return
				}
				_, _ = c.Write([]byte{0x05, 0x00})
				req := make([]byte, 64)
				if _, err := c.Read(req); err != nil {
					return
				}
				_, _ = c.Write([]byte{0x05, 0x00, 0x00, 0x01, 127, 0, 0, 1, 0x38, 0x6D})
			}(conn)
		}
	}()

	sHost, sPortStr, _ := net.SplitHostPort(s5Ln.Addr().String())
	rawLine := sHost + ":" + sPortStr
	parsed, err := ParseLine(rawLine, "http") // giả lập người dùng quên chuyển dropdown sang SOCKS5
	if err != nil {
		t.Fatalf("ParseLine: %v", err)
	}
	checked := CheckBatch(ctx, []model.ProxyItem{parsed}, "127.0.0.1", 14445, 4)
	if len(checked) != 1 || checked[0].Status != "live" || !strings.EqualFold(checked[0].Type, "socks5") {
		t.Fatalf("expected CheckBatch to auto-detect live socks5, got %+v", checked)
	}
}
