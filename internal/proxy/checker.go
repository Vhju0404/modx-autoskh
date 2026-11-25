package proxy

import (
	"bufio"
	"context"
	"encoding/base64"
	"errors"
	"fmt"
	"io"
	"net"
	"strconv"
	"strings"
	"sync"
	"time"

	"hteam/modx-autoskh/internal/model"
)

// ParseLine đọc 1 dòng proxy hỗ trợ MỌI định dạng phổ biến mà người dùng hay dán:
//   - ip:port
//   - ip:port:user:pass  hoặc  ip|port|user|pass  hoặc  ip;port;user;pass  hoặc  ip port user pass
//   - user:pass:ip:port  (tự nhận diện khi người dùng dán ngược!)
//   - user:pass@ip:port  hoặc  ip:port@user:pass
//   - http://..., https://..., socks5://..., socks5h://..., socks://...
//   - "http ip:port:user:pass"  hoặc  "socks5|ip|port|user|pass"
func ParseLine(raw string, defaultType string) (model.ProxyItem, error) {
	p, _, err := ParseLineDetailed(raw, defaultType)
	return p, err
}

// ParseLineDetailed trả thêm explicitType=true nếu chuỗi nhập vào ghi rõ giao thức (http:// hoặc socks5://).
func ParseLineDetailed(raw string, defaultType string) (model.ProxyItem, bool, error) {
	// Lọc ký tự ẩn zero-width, dấu ngoặc kép thừa, khoảng trắng
	line := strings.TrimSpace(strings.Map(func(r rune) rune {
		if r == '\u200b' || r == '\ufeff' || r == '\r' || r == '"' || r == '\'' {
			return -1
		}
		return r
	}, raw))
	if line == "" || strings.HasPrefix(line, "#") {
		return model.ProxyItem{}, false, errors.New("dòng trống")
	}

	pType := strings.ToLower(strings.TrimSpace(defaultType))
	if pType != "socks5" {
		pType = "http"
	}
	explicitType := false

	work := strings.TrimRight(line, "/")
	lowWork := strings.ToLower(work)
	switch {
	case strings.HasPrefix(lowWork, "socks5h://"):
		pType = "socks5"
		explicitType = true
		work = work[len("socks5h://"):]
	case strings.HasPrefix(lowWork, "socks5://"):
		pType = "socks5"
		explicitType = true
		work = work[len("socks5://"):]
	case strings.HasPrefix(lowWork, "socks://"):
		pType = "socks5"
		explicitType = true
		work = work[len("socks://"):]
	case strings.HasPrefix(lowWork, "https://"):
		pType = "http"
		explicitType = true
		work = work[len("https://"):]
	case strings.HasPrefix(lowWork, "http://"):
		pType = "http"
		explicitType = true
		work = work[len("http://"):]
	case strings.HasPrefix(lowWork, "socks5 ") || strings.HasPrefix(lowWork, "socks5|") || strings.HasPrefix(lowWork, "socks5\t"):
		pType = "socks5"
		explicitType = true
		work = strings.TrimSpace(work[len("socks5")+1:])
	case strings.HasPrefix(lowWork, "http ") || strings.HasPrefix(lowWork, "http|") || strings.HasPrefix(lowWork, "http\t"):
		pType = "http"
		explicitType = true
		work = strings.TrimSpace(work[len("http")+1:])
	}

	var host, user, pass string
	var port int

	if strings.Contains(work, "@") {
		// Hỗ trợ cả user:pass@host:port LẪN host:port@user:pass (dùng LastIndex đề phòng mật khẩu chứa '@')
		at := strings.LastIndex(work, "@")
		left := strings.TrimSpace(work[:at])
		right := strings.TrimSpace(work[at+1:])

		if h, p, ok := splitHostPort(right); ok {
			host, port = h, p
			user, pass = splitCred(left)
		} else if h, p, ok := splitHostPort(left); ok {
			host, port = h, p
			user, pass = splitCred(right)
		} else {
			return model.ProxyItem{}, explicitType, fmt.Errorf("sai định dạng host:port trong %s", line)
		}
	} else {
		// Hỗ trợ phân tách bằng :, |, ,, ;, tab hoặc khoảng trắng
		fields := strings.FieldsFunc(work, func(r rune) bool {
			return r == ':' || r == '|' || r == ',' || r == ';' || r == ' ' || r == '\t'
		})
		// Nếu token đầu là "http"/"https"/"socks5" do nối bằng ':' (vd socks5:1.2.3.4:1080)
		if len(fields) >= 3 {
			f0 := strings.ToLower(fields[0])
			if f0 == "socks5" || f0 == "socks" {
				pType = "socks5"
				explicitType = true
				fields = fields[1:]
			} else if f0 == "http" || f0 == "https" {
				pType = "http"
				explicitType = true
				fields = fields[1:]
			}
		}

		if len(fields) < 2 {
			return model.ProxyItem{}, explicitType, fmt.Errorf("sai định dạng proxy: %s", line)
		}

		// Kiểm tra dạng chuẩn host:port[:user[:pass]] trước
		if p, ok := parseValidPort(fields[1]); ok && looksLikeHost(fields[0]) {
			host = strings.TrimSpace(fields[0])
			port = p
			if len(fields) >= 3 {
				user = strings.TrimSpace(fields[2])
			}
			if len(fields) >= 4 {
				pass = strings.TrimSpace(strings.Join(fields[3:], ":"))
			}
		} else if len(fields) >= 4 {
			// Hỗ trợ người dùng dán ngược: user:pass:host:port
			if p, ok := parseValidPort(fields[len(fields)-1]); ok && looksLikeHost(fields[len(fields)-2]) {
				host = strings.TrimSpace(fields[len(fields)-2])
				port = p
				user = strings.TrimSpace(fields[0])
				pass = strings.TrimSpace(strings.Join(fields[1:len(fields)-2], ":"))
			} else {
				return model.ProxyItem{}, explicitType, fmt.Errorf("port hoặc host không hợp lệ: %s", line)
			}
		} else {
			return model.ProxyItem{}, explicitType, fmt.Errorf("port không hợp lệ: %s", line)
		}
	}

	if host == "" || !looksLikeHost(host) {
		return model.ProxyItem{}, explicitType, errors.New("host/IP proxy không hợp lệ")
	}

	item := model.ProxyItem{
		Raw:      line,
		Type:     pType,
		Host:     host,
		Port:     port,
		Username: user,
		Password: pass,
		Status:   "unknown",
	}
	return item, explicitType, nil
}

// NormalizeRaw chuyển ProxyItem về dạng chuẩn ngắn gọn có ghi rõ giao thức để lưu & chạy ổn định.
func NormalizeRaw(p model.ProxyItem) string {
	if p.Host == "" || p.Port <= 0 {
		return ""
	}
	scheme := "http"
	if strings.ToLower(p.Type) == "socks5" {
		scheme = "socks5"
	}
	if p.Username != "" {
		return fmt.Sprintf("%s://%s:%s@%s:%d", scheme, p.Username, p.Password, p.Host, p.Port)
	}
	return fmt.Sprintf("%s://%s:%d", scheme, p.Host, p.Port)
}

func parseValidPort(s string) (int, bool) {
	p, err := strconv.Atoi(strings.TrimSpace(s))
	if err != nil || p <= 0 || p > 65535 {
		return 0, false
	}
	return p, true
}

func splitHostPort(s string) (string, int, bool) {
	parts := strings.FieldsFunc(s, func(r rune) bool {
		return r == ':' || r == '|' || r == ',' || r == ';' || r == ' '
	})
	if len(parts) != 2 {
		return "", 0, false
	}
	p, ok := parseValidPort(parts[1])
	if !ok || !looksLikeHost(parts[0]) {
		return "", 0, false
	}
	return strings.TrimSpace(parts[0]), p, true
}

func splitCred(s string) (string, string) {
	if idx := strings.IndexAny(s, ":|,;"); idx >= 0 {
		return strings.TrimSpace(s[:idx]), strings.TrimSpace(s[idx+1:])
	}
	return strings.TrimSpace(s), ""
}

func looksLikeHost(h string) bool {
	h = strings.TrimSpace(h)
	if h == "" || strings.ContainsAny(h, " \t/\\") {
		return false
	}
	if ip := net.ParseIP(h); ip != nil {
		return true
	}
	return strings.Contains(h, ".") || strings.EqualFold(h, "localhost")
}

// CheckOneAuto kiểm tra proxy; nếu chuỗi nhập không ghi rõ giao thức, tự thử cả HTTP và SOCKS5!
func CheckOneAuto(ctx context.Context, raw string, targetHost string, targetPort int, timeout time.Duration) (model.ProxyItem, bool, int64, error) {
	p, explicit, err := ParseLineDetailed(raw, "http")
	if err != nil {
		return model.ProxyItem{}, false, 0, err
	}
	ok, ping, checkErr := CheckOne(ctx, p, targetHost, targetPort, timeout)
	if ok {
		p.Status = "live"
		p.PingMs = ping
		p.LastCheckAt = model.NowUnixMs()
		return p, true, ping, nil
	}
	if !explicit {
		alt := p
		if alt.Type == "http" {
			alt.Type = "socks5"
		} else {
			alt.Type = "http"
		}
		if ok2, ping2, err2 := CheckOne(ctx, alt, targetHost, targetPort, timeout); ok2 {
			alt.Status = "live"
			alt.PingMs = ping2
			alt.LastCheckAt = model.NowUnixMs()
			return alt, true, ping2, nil
		} else if checkErr == nil {
			checkErr = err2
		}
	}
	p.Status = "die"
	p.PingMs = 0
	p.LastCheckAt = model.NowUnixMs()
	return p, false, 0, checkErr
}

// CheckOne kiểm tra proxy HTTP CONNECT hoặc SOCKS5 thật tới targetHost:targetPort và đo ping (ms).
func CheckOne(ctx context.Context, p model.ProxyItem, targetHost string, targetPort int, timeout time.Duration) (bool, int64, error) {
	if timeout <= 0 {
		timeout = 6 * time.Second
	}
	start := time.Now()
	dialer := net.Dialer{Timeout: timeout}
	addr := net.JoinHostPort(p.Host, strconv.Itoa(p.Port))
	conn, err := dialer.DialContext(ctx, "tcp", addr)
	if err != nil {
		return false, 0, err
	}
	defer conn.Close()
	_ = conn.SetDeadline(time.Now().Add(timeout))

	if strings.ToLower(p.Type) == "socks5" {
		if err := handshakeSOCKS5(conn, p, targetHost, targetPort); err != nil {
			return false, 0, err
		}
		return true, time.Since(start).Milliseconds(), nil
	}

	if err := handshakeHTTPConnect(conn, p, targetHost, targetPort); err != nil {
		return false, 0, err
	}
	return true, time.Since(start).Milliseconds(), nil
}

func handshakeHTTPConnect(conn net.Conn, p model.ProxyItem, targetHost string, targetPort int) error {
	var b strings.Builder
	fmt.Fprintf(&b, "CONNECT %s:%d HTTP/1.1\r\nHost: %s:%d\r\n", targetHost, targetPort, targetHost, targetPort)
	if p.Username != "" {
		auth := base64.StdEncoding.EncodeToString([]byte(p.Username + ":" + p.Password))
		fmt.Fprintf(&b, "Proxy-Authorization: Basic %s\r\n", auth)
	}
	b.WriteString("\r\n")

	if _, err := conn.Write([]byte(b.String())); err != nil {
		return err
	}
	reader := bufio.NewReader(conn)
	statusLine, err := reader.ReadString('\n')
	if err != nil {
		return err
	}
	if !strings.Contains(statusLine, " 200 ") {
		return fmt.Errorf("http proxy từ chối: %s", strings.TrimSpace(statusLine))
	}
	return nil
}

// handshakeSOCKS5 triển khai chuẩn RFC 1928 (cấp phát động đúng độ dài domain, không bị tràn mảng như bản C# cũ).
func handshakeSOCKS5(conn net.Conn, p model.ProxyItem, targetHost string, targetPort int) error {
	hasAuth := p.Username != ""
	var greet []byte
	if hasAuth {
		greet = []byte{0x05, 0x02, 0x00, 0x02}
	} else {
		greet = []byte{0x05, 0x01, 0x00}
	}
	if _, err := conn.Write(greet); err != nil {
		return err
	}

	resp := make([]byte, 2)
	if _, err := io.ReadFull(conn, resp); err != nil {
		return err
	}
	if resp[0] != 0x05 {
		return errors.New("không phải SOCKS5")
	}
	if resp[1] == 0x02 {
		if !hasAuth {
			return errors.New("proxy yêu cầu user/pass")
		}
		if len(p.Username) > 255 || len(p.Password) > 255 {
			return errors.New("user/pass quá dài")
		}
		authReq := make([]byte, 0, 3+len(p.Username)+len(p.Password))
		authReq = append(authReq, 0x01, byte(len(p.Username)))
		authReq = append(authReq, []byte(p.Username)...)
		authReq = append(authReq, byte(len(p.Password)))
		authReq = append(authReq, []byte(p.Password)...)
		if _, err := conn.Write(authReq); err != nil {
			return err
		}
		authResp := make([]byte, 2)
		if _, err := io.ReadFull(conn, authResp); err != nil {
			return err
		}
		if authResp[1] != 0x00 {
			return errors.New("sai mật khẩu SOCKS5")
		}
	} else if resp[1] != 0x00 {
		return fmt.Errorf("SOCKS5 method bị từ chối: 0x%02x", resp[1])
	}

	// CONNECT request (dynamic buffer for domain length)
	if len(targetHost) > 255 {
		return errors.New("domain quá dài")
	}
	req := make([]byte, 0, 7+len(targetHost))
	req = append(req, 0x05, 0x01, 0x00, 0x03, byte(len(targetHost)))
	req = append(req, []byte(targetHost)...)
	req = append(req, byte(targetPort>>8), byte(targetPort&0xFF))
	if _, err := conn.Write(req); err != nil {
		return err
	}

	hdr := make([]byte, 4)
	if _, err := io.ReadFull(conn, hdr); err != nil {
		return err
	}
	if hdr[0] != 0x05 || hdr[1] != 0x00 {
		return fmt.Errorf("SOCKS5 CONNECT thất bại: code=%d", hdr[1])
	}
	// Đọc nốt phần bind address + port tùy theo atyp
	var restLen int
	switch hdr[3] {
	case 0x01:
		restLen = 4 + 2
	case 0x03:
		lbuf := make([]byte, 1)
		if _, err := io.ReadFull(conn, lbuf); err != nil {
			return err
		}
		restLen = int(lbuf[0]) + 2
	case 0x04:
		restLen = 16 + 2
	}
	if restLen > 0 {
		discard := make([]byte, restLen)
		_, _ = io.ReadFull(conn, discard)
	}
	return nil
}

// CheckBatch kiểm tra song song danh sách proxy với worker pool giới hạn concurrency.
func CheckBatch(ctx context.Context, list []model.ProxyItem, targetHost string, targetPort int, concurrency int) []model.ProxyItem {
	if concurrency <= 0 {
		concurrency = 16
	}
	out := make([]model.ProxyItem, len(list))
	copy(out, list)

	jobs := make(chan int, len(out))
	for i := range out {
		jobs <- i
	}
	close(jobs)

	var wg sync.WaitGroup
	workers := concurrency
	if workers > len(out) {
		workers = len(out)
	}
	for w := 0; w < workers; w++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			for idx := range jobs {
				ok, ping, _ := CheckOne(ctx, out[idx], targetHost, targetPort, 6*time.Second)
				out[idx].LastCheckAt = model.NowUnixMs()
				if ok {
					out[idx].Status = "live"
					out[idx].PingMs = ping
				} else {
					out[idx].Status = "die"
					out[idx].PingMs = 0
				}
			}
		}()
	}
	wg.Wait()
	return out
}

// FormatForClient trả chuỗi chuẩn truyền cho client game C#: "host:port:user:pass".
func FormatForClient(p model.ProxyItem) string {
	if p.Host == "" || p.Port <= 0 {
		return "home"
	}
	if p.Username != "" {
		return fmt.Sprintf("%s:%d:%s:%s", p.Host, p.Port, p.Username, p.Password)
	}
	return fmt.Sprintf("%s:%d", p.Host, p.Port)
}
