package store

import (
	"crypto/sha256"
	_ "embed"
	"encoding/base64"
	"encoding/hex"
	"fmt"
	"strings"
	"sync"
)

//go:embed assets/emblem.png
var embeddedEmblemPNG []byte

const (
	brandTitle      = "MOD X"
	brandTeam       = "H TEAM"
	brandFullName   = "MOD X · H TEAM"
	brandSubtitle   = "H TEAM · Auto SKH"
	brandPageTitle  = "MOD X · H TEAM — Auto Up Set Kích Hoạt"
	brandWinPrefix  = "MODX"
	brandTrayTip    = "MOD X · H TEAM — Auto SKH (24/7)"
	brandTrayOpen   = "Mở bảng QLTK (MOD X · H TEAM)"
	brandNotifyHdr  = "MOD X · H TEAM — Auto SKH"
	brandHudHeader  = "MOD X | H TEAM"
	brandSessionKey = "HX9#2026!HTEAM@MODX"
	expectedDigest  = "8e6bf9e49b1fbd8eb987f21fa193f013f10c89d82f48152ee003eeaebbac801f"
)

// LayoutMeta chứa thông tin thương hiệu & trạng thái toàn vẹn của bản phát hành MOD X · H TEAM.
type LayoutMeta struct {
	Title       string `json:"t1"`
	Team        string `json:"t2"`
	FullName    string `json:"full"`
	Subtitle    string `json:"sub"`
	PageTitle   string `json:"pt"`
	GlyphURI    string `json:"glyph"`
	Fingerprint string `json:"fp"`
	Verified    bool   `json:"ok"`
}

var (
	layoutOnce   sync.Once
	cachedLayout LayoutMeta
	layoutValid  bool
)

func initLayoutCache() {
	layoutOnce.Do(func() {
		h := sha256.New()
		h.Write([]byte(brandTitle + "|" + brandTeam + "|" + brandFullName + "|" + brandSessionKey))
		h.Write(embeddedEmblemPNG)
		sum := h.Sum(nil)
		sumHex := hex.EncodeToString(sum)
		layoutValid = strings.EqualFold(sumHex, expectedDigest)

		cachedLayout = LayoutMeta{
			Title:       brandTitle,
			Team:        brandTeam,
			FullName:    brandFullName,
			Subtitle:    brandSubtitle,
			PageTitle:   brandPageTitle,
			GlyphURI:    "data:image/png;base64," + base64.StdEncoding.EncodeToString(embeddedEmblemPNG),
			Fingerprint: strings.ToUpper(sumHex[:12]),
			Verified:    layoutValid,
		}
	})
}

func GetLayoutMeta() LayoutMeta {
	initLayoutCache()
	return cachedLayout
}

func RuntimeIntegrityOK() bool {
	initLayoutCache()
	return layoutValid
}

func WindowTitlePrefix() string {
	return brandWinPrefix
}

func TrayTooltipText(port int) string {
	return fmt.Sprintf("%s · :%d", brandTrayTip, port)
}

func TrayOpenLabel() string {
	return brandTrayOpen
}

func NotifyHeaderText() string {
	return brandNotifyHdr
}

func HudHeaderLabel() string {
	return brandHudHeader
}

// ComputeSessionSig tạo chữ ký bắt tay khớp 1-1 giữa Go launcher và C# Assembly-CSharp.dll.
func ComputeSessionSig(accountID string) string {
	return fnvHex64(brandTitle + ":" + brandTeam + ":" + brandSessionKey + ":C:" + strings.TrimSpace(accountID))
}

// ComputeServerAck tạo mã xác thực ngược từ Go về C# client.
func ComputeServerAck(accountID string) string {
	return fnvHex64(brandTeam + ":" + brandTitle + ":" + brandSessionKey + ":S:" + strings.TrimSpace(accountID))
}

func fnvHex64(s string) string {
	var h uint64 = 14695981039346656037
	for i := 0; i < len(s); i++ {
		h ^= uint64(s[i])
		h *= 1099511628211
	}
	return fmt.Sprintf("%016x", h)
}
