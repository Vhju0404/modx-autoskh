package notify

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"strings"
	"sync"
	"time"
)

type TelegramClient struct {
	mu       sync.Mutex
	lastSent map[string]time.Time
	httpCli  *http.Client
}

func NewTelegramClient() *TelegramClient {
	return &TelegramClient{
		lastSent: make(map[string]time.Time),
		httpCli:  &http.Client{Timeout: 8 * time.Second},
	}
}

// Send gửi tin nhắn HTML tới Telegram Bot, hỗ trợ chống spam theo dedupKey + cooldown.
func (t *TelegramClient) Send(token, chatID, dedupKey string, cooldown time.Duration, htmlMessage string) error {
	token = strings.TrimSpace(token)
	chatID = strings.TrimSpace(chatID)
	if token == "" || chatID == "" {
		return errors.New("chưa cấu hình Bot Token hoặc Chat ID")
	}

	if dedupKey != "" && cooldown > 0 {
		t.mu.Lock()
		if prev, ok := t.lastSent[dedupKey]; ok && time.Since(prev) < cooldown {
			t.mu.Unlock()
			return nil
		}
		t.lastSent[dedupKey] = time.Now()
		t.mu.Unlock()
	}

	payload := map[string]interface{}{
		"chat_id":                  chatID,
		"text":                     htmlMessage,
		"parse_mode":               "HTML",
		"disable_web_page_preview": true,
	}
	body, err := json.Marshal(payload)
	if err != nil {
		return err
	}

	url := fmt.Sprintf("https://api.telegram.org/bot%s/sendMessage", token)
	resp, err := t.httpCli.Post(url, "application/json", bytes.NewReader(body))
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode >= 300 {
		return fmt.Errorf("telegram trả về mã %d", resp.StatusCode)
	}
	return nil
}
