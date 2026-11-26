//go:build windows

package launcher

import "testing"

// TestEnumWindowsCallbacksExceed2000Limit chứng minh gọi FindMainWindowByPID & CloseCrashDialogs
// trên 2.500 lần liên tiếp KHÔNG bao giờ chạm trần 2.000 callback của Go runtime trên Windows.
func TestEnumWindowsCallbacksExceed2000Limit(t *testing.T) {
	for i := 0; i < 2500; i++ {
		_ = FindMainWindowByPID(99999999)
	}
	for i := 0; i < 100; i++ {
		CloseCrashDialogs()
	}
}
