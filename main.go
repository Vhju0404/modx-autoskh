package main

import (
	"embed"
	"encoding/json"
	"fmt"
	"io/fs"
	"log"
	"net"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"strings"
	"syscall"
	"time"

	"hteam/modx-autoskh/internal/launcher"
	"hteam/modx-autoskh/internal/store"
)

//go:embed all:frontend
var frontendAssets embed.FS

//go:embed build/windows/icon.ico
var appIconBytes []byte

func readSavedUIPort() int {
	baseDir := resolveBaseDir()
	b, err := os.ReadFile(filepath.Join(baseDir, "data", "settings.json"))
	if err != nil {
		return 20269
	}
	var partial struct {
		UIPort int `json:"ui_port"`
	}
	if err := json.Unmarshal(b, &partial); err == nil && partial.UIPort >= 1024 && partial.UIPort <= 65535 {
		return partial.UIPort
	}
	return 20269
}

func main() {
	minimized := false
	for _, arg := range os.Args[1:] {
		a := strings.ToLower(strings.TrimSpace(arg))
		if a == "--minimized" || a == "-minimized" || a == "--tray" {
			minimized = true
		}
	}

	preferredPort := readSavedUIPort()

	// Single-Instance Mutex: nếu app đã chạy ngầm ở Tray -> mở thẳng lại bảng QLTK thay vì chạy trùng 2 bản!
	hMutex, acquired := launcher.AcquireSingleInstance("Local\\ModXHTeamAutoSKH_Instance")
	if !acquired {
		if !minimized {
			launcher.OpenAppWindow(fmt.Sprintf("http://127.0.0.1:%d", preferredPort))
		}
		return
	}
	defer launcher.ReleaseSingleInstance(hMutex)

	app := NewApp()
	app.startup()

	iconPath := filepath.Join(app.store.RuntimeDir(), "app.ico")
	if len(appIconBytes) > 0 {
		_ = os.WriteFile(iconPath, appIconBytes, 0o600)
	}

	subFS, err := fs.Sub(frontendAssets, "frontend")
	if err != nil {
		log.Fatal(err)
	}

	uiPort := app.store.GetSettings().UIPort
	if uiPort < 1024 || uiPort > 65535 {
		uiPort = 20269
	}

	var ln net.Listener
	actualPort := uiPort
	for p := uiPort; p <= uiPort+20; p++ {
		l, lerr := net.Listen("tcp", fmt.Sprintf("127.0.0.1:%d", p))
		if lerr == nil {
			ln = l
			actualPort = p
			break
		}
	}
	if ln == nil {
		app.shutdown()
		log.Fatalf("không thể mở cổng Web UI quanh 127.0.0.1:%d", uiPort)
	}
	app.activeUIPort = actualPort
	uiURL := fmt.Sprintf("http://127.0.0.1:%d", actualPort)

	httpSrv := &http.Server{
		Handler:           app.HTTPHandler(subFS, appIconBytes),
		ReadHeaderTimeout: 10 * time.Second,
	}
	go func() {
		if err := httpSrv.Serve(ln); err != nil && err != http.ErrServerClosed {
			log.Printf("HTTP UI server dừng: %v", err)
		}
	}()

	// Bắt tín hiệu Ctrl+C / SIGTERM để thoát sạch toàn bộ tab game
	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, os.Interrupt, syscall.SIGTERM)
	go func() {
		<-sigCh
		launcher.RequestTrayQuit()
	}()

	if !minimized {
		go func() {
			time.Sleep(220 * time.Millisecond)
			launcher.OpenAppWindow(uiURL)
		}()
	}

	launcher.RunTrayLoop(launcher.TrayCallbacks{
		Tooltip:     store.TrayTooltipText(actualPort),
		OpenUILabel: store.TrayOpenLabel(),
		IconPath:    iconPath,
		OnOpenUI:  func() { launcher.OpenAppWindow(uiURL) },
		OnStopAll: func() { app.StopAllTabs() },
		OnQuit: func() {
			_ = httpSrv.Close()
			app.shutdown()
		},
	})
}
