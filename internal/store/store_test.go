package store

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"hteam/modx-autoskh/internal/model"
)

func TestStoreEncryptsPasswordAndSeedsDefaults(t *testing.T) {
	dir := t.TempDir()
	st, err := New(dir)
	if err != nil {
		t.Fatalf("New store failed: %v", err)
	}
	if len(st.GetServers()) < 14 {
		t.Fatalf("expected >=14 official servers, got %d", len(st.GetServers()))
	}
	if len(st.GetProfiles()) < 4 {
		t.Fatalf("expected >=4 default farm profiles, got %d", len(st.GetProfiles()))
	}

	secret := "MySuperSecretPass!99"
	err = st.UpdateAccount(model.Account{
		ID:       "acc_1",
		Username: "player01",
		Password: secret,
		ServerID: "sv1",
	})
	if err != nil {
		t.Fatalf("UpdateAccount failed: %v", err)
	}

	rawBytes, err := os.ReadFile(filepath.Join(dir, "accounts.json"))
	if err != nil {
		t.Fatalf("read accounts.json failed: %v", err)
	}
	if strings.Contains(string(rawBytes), secret) {
		t.Fatalf("plaintext password leaked into accounts.json!")
	}

	// Reload from disk and verify decryption works
	st2, err := New(dir)
	if err != nil {
		t.Fatalf("reload store failed: %v", err)
	}
	acc, ok := st2.GetAccountByID("acc_1")
	if !ok || acc.Password != secret {
		t.Fatalf("expected decrypted password %q, got ok=%v pw=%q", secret, ok, acc.Password)
	}
}

func TestDeleteAccountsCleansRuntimeAndLootHistory(t *testing.T) {
	dir := t.TempDir()
	st, err := New(dir)
	if err != nil {
		t.Fatalf("New store failed: %v", err)
	}

	if err := st.UpdateAccount(model.Account{ID: "acc_old", Username: "old_user", Password: "p1", ServerID: "sv7", ProfileID: "prof_tdlt"}); err != nil {
		t.Fatal(err)
	}
	if err := st.UpdateAccount(model.Account{ID: "acc_keep", Username: "keep_user", Password: "p2", ServerID: "sv7", ProfileID: "prof_tdlt"}); err != nil {
		t.Fatal(err)
	}

	oldRuntime := filepath.Join(st.RuntimeDir(), "profile_acc_old.json")
	keepRuntime := filepath.Join(st.RuntimeDir(), "profile_acc_keep.json")
	orphanRuntime := filepath.Join(st.RuntimeDir(), "profile_acc_ghost.json")
	_ = os.WriteFile(oldRuntime, []byte(`{}`), 0o600)
	_ = os.WriteFile(keepRuntime, []byte(`{}`), 0o600)
	_ = os.WriteFile(orphanRuntime, []byte(`{}`), 0o600)

	_ = st.AppendLootHistory(model.LootHistoryEntry{ID: "l1", AccountID: "acc_old", Username: "old_user"})
	_ = st.AppendLootHistory(model.LootHistoryEntry{ID: "l2", AccountID: "acc_keep", Username: "keep_user"})
	_ = st.AppendLootHistory(model.LootHistoryEntry{ID: "l3", AccountID: "acc_ghost", Username: "ghost_user"})

	if err := st.DeleteAccounts([]string{"acc_old"}); err != nil {
		t.Fatalf("DeleteAccounts failed: %v", err)
	}
	if _, err := os.Stat(oldRuntime); !os.IsNotExist(err) {
		t.Fatalf("expected deleted account runtime profile to be removed")
	}
	if _, err := os.Stat(keepRuntime); err != nil {
		t.Fatalf("expected kept account runtime profile to remain: %v", err)
	}
	for _, h := range st.GetLootHistory() {
		if h.AccountID == "acc_old" {
			t.Fatalf("deleted account loot history leaked after DeleteAccounts")
		}
	}

	// Reload should also prune orphan runtime file & orphan loot history entry
	stReload, err := New(dir)
	if err != nil {
		t.Fatalf("reload failed: %v", err)
	}
	if _, err := os.Stat(orphanRuntime); !os.IsNotExist(err) {
		t.Fatalf("expected orphan runtime file to be pruned on load")
	}
	if got := len(stReload.GetLootHistory()); got != 1 || stReload.GetLootHistory()[0].AccountID != "acc_keep" {
		t.Fatalf("expected only acc_keep in loot history after reload, got %+v", stReload.GetLootHistory())
	}
}
