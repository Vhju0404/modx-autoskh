//go:build windows

package launcher

import (
	"runtime"
	"sync/atomic"
	"syscall"
	"unsafe"
)

var (
	shell32                = syscall.NewLazyDLL("shell32.dll")
	procShellNotifyIconW   = shell32.NewProc("Shell_NotifyIconW")
	procGetModuleHandleW   = kernel32.NewProc("GetModuleHandleW")
	procRegisterClassExW   = user32.NewProc("RegisterClassExW")
	procCreateWindowExW    = user32.NewProc("CreateWindowExW")
	procDefWindowProcW     = user32.NewProc("DefWindowProcW")
	procGetMessageW        = user32.NewProc("GetMessageW")
	procTranslateMessage   = user32.NewProc("TranslateMessage")
	procDispatchMessageW   = user32.NewProc("DispatchMessageW")
	procPostQuitMessage    = user32.NewProc("PostQuitMessage")
	procDestroyWindow      = user32.NewProc("DestroyWindow")
	procCreatePopupMenu    = user32.NewProc("CreatePopupMenu")
	procAppendMenuW        = user32.NewProc("AppendMenuW")
	procTrackPopupMenu     = user32.NewProc("TrackPopupMenu")
	procDestroyMenu        = user32.NewProc("DestroyMenu")
	procGetCursorPos       = user32.NewProc("GetCursorPos")
	procLoadImageW         = user32.NewProc("LoadImageW")
	procLoadIconW          = user32.NewProc("LoadIconW")
)

const (
	wmDestroy        = 0x0002
	wmCommand        = 0x0111
	wmUser           = 0x0400
	wmTrayCallback   = wmUser + 1
	wmLButtonUp      = 0x0202
	wmLButtonDblClk  = 0x0203
	wmRButtonUp      = 0x0205
	nimAdd           = 0x00000000
	nimDelete        = 0x00000002
	nifMessage       = 0x00000001
	nifIcon          = 0x00000002
	nifTip           = 0x00000004
	mfString         = 0x00000000
	mfSeparator      = 0x00000800
	tpmBottomAlign   = 0x0020
	tpmLeftAlign     = 0x0000
	imageIcon        = 1
	lrLoadFromFile   = 0x00000010
	idiApplication   = 32512
	cmdTrayOpenUI    = 1001
	cmdTrayStopAll   = 1002
	cmdTrayQuit      = 1003
)

type wndClassExW struct {
	cbSize        uint32
	style         uint32
	lpfnWndProc   uintptr
	cbClsExtra    int32
	cbWndExtra    int32
	hInstance     uintptr
	hIcon         uintptr
	hCursor       uintptr
	hbrBackground uintptr
	lpszMenuName  *uint16
	lpszClassName *uint16
	hIconSm       uintptr
}

type point struct {
	X, Y int32
}

type winMsg struct {
	HWnd    uintptr
	Message uint32
	WParam  uintptr
	LParam  uintptr
	Time    uint32
	Pt      point
}

type notifyIconDataW struct {
	cbSize           uint32
	hWnd             uintptr
	uID              uint32
	uFlags           uint32
	uCallbackMessage uint32
	hIcon            uintptr
	szTip            [128]uint16
	dwState          uint32
	dwStateMask      uint32
	szInfo           [256]uint16
	uVersion         uint32
	szInfoTitle      [64]uint16
	dwInfoFlags      uint32
	guidItem         [16]byte
	hBalloonIcon     uintptr
}

// TrayCallbacks sự kiện khi người dùng thao tác trên icon khay hệ thống (System Tray).
type TrayCallbacks struct {
	Tooltip     string
	OpenUILabel string
	IconPath    string
	OnOpenUI    func()
	OnStopAll   func()
	OnQuit      func()
}

var (
	trayHwnd atomic.Uintptr
	trayCb   TrayCallbacks
)

// RequestTrayQuit gửi tín hiệu đóng gọn vòng lặp khay hệ thống từ luồng HTTP/tín hiệu hệ thống.
func RequestTrayQuit() {
	if h := trayHwnd.Load(); h != 0 {
		_, _, _ = procPostMessageW.Call(h, wmClose, 0, 0)
	}
}

// RunTrayLoop ghim icon ở khay hệ thống (pure Win32, chạy chuẩn từ Win 7 / Win Server 2012 R2 -> Win 11).
func RunTrayLoop(cb TrayCallbacks) {
	runtime.LockOSThread()
	defer runtime.UnlockOSThread()

	trayCb = cb
	hInst, _, _ := procGetModuleHandleW.Call(0)
	clsName, _ := syscall.UTF16PtrFromString("ModXHTeamAutoSKH_TrayWnd")

	wndProc := syscall.NewCallback(func(hwnd uintptr, msg uint32, wParam, lParam uintptr) uintptr {
		switch msg {
		case wmTrayCallback:
			switch uint32(lParam) {
			case wmLButtonDblClk, wmLButtonUp:
				if trayCb.OnOpenUI != nil {
					go trayCb.OnOpenUI()
				}
			case wmRButtonUp:
				showTrayPopupMenu(hwnd)
			}
			return 0
		case wmCommand:
			switch uint32(wParam & 0xFFFF) {
			case cmdTrayOpenUI:
				if trayCb.OnOpenUI != nil {
					go trayCb.OnOpenUI()
				}
			case cmdTrayStopAll:
				if trayCb.OnStopAll != nil {
					go trayCb.OnStopAll()
				}
			case cmdTrayQuit:
				_, _, _ = procPostMessageW.Call(hwnd, wmClose, 0, 0)
			}
			return 0
		case wmClose:
			_, _, _ = procDestroyWindow.Call(hwnd)
			return 0
		case wmDestroy:
			_, _, _ = procPostQuitMessage.Call(0)
			return 0
		}
		r, _, _ := procDefWindowProcW.Call(hwnd, uintptr(msg), wParam, lParam)
		return r
	})

	wc := wndClassExW{
		cbSize:        uint32(unsafe.Sizeof(wndClassExW{})),
		lpfnWndProc:   wndProc,
		hInstance:     hInst,
		lpszClassName: clsName,
	}
	_, _, _ = procRegisterClassExW.Call(uintptr(unsafe.Pointer(&wc)))

	hwnd, _, _ := procCreateWindowExW.Call(
		0,
		uintptr(unsafe.Pointer(clsName)),
		uintptr(unsafe.Pointer(clsName)),
		0, 0, 0, 0, 0,
		0, 0, hInst, 0,
	)
	if hwnd == 0 {
		return
	}
	trayHwnd.Store(hwnd)
	defer trayHwnd.Store(0)

	var hIcon uintptr
	if cb.IconPath != "" {
		if p, err := syscall.UTF16PtrFromString(cb.IconPath); err == nil {
			hIcon, _, _ = procLoadImageW.Call(0, uintptr(unsafe.Pointer(p)), imageIcon, 16, 16, lrLoadFromFile)
		}
	}
	if hIcon == 0 {
		hIcon, _, _ = procLoadIconW.Call(0, idiApplication)
	}

	nid := notifyIconDataW{
		cbSize:           uint32(unsafe.Sizeof(notifyIconDataW{})),
		hWnd:             hwnd,
		uID:              1,
		uFlags:           nifMessage | nifIcon | nifTip,
		uCallbackMessage: wmTrayCallback,
		hIcon:            hIcon,
	}
	tip := cb.Tooltip
	if tip == "" {
		tip = "Auto SKH (24/7)"
	}
	if u16, err := syscall.UTF16FromString(tip); err == nil {
		copy(nid.szTip[:len(nid.szTip)-1], u16)
	}
	_, _, _ = procShellNotifyIconW.Call(nimAdd, uintptr(unsafe.Pointer(&nid)))
	defer func() {
		_, _, _ = procShellNotifyIconW.Call(nimDelete, uintptr(unsafe.Pointer(&nid)))
	}()

	var m winMsg
	for {
		ret, _, _ := procGetMessageW.Call(uintptr(unsafe.Pointer(&m)), 0, 0, 0)
		if int32(ret) <= 0 {
			break
		}
		_, _, _ = procTranslateMessage.Call(uintptr(unsafe.Pointer(&m)))
		_, _, _ = procDispatchMessageW.Call(uintptr(unsafe.Pointer(&m)))
	}

	if trayCb.OnQuit != nil {
		trayCb.OnQuit()
	}
}

func showTrayPopupMenu(hwnd uintptr) {
	menu, _, _ := procCreatePopupMenu.Call()
	if menu == 0 {
		return
	}
	defer procDestroyMenu.Call(menu)

	openLabel := trayCb.OpenUILabel
	if openLabel == "" {
		openLabel = "Mở bảng QLTK"
	}
	appendMenuText(menu, cmdTrayOpenUI, openLabel)
	appendMenuText(menu, cmdTrayStopAll, "Dừng toàn bộ tab game")
	_, _, _ = procAppendMenuW.Call(menu, mfSeparator, 0, 0)
	appendMenuText(menu, cmdTrayQuit, "Thoát hoàn toàn")

	var pt point
	_, _, _ = procGetCursorPos.Call(uintptr(unsafe.Pointer(&pt)))
	_, _, _ = procSetForegroundWindow.Call(hwnd)
	_, _, _ = procTrackPopupMenu.Call(menu, tpmBottomAlign|tpmLeftAlign, uintptr(pt.X), uintptr(pt.Y), 0, hwnd, 0)
}

func appendMenuText(menu uintptr, id uintptr, text string) {
	p, err := syscall.UTF16PtrFromString(text)
	if err != nil {
		return
	}
	_, _, _ = procAppendMenuW.Call(menu, mfString, id, uintptr(unsafe.Pointer(p)))
}
