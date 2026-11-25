// Bridge gọi Go backend (ưu tiên HTTP Local API chạy chuẩn Win 2012 R2 -> Win 11, giữ tương thích Wails nếu có)
window.MX = window.MX || {};

window.MX.api = new Proxy({}, {
  get(_, method) {
    return async (...args) => {
      const wailsFn = window.go && window.go.main && window.go.main.App && window.go.main.App[method];
      if (typeof wailsFn === 'function') {
        return await wailsFn(...args);
      }
      const res = await fetch(`/api/${encodeURIComponent(String(method))}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ args }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok || data.error) {
        throw new Error(data.error || `Lỗi API ${String(method)} (${res.status})`);
      }
      return data.result;
    };
  }
});

window.MX.privacy = {
  isOn() {
    const st = window.MX.state || {};
    if (st.summary && typeof st.summary.privacy_mode === 'boolean') return st.summary.privacy_mode;
    return Boolean(st.settings && st.settings.privacy_mode);
  },
  accAlias(username, accountId) {
    const rows = (window.MX.state && window.MX.state.rows) || [];
    if (accountId) {
      const idx = rows.findIndex((r) => r.account && r.account.id === accountId);
      if (idx >= 0) return `Acc #${idx + 1}`;
    }
    if (username) {
      const low = String(username).toLowerCase();
      const idx = rows.findIndex((r) => r.account && String(r.account.username || '').toLowerCase() === low);
      if (idx >= 0) return `Acc #${idx + 1}`;
    }
    return 'Acc #•••';
  },
  maskUser(username, accountId) {
    if (!this.isOn()) return String(username || '');
    return this.accAlias(username, accountId);
  },
  maskChar(charName, fallback) {
    if (!this.isOn()) return String(charName || fallback || '');
    if (charName) return 'Nhân vật •••';
    return (!fallback || fallback === 'Chưa vào game') ? 'Chưa vào game' : '••••••';
  },
  maskTag(tag) {
    if (!tag) return '';
    return this.isOn() ? 'Nhóm •••' : String(tag);
  },
  maskProxyLabel(label) {
    if (!this.isOn()) return String(label || '');
    return '•••.•••.•••:•••';
  },
  maskProxyUser(user) {
    if (!user) return '—';
    return this.isOn() ? '••••••' : String(user);
  },
  maskText(text) {
    let out = String(text ?? '');
    if (!this.isOn() || !out) return out;
    const st = window.MX.state || {};
    const rows = st.rows || [];
    const proxies = st.proxies || [];
    const cfg = st.settings || {};

    const pairs = [];
    rows.forEach((r, i) => {
      const alias = `Acc #${i + 1}`;
      if (r.account) {
        if (r.account.username) pairs.push([r.account.username, alias]);
        if (r.account.tag) pairs.push([r.account.tag, 'Nhóm •••']);
        if (r.account.note) pairs.push([r.account.note, '••••••']);
        if (r.account.custom_proxy) pairs.push([r.account.custom_proxy, '•••.•••.•••:•••']);
      }
      if (r.runtime) {
        if (r.runtime.char_name) pairs.push([r.runtime.char_name, 'Nhân vật •••']);
        if (r.runtime.active_proxy && r.runtime.active_proxy !== 'Home') pairs.push([r.runtime.active_proxy, '•••.•••.•••:•••']);
      }
      if (r.proxy_label && !r.proxy_label.startsWith('Home') && r.proxy_label !== 'Tự động') {
        pairs.push([r.proxy_label, '•••.•••.•••:•••']);
      }
    });
    proxies.forEach((p) => {
      if (p.raw) pairs.push([p.raw, '•••.•••.•••:•••']);
      if (p.host) {
        pairs.push([`${p.host}:${p.port}`, '•••.•••.•••:•••']);
        pairs.push([p.host, '•••.•••.•••']);
      }
      if (p.username) pairs.push([p.username, '••••••']);
      if (p.password) pairs.push([p.password, '••••••']);
    });
    if (cfg.telegram_bot_token) pairs.push([cfg.telegram_bot_token, '••••••']);
    if (cfg.telegram_chat_id) pairs.push([cfg.telegram_chat_id, '••••••']);

    pairs.sort((a, b) => b[0].length - a[0].length);
    for (const [needle, repl] of pairs) {
      if (!needle || needle.length < 2) continue;
      const escaped = needle.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      out = out.replace(new RegExp(escaped, 'gi'), repl);
    }

    out = out
      .replace(/\b(?:https?|socks5h?|socks4a?|socks):\/\/\S+/gi, '•••.•••.•••:•••')
      .replace(/[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g, 'Acc #•••')
      .replace(/\b\d{1,3}(?:\.\d{1,3}){3}(?::\d{1,5})?\b/g, '•••.•••.•••:•••')
      .replace(/Proxy:\s*[^)\s,]+(?:\s*\([^)]*\))?/gi, 'Proxy: •••.•••.•••:•••')
      .replace(/[A-Za-z]:\\Users\\[^\\\s]+/gi, 'C:\\Users\\•••');
    return out;
  }
};

window.MX.toast = function (msg, kind = 'info') {
  const stack = document.getElementById('toast-stack');
  if (!stack) return;
  const el = document.createElement('div');
  el.className = `toast ${kind}`;
  el.textContent = window.MX.privacy ? window.MX.privacy.maskText(msg) : msg;
  stack.appendChild(el);
  setTimeout(() => el.remove(), 3400);
};

window.MX.esc = function (s) {
  return String(s ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
};

window.MX.modal = {
  open(title, bodyHtml, footerButtons = []) {
    document.getElementById('modal-title').textContent = title;
    document.getElementById('modal-body').innerHTML = bodyHtml;
    const foot = document.getElementById('modal-foot');
    foot.innerHTML = '';
    footerButtons.forEach((b) => {
      const btn = document.createElement('button');
      btn.className = `btn ${b.cls || 'btn-secondary'}`;
      btn.textContent = b.label;
      btn.onclick = b.onClick;
      foot.appendChild(btn);
    });
    document.getElementById('modal-backdrop').classList.remove('hidden');
  },
  close() {
    document.getElementById('modal-backdrop').classList.add('hidden');
  }
};

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('modal-close').onclick = () => window.MX.modal.close();
  document.getElementById('modal-backdrop').addEventListener('click', (e) => {
    if (e.target.id === 'modal-backdrop') window.MX.modal.close();
  });
  document.addEventListener('click', () => {
    const cm = document.getElementById('ctx-menu');
    if (cm) cm.classList.add('hidden');
  });
});
