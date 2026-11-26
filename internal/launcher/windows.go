//go:build windows

package launcher

import (
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"sync"
	"syscall"
	"time"
	"unsafe"

	"hteam/modx-autoskh/internal/model"
)

var (
	kernel32                  = syscall.NewLazyDLL("kernel32.dll")
	procCreateMutexW          = kernel32.NewProc("CreateMutexW")
	psapi                     = syscall.NewLazyDLL("psapi.dll")
	procGetProcessMemoryInfo  = psapi.NewProc("GetProcessMemoryInfo")
	procEmptyWorkingSet       = psapi.NewProc("EmptyWorkingSet")
	user32                    = syscall.NewLazyDLL("user32.dll")
	procEnumWindows           = user32.NewProc("EnumWindows")
	procGetWindowThreadProcID = user32.NewProc("GetWindowThreadProcessId")
	procIsWindowVisible       = user32.NewProc("IsWindowVisible")
	procGetWindowTextW        = user32.NewProc("GetWindowTextW")
	procGetClassNameW         = user32.NewProc("GetClassNameW")
	procSetWindowTextW        = user32.NewProc("SetWindowTextW")
	procMoveWindow            = user32.NewProc("MoveWindow")
	procGetWindowRect         = user32.NewProc("GetWindowRect")
	procSetForegroundWindow   = user32.NewProc("SetForegroundWindow")
	procShowWindow            = user32.NewProc("ShowWindow")
	procSystemParametersInfoW = user32.NewProc("SystemParametersInfoW")
	procIsHungAppWindow       = user32.NewProc("IsHungAppWindow")
	procPostMessageW          = user32.NewProc("PostMessageW")
)

const (
	swRestore                     = 9
	spiGetWorkArea                = 0x0030
	wmClose                       = 0x0010
	processQueryLimitedInfo       = 0x1000
	processQueryInformation       = 0x0400
	processVMRead                 = 0x0010
	processSetQuota               = 0x0100
	processTerminate              = 0x0001
	errorAlreadyExists            = 183
)

type rect struct {
	Left, Top, Right, Bottom int32
}

type processMemoryCounters struct {
	CB                         uint32
	PageFaultCount             uint32
	PeakWorkingSetSize         uintptr
	WorkingSetSize             uintptr
	QuotaPeakPagedPoolUsage    uintptr
	QuotaPagedPoolUsage        uintptr
	QuotaPeakNonPagedPoolUsage uintptr
	QuotaNonPagedPoolUsage     uintptr
	PagefileUsage              uintptr
	PeakPagefileUsage          uintptr
}

// LaunchSpec tham số đầy đủ để khởi chạy 1 tab game sạch.
type LaunchSpec struct {
	GameExePath  string
	RuntimeDir   string
	Account      model.Account
	Server       model.ServerConfig
	Profile      model.FarmProfile
	ProxyStr     string // "home" hoặc "host:port:user:pass"
	ProxyType    string // "http" | "socks5"
	SocketPort   int
	WindowWidth  int
	WindowHeight int
	Headless     bool
	PrivacyMode  bool
	DisplayAlias string
	TitlePrefix  string
}

// LaunchResult thông tin tiến trình vừa mở.
type LaunchResult struct {
	PID   int
	HWND  uintptr
	Title string
}

// AcquireSingleInstance giữ mutex đặt tên để tránh mở trùng 2 bản QLTK đụng cổng.
func AcquireSingleInstance(name string) (uintptr, bool) {
	p, err := syscall.UTF16PtrFromString(name)
	if err != nil {
		return 0, true
	}
	h, _, lastErr := procCreateMutexW.Call(0, 0, uintptr(unsafe.Pointer(p)))
	if h == 0 {
		return 0, true
	}
	if errno, ok := lastErr.(syscall.Errno); ok && errno == errorAlreadyExists {
		_ = syscall.CloseHandle(syscall.Handle(h))
		return 0, false
	}
	return h, true
}

// ReleaseSingleInstance giải phóng mutex khi thoát hoàn toàn.
func ReleaseSingleInstance(h uintptr) {
	if h != 0 {
		_ = syscall.CloseHandle(syscall.Handle(h))
	}
}

// OpenAppWindow mở giao diện QLTK dạng cửa sổ app gọn gàng (Edge/Chrome --app) hoặc trình duyệt mặc định (Win 2012 R2).
func OpenAppWindow(url string) {
	pf := os.Getenv("ProgramFiles")
	pf86 := os.Getenv("ProgramFiles(x86)")
	localApp := os.Getenv("LOCALAPPDATA")

	candidates := []string{
		filepath.Join(pf86, "Microsoft", "Edge", "Application", "msedge.exe"),
		filepath.Join(pf, "Microsoft", "Edge", "Application", "msedge.exe"),
		filepath.Join(pf, "Google", "Chrome", "Application", "chrome.exe"),
		filepath.Join(pf86, "Google", "Chrome", "Application", "chrome.exe"),
		filepath.Join(localApp, "Google", "Chrome", "Application", "chrome.exe"),
		filepath.Join(pf, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
	}
	for _, exe := range candidates {
		if exe == "" {
			continue
		}
		if _, err := os.Stat(exe); err == nil {
			cmd := exec.Command(exe,
				"--app="+url,
				"--window-size=1360,840",
				"--disable-extensions",
			)
			if err := cmd.Start(); err == nil {
				go func() { _ = cmd.Wait() }()
				return
			}
		}
	}
	// Fallback cho VPS Win Server 2012 R2 không có Edge/Chrome
	cmd := exec.Command("rundll32.exe", "url.dll,FileProtocolHandler", url)
	if err := cmd.Start(); err == nil {
		go func() { _ = cmd.Wait() }()
	}
}

// StartGame ghi profile JSON riêng của acc rồi mở Dragonboy_vn_v231.exe (-nolog) và đặt tên cửa sổ.
func StartGame(spec LaunchSpec) (LaunchResult, error) {
	exePath := spec.GameExePath
	if !filepath.IsAbs(exePath) {
		if wd, err := os.Getwd(); err == nil {
			exePath = filepath.Join(wd, exePath)
		}
	}
	if _, err := os.Stat(exePath); err != nil {
		return LaunchResult{}, fmt.Errorf("không tìm thấy file game: %s", exePath)
	}

	// Ghi file cấu hình profile riêng cho acc (tránh xung đột giữa các tab)
	profilePath := filepath.Join(spec.RuntimeDir, fmt.Sprintf("profile_%s.json", sanitizeFile(spec.Account.ID)))
	if b, err := json.Marshal(spec.Profile); err == nil {
		_ = os.WriteFile(profilePath, b, 0o600)
	}

	zoneMode := spec.Account.ZoneMode
	if zoneMode == "" || zoneMode == "profile" {
		zoneMode = spec.Profile.ZoneStrategy
	}
	zoneID := spec.Account.ZoneID
	if spec.Account.ZoneMode == "" || spec.Account.ZoneMode == "profile" {
		zoneID = spec.Profile.FixedZoneID
	}

	privacyArg := "0"
	if spec.PrivacyMode {
		privacyArg = "1"
	}

	args := make([]string, 0, 38)
	if spec.Headless {
		args = append(args, "-batchmode", "-nographics")
	}
	args = append(args,
		"-nolog",
		"--account-id", spec.Account.ID,
		"--username", spec.Account.Username,
		"--password", spec.Account.Password,
		"--server", fmt.Sprintf("%s:%d", spec.Server.Host, spec.Server.Port),
		"--map", strconv.Itoa(spec.Account.MapID),
		"--zone-mode", zoneMode,
		"--zone", strconv.Itoa(zoneID),
		"--cx", strconv.Itoa(spec.Account.CoordX),
		"--cy", strconv.Itoa(spec.Account.CoordY),
		"--proxy", spec.ProxyStr,
		"--proxy-type", spec.ProxyType,
		"--port", strconv.Itoa(spec.SocketPort),
		"--width", strconv.Itoa(spec.WindowWidth),
		"--height", strconv.Itoa(spec.WindowHeight),
		"--title-expiry", strconv.FormatInt(spec.Account.TitleExpiryMs, 10),
		"--privacy", privacyArg,
		"--profile-file", profilePath,
	)
	if spec.Headless {
		args = append(args, "--headless", "1")
	}

	cmd := exec.Command(exePath, args...)
	cmd.Dir = filepath.Dir(exePath)
	if spec.Headless {
		cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	}
	if err := cmd.Start(); err != nil {
		return LaunchResult{}, fmt.Errorf("lỗi mở game: %w", err)
	}
	pid := cmd.Process.Pid
	go func() {
		_ = cmd.Wait()
	}()

	titleName := spec.Account.Username
	if spec.PrivacyMode {
		if strings.TrimSpace(spec.DisplayAlias) != "" {
			titleName = spec.DisplayAlias
		} else {
			titleName = "Acc ***"
		}
	}
	pfx := strings.TrimSpace(spec.TitlePrefix)
	if pfx == "" {
		pfx = "SKH"
	}
	title := fmt.Sprintf("%s | %s | %s", pfx, titleName, spec.Server.Name)
	if spec.Headless {
		return LaunchResult{PID: pid, HWND: 0, Title: title}, nil
	}
	var hwnd uintptr
	deadline := time.Now().Add(10 * time.Second)
	for time.Now().Before(deadline) {
		hwnd = FindMainWindowByPID(pid)
		if hwnd != 0 {
			SetWindowTitle(hwnd, title)
			break
		}
		time.Sleep(150 * time.Millisecond)
	}

	return LaunchResult{PID: pid, HWND: hwnd, Title: title}, nil
}

// IsProcessAlive kiểm tra PID còn đang sống hay đã thoát (tương thích Win 7 / Win 2012 R2 -> Win 11).
func IsProcessAlive(pid int) bool {
	if pid <= 0 {
		return false
	}
	h, err := syscall.OpenProcess(processQueryLimitedInfo, false, uint32(pid))
	if err != nil {
		return false
	}
	defer syscall.CloseHandle(h)
	var code uint32
	if err := syscall.GetExitCodeProcess(h, &code); err != nil {
		return false
	}
	return code == 259 // STILL_ACTIVE
}

// GetProcessMemoryMB đọc WorkingSet RAM thật (MB) của 1 tiến trình game qua psapi.dll (chuẩn Win 2012 R2+).
func GetProcessMemoryMB(pid int) int {
	if pid <= 0 {
		return 0
	}
	h, err := syscall.OpenProcess(processQueryInformation|processVMRead, false, uint32(pid))
	if err != nil {
		h, err = syscall.OpenProcess(processQueryLimitedInfo, false, uint32(pid))
		if err != nil {
			return 0
		}
	}
	defer syscall.CloseHandle(h)
	var pmc processMemoryCounters
	pmc.CB = uint32(unsafe.Sizeof(pmc))
	r, _, _ := procGetProcessMemoryInfo.Call(uintptr(h), uintptr(unsafe.Pointer(&pmc)), uintptr(pmc.CB))
	if r == 0 {
		return 0
	}
	return int(pmc.WorkingSetSize / (1024 * 1024))
}

// TrimProcessWorkingSet gọi EmptyWorkingSet (psapi.dll) thu gọn phần RAM rảnh của tab game.
func TrimProcessWorkingSet(pid int) {
	if pid <= 0 {
		return
	}
	h, err := syscall.OpenProcess(processQueryInformation|processSetQuota, false, uint32(pid))
	if err != nil {
		return
	}
	defer syscall.CloseHandle(h)
	_, _, _ = procEmptyWorkingSet.Call(uintptr(h))
}

// KillProcess diệt tiến trình game an toàn.
func KillProcess(pid int) error {
	if pid <= 0 {
		return nil
	}
	h, err := syscall.OpenProcess(processTerminate, false, uint32(pid))
	if err != nil {
		return nil
	}
	defer syscall.CloseHandle(h)
	return syscall.TerminateProcess(h, 1)
}

// IsWindowHung kiểm tra cửa sổ game có bị treo (Not Responding) không.
func IsWindowHung(hwnd uintptr) bool {
	if hwnd == 0 {
		return false
	}
	r, _, _ := procIsHungAppWindow.Call(hwnd)
	return r != 0
}

var (
	enumWinMu        sync.Mutex
	findWinTargetPID int
	findWinUnity     uintptr
	findWinFallback  uintptr
	findWinCallback  = syscall.NewCallback(enumFindMainWindowProc)
	crashDlgCallback = syscall.NewCallback(enumCloseCrashDialogsProc)
)

func enumFindMainWindowProc(hwnd uintptr, lparam uintptr) uintptr {
	vis, _, _ := procIsWindowVisible.Call(hwnd)
	if vis == 0 {
		return 1
	}
	var wpid uint32
	_, _, _ = procGetWindowThreadProcID.Call(hwnd, uintptr(unsafe.Pointer(&wpid)))
	if int(wpid) != findWinTargetPID {
		return 1
	}
	var clsBuf [64]uint16
	n, _, _ := procGetClassNameW.Call(hwnd, uintptr(unsafe.Pointer(&clsBuf[0])), uintptr(len(clsBuf)))
	cls := ""
	if n > 0 {
		cls = syscall.UTF16ToString(clsBuf[:n])
	}
	if cls == "UnityWndClass" {
		findWinUnity = hwnd
		return 0
	}
	lowCls := strings.ToLower(cls)
	if strings.Contains(lowCls, "uac") || strings.Contains(lowCls, "ime") || strings.Contains(lowCls, "tooltip") {
		return 1
	}
	if findWinFallback == 0 {
		var r rect
		if ok, _, _ := procGetWindowRect.Call(hwnd, uintptr(unsafe.Pointer(&r))); ok != 0 {
			if (r.Right-r.Left) >= 120 && (r.Bottom-r.Top) >= 100 {
				findWinFallback = hwnd
			}
		}
	}
	return 1
}

// FindMainWindowByPID tìm đúng cửa sổ game chính (dùng callback singleton không rò rỉ slot Win32 khi chạy 24/7).
func FindMainWindowByPID(pid int) uintptr {
	if pid <= 0 {
		return 0
	}
	enumWinMu.Lock()
	defer enumWinMu.Unlock()
	findWinTargetPID = pid
	findWinUnity = 0
	findWinFallback = 0
	_, _, _ = procEnumWindows.Call(findWinCallback, 0)
	if findWinUnity != 0 {
		return findWinUnity
	}
	return findWinFallback
}

// SetWindowTitle đổi tiêu đề cửa sổ game.
func SetWindowTitle(hwnd uintptr, title string) {
	if hwnd == 0 {
		return
	}
	p, err := syscall.UTF16PtrFromString(title)
	if err != nil {
		return
	}
	_, _, _ = procSetWindowTextW.Call(hwnd, uintptr(unsafe.Pointer(p)))
}

// FocusWindow khôi phục và đưa cửa sổ game lên trên cùng.
func FocusWindow(hwnd uintptr) {
	if hwnd == 0 {
		return
	}
	_, _, _ = procShowWindow.Call(hwnd, swRestore)
	_, _, _ = procSetForegroundWindow.Call(hwnd)
}

// ArrangeGrid tự xếp lưới tất cả cửa sổ game vừa khít WorkArea màn hình.
func ArrangeGrid(hwnds []uintptr, targetW, targetH int) {
	valid := make([]uintptr, 0, len(hwnds))
	for _, h := range hwnds {
		if h != 0 {
			vis, _, _ := procIsWindowVisible.Call(h)
			if vis != 0 {
				valid = append(valid, h)
			}
		}
	}
	if len(valid) == 0 {
		return
	}
	sort.Slice(valid, func(i, j int) bool { return valid[i] < valid[j] })

	var wa rect
	_, _, _ = procSystemParametersInfoW.Call(spiGetWorkArea, 0, uintptr(unsafe.Pointer(&wa)), 0)
	areaW := int(wa.Right - wa.Left)
	if areaW <= 0 {
		areaW = 1920
	}

	x := int(wa.Left)
	y := int(wa.Top)
	rowMaxH := 0

	for _, h := range valid {
		w := targetW
		ht := targetH
		var r rect
		if ok, _, _ := procGetWindowRect.Call(h, uintptr(unsafe.Pointer(&r))); ok != 0 {
			rw := int(r.Right - r.Left)
			rh := int(r.Bottom - r.Top)
			if rw > 120 && rh > 100 {
				w = rw
				ht = rh
			}
		}
		if x > int(wa.Left) && x+w > int(wa.Right) {
			x = int(wa.Left)
			y += rowMaxH
			rowMaxH = 0
		}
		_, _, _ = procMoveWindow.Call(h, uintptr(x), uintptr(y), uintptr(w), uintptr(ht), 1)
		x += w
		if ht > rowMaxH {
			rowMaxH = ht
		}
	}
}

func enumCloseCrashDialogsProc(hwnd uintptr, lparam uintptr) uintptr {
	vis, _, _ := procIsWindowVisible.Call(hwnd)
	if vis == 0 {
		return 1
	}
	var buf [256]uint16
	n, _, _ := procGetWindowTextW.Call(hwnd, uintptr(unsafe.Pointer(&buf[0])), uintptr(len(buf)))
	if n == 0 {
		return 1
	}
	title := strings.ToLower(syscall.UTF16ToString(buf[:n]))
	if strings.Contains(title, "unitycrashhandler") ||
		strings.Contains(title, "oops!") ||
		strings.Contains(title, "fatal error") {
		_, _, _ = procPostMessageW.Call(hwnd, wmClose, 0, 0)
		return 1
	}
	if strings.Contains(title, "dragonboy_vn_v231.exe") {
		var clsBuf [64]uint16
		cn, _, _ := procGetClassNameW.Call(hwnd, uintptr(unsafe.Pointer(&clsBuf[0])), uintptr(len(clsBuf)))
		cls := ""
		if cn > 0 {
			cls = syscall.UTF16ToString(clsBuf[:cn])
		}
		if cls == "#32770" || strings.Contains(title, "application error") || strings.Contains(title, "stopped working") {
			_, _, _ = procPostMessageW.Call(hwnd, wmClose, 0, 0)
		}
	}
	return 1
}

// CloseCrashDialogs đóng popup crash Unity ("Oops!", "Fatal", "UnityCrashHandler", hộp thoại lỗi #32770).
func CloseCrashDialogs() {
	enumWinMu.Lock()
	defer enumWinMu.Unlock()
	_, _, _ = procEnumWindows.Call(crashDlgCallback, 0)
}

func sanitizeFile(s string) string {
	var b strings.Builder
	for _, r := range s {
		if (r >= 'a' && r <= 'z') || (r >= 'A' && r <= 'Z') || (r >= '0' && r <= '9') || r == '_' || r == '-' {
			b.WriteRune(r)
		} else {
			b.WriteByte('_')
		}
	}
	return b.String()
}
