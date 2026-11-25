package proxy

import "testing"

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
