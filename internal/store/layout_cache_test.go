package store

import (
	"strings"
	"testing"
)

func TestLayoutIntegrityAndHandshake(t *testing.T) {
	if !RuntimeIntegrityOK() {
		t.Fatal("expected RuntimeIntegrityOK() == true")
	}
	meta := GetLayoutMeta()
	if !meta.Verified {
		t.Fatal("expected LayoutMeta.Verified == true")
	}
	if meta.Title != "MOD X" || meta.Team != "H TEAM" {
		t.Fatalf("unexpected decoded metadata: %+v", meta)
	}
	if !strings.HasPrefix(meta.GlyphURI, "data:image/png;base64,") || len(meta.GlyphURI) < 2000 {
		t.Fatalf("expected valid embedded PNG data URI, len=%d", len(meta.GlyphURI))
	}
	if len(meta.Fingerprint) != 12 {
		t.Fatalf("expected 12-char fingerprint, got %q", meta.Fingerprint)
	}

	sig := ComputeSessionSig("acc_demo_01")
	ack := ComputeServerAck("acc_demo_01")
	if len(sig) != 16 || len(ack) != 16 || sig == ack {
		t.Fatalf("unexpected handshake values: sig=%q ack=%q", sig, ack)
	}
}
