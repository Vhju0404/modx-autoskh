// Khởi chạy giao diện chính Auto SKH
window.MX = window.MX || {};
window.MX.state = {
  activeTab: 'accounts',
  layout: null,
  summary: {},
  rows: [],
  profiles: [],
  servers: [],
  proxies: [],
  settings: {},
  loot_report: {},
  loot_history: [],
  logs: [],
  map_presets: [],
  skh_sets: [],
};

let lastStateSeq = 0;
let hdrObserverStarted = false;

function syncHeaderState() {
  const meta = window.MX.state.layout;
  if (!meta || !meta.t1) return;

  if (meta.pt && document.title !== meta.pt) {
    document.title = meta.pt;
  }

  const sidebar = document.querySelector('.sidebar');
  if (sidebar) {
    let slot = document.getElementById('app-hdr-slot');
    const hiddenSlot = !slot || !sidebar.contains(slot) || (() => {
      const cs = getComputedStyle(slot);
      const rc = slot.getBoundingClientRect();
      return cs.display === 'none' || cs.visibility === 'hidden' || parseFloat(cs.opacity || '1') < 0.45 || rc.width < 40 || rc.height < 20;
    })();
    if (hiddenSlot) {
      if (slot) slot.remove();
      slot = document.createElement('div');
      slot.className = 'brand';
      slot.id = 'app-hdr-slot';
      slot.innerHTML = `<div class="brand-logo-wrap" id="hdr-glyph"></div><div class="brand-text"><div class="brand-title" id="hdr-t1"></div><div class="brand-sub" id="hdr-t2"></div></div>`;
      sidebar.insertBefore(slot, sidebar.firstChild);
    }
    slot.style.setProperty('display', 'flex', 'important');
    slot.style.setProperty('visibility', 'visible', 'important');
    slot.style.setProperty('opacity', '1', 'important');

    const gBox = document.getElementById('hdr-glyph');
    if (gBox) {
      let img = gBox.querySelector('img');
      if (!img || img.src !== meta.glyph) {
        gBox.innerHTML = '';
        img = document.createElement('img');
        img.className = 'brand-logo';
        img.alt = meta.full || meta.t1;
        img.src = meta.glyph;
        gBox.appendChild(img);
      }
      gBox.style.setProperty('display', 'flex', 'important');
      gBox.style.setProperty('visibility', 'visible', 'important');
      img.style.setProperty('display', 'block', 'important');
      img.style.setProperty('opacity', '1', 'important');
    }

    const t1 = document.getElementById('hdr-t1');
    const t2 = document.getElementById('hdr-t2');
    if (t1 && t1.textContent !== meta.t1) t1.textContent = meta.t1;
    if (t2 && t2.textContent !== (meta.sub || meta.t2)) t2.textContent = meta.sub || meta.t2;

    const foot = document.getElementById('hdr-foot');
    const wantFoot = `${meta.full || meta.t1} · Tray 24/7`;
    if (foot && foot.textContent !== wantFoot) foot.textContent = wantFoot;
  }

  // Nhãn góc dưới nhẹ nhàng, tự phục hồi nếu bị ẩn/xóa trên DOM
  let corner = document.getElementById('ui-corner-tag');
  const cornerMissing = !corner || !document.body.contains(corner) || (() => {
    const cs = getComputedStyle(corner);
    return cs.display === 'none' || cs.visibility === 'hidden' || parseFloat(cs.opacity || '1') < 0.35;
  })();
  if (cornerMissing) {
    if (corner) corner.remove();
    corner = document.createElement('div');
    corner.id = 'ui-corner-tag';
    corner.className = 'wm-corner';
    document.body.appendChild(corner);
  }
  const wantLabel = meta.full || meta.t1;
  if (corner.dataset.fp !== meta.fp || !corner.querySelector('img') || !corner.textContent.includes(wantLabel)) {
    corner.dataset.fp = meta.fp || '';
    corner.innerHTML = '';
    const ci = document.createElement('img');
    ci.src = meta.glyph;
    ci.alt = wantLabel;
    const cs = document.createElement('span');
    cs.textContent = wantLabel;
    corner.appendChild(ci);
    corner.appendChild(cs);
  }
  corner.style.setProperty('display', 'inline-flex', 'important');
  corner.style.setProperty('visibility', 'visible', 'important');
  corner.style.setProperty('opacity', '1', 'important');

  // Khóa giao diện an toàn nếu binary bị chỉnh sửa sai chữ ký
  const locked = Boolean((window.MX.state.summary && window.MX.state.summary.sync_locked) || meta.ok === false);
  const lockBar = document.getElementById('sync-lock-bar');
  if (lockBar) {
    if (locked) {
      lockBar.classList.remove('hidden');
      lockBar.textContent = '⚠ Phát hiện bản cài đặt đã bị chỉnh sửa trái phép — chức năng tự động đã được tạm khóa. Vui lòng dùng bản gốc!';
    } else {
      lockBar.classList.add('hidden');
    }
  }
}

function bindLayoutWatch() {
  if (hdrObserverStarted) return;
  hdrObserverStarted = true;
  if (typeof MutationObserver !== 'undefined') {
    const obs = new MutationObserver(() => syncHeaderState());
    const sb = document.querySelector('.sidebar');
    if (sb) obs.observe(sb, { childList: true, subtree: true, attributes: true, characterData: true });
    obs.observe(document.body, { childList: true });
  }
  setInterval(syncHeaderState, 2500);
}

function openOriginVerifyModal() {
  const meta = window.MX.state.layout || {};
  const locked = Boolean((window.MX.state.summary && window.MX.state.summary.sync_locked) || meta.ok === false);
  const esc = window.MX.esc;
  const html = `
    <div style="display:flex;align-items:center;gap:14px;padding:6px 0;">
      <img src="${meta.glyph || ''}" alt="" style="width:64px;height:64px;border-radius:14px;border:1px solid rgba(245,158,11,0.5);" />
      <div>
        <div style="font-size:17px;font-weight:800;color:#fde68a;">${esc(meta.full || '')}</div>
        <div class="text-dim" style="margin-top:2px;">Bản phát hành chính chủ · Auto Up Set Kích Hoạt (Win 2012R2 → Win 11)</div>
        <div style="margin-top:6px;display:flex;gap:8px;flex-wrap:wrap;">
          <span class="badge ${locked ? 'b-err' : 'b-farm'}">${locked ? '⚠ SAI CHỮ KÝ' : '✓ TOÀN VẸN HỢP LỆ'}</span>
          <span class="badge b-tag">Build ID: ${esc(meta.fp || 'N/A')}</span>
        </div>
      </div>
    </div>
    <div class="small-sub" style="margin-top:8px;">
      Tổ hợp xác thực bản quyền ẩn: <code>Ctrl + Alt + Shift + H</code> (QLTK) · <code>Ctrl + Shift + H</code> (trong game).
    </div>
  `;
  window.MX.modal.open('🛡️ Xác Thực Bản Gốc Chính Chủ', html, [
    { label: 'Đóng', cls: 'btn-primary', onClick: () => window.MX.modal.close() },
  ]);
}

window.MX.refreshBootstrap = async function () {
  const data = await window.MX.api.GetBootstrap();
  if (typeof data.state_seq === 'number' && data.state_seq > lastStateSeq) {
    lastStateSeq = data.state_seq;
  }
  Object.assign(window.MX.state, data);
  syncHeaderState();
  bindLayoutWatch();
  renderGlobalChrome();
  renderActiveTab(true);
};

function applyLiveSnapshot(snap) {
  if (!snap) return;
  if (typeof snap.state_seq === 'number' && snap.state_seq > 0) {
    if (snap.state_seq < lastStateSeq) {
      return;
    }
    lastStateSeq = snap.state_seq;
  }
  const prevPriv = window.MX.privacy.isOn();
  window.MX.state.summary = snap.summary || window.MX.state.summary;
  if (snap.summary && typeof snap.summary.privacy_mode === 'boolean') {
    window.MX.state.settings = window.MX.state.settings || {};
    window.MX.state.settings.privacy_mode = snap.summary.privacy_mode;
  }
  window.MX.state.rows = snap.rows || [];
  window.MX.state.loot_report = snap.loot_report || {};
  window.MX.state.loot_history = snap.loot_history || [];
  window.MX.state.logs = snap.logs || [];
  renderGlobalChrome();
  const privChanged = prevPriv !== window.MX.privacy.isOn();
  if (privChanged || window.MX.state.activeTab === 'accounts' || window.MX.state.activeTab === 'loot') {
    renderActiveTab(privChanged);
  }
}

function renderGlobalChrome() {
  syncHeaderState();
  const s = window.MX.state.summary || {};
  document.getElementById('kpi-running').textContent = `${s.running_tabs || 0} / ${s.max_tabs || 5}`;
  document.getElementById('kpi-done').textContent = s.done_accounts || 0;
  document.getElementById('kpi-skh').textContent = s.total_skh_items || 0;
  document.getElementById('kpi-sets').textContent = s.complete_sets || 0;
  document.getElementById('kpi-proxy').textContent = `${s.live_proxies || 0} / ${s.total_proxies || 0}`;
  document.getElementById('kpi-warn').textContent = s.warning_accounts || 0;

  document.getElementById('badge-accounts').textContent = s.total_accounts || 0;
  document.getElementById('badge-profiles').textContent = (window.MX.state.profiles || []).length;
  document.getElementById('badge-skh').textContent = s.total_skh_items || 0;
  document.getElementById('badge-proxy').textContent = s.total_proxies || 0;

  const dot = document.getElementById('engine-dot');
  const lbl = document.getElementById('engine-label');
  const pill = document.getElementById('engine-pill');
  const btn = document.getElementById('btn-toggle-autorun');

  if (s.sync_locked) {
    dot.className = 'dot dot-maint';
    lbl.textContent = 'Bản bị chỉnh sửa';
    if (pill) pill.title = 'Sai chữ ký toàn vẹn ứng dụng';
  } else if (s.outside_schedule) {
    dot.className = 'dot dot-maint';
    lbl.textContent = 'Nghỉ ngoài ca chạy';
    if (pill) pill.title = s.maint_reason || 'Đang ngoài khung giờ ca chạy tự chọn — tự mở lại khi đến ca';
  } else if (s.in_maintenance) {
    dot.className = 'dot dot-maint';
    lbl.textContent = 'Đang nghỉ bảo trì';
    if (pill) pill.title = s.maint_reason || 'Server đang bảo trì — tự dò cổng mở lại mỗi 60s';
  } else if (s.auto_running) {
    dot.className = 'dot dot-run';
    lbl.textContent = `Đang tự động (${s.running_tabs || 0}/${s.max_tabs || 5})`;
    if (pill) pill.title = 'Đang chạy tự động 24/7';
  } else {
    dot.className = 'dot dot-idle';
    lbl.textContent = 'Chế độ thủ công';
    if (pill) pill.title = 'Chế độ thủ công — vẫn giữ Watchdog tự mở lại các tab bạn bấm Chạy';
  }

  btn.disabled = Boolean(s.sync_locked);
  if (s.auto_running) {
    btn.className = 'btn btn-danger-ghost btn-full';
    btn.textContent = '⏸ Tạm Dừng Xoay Vòng';
  } else {
    btn.className = 'btn btn-primary btn-full';
    btn.textContent = '▶ Bật Auto Xoay Vòng';
  }

  const privOn = window.MX.privacy.isOn();
  const privBtn = document.getElementById('btn-toggle-privacy');
  if (privBtn) {
    privBtn.className = privOn ? 'btn btn-privacy-on' : 'btn btn-secondary';
    privBtn.textContent = privOn ? '🙈 Đang Che TK' : '👁 Che TK';
  }

  renderLogs();
}

function renderLogs() {
  const box = document.getElementById('log-list');
  if (!box) return;
  const esc = window.MX.esc;
  const priv = window.MX.privacy;
  const privOn = priv.isOn();
  const logs = window.MX.state.logs || [];
  box.innerHTML = logs.slice(0, 120).map((l) => {
    const accDisp = (!privOn || l.account === 'SYSTEM') ? l.account : priv.accAlias(l.account, l.account);
    const msgDisp = privOn ? priv.maskText(l.message) : l.message;
    return `
      <div class="log-line log-${esc(l.level)}">
        <span class="text-dim">[${esc(l.time)}]</span>
        <span>[${esc(accDisp)}]</span>
        <span>${esc(msgDisp)}</span>
      </div>
    `;
  }).join('');
}

function renderActiveTab(force) {
  const tab = window.MX.state.activeTab;
  if (tab === 'accounts') window.MX.AccountsTab.render();
  else if (tab === 'profiles' && force) window.MX.ProfilesTab.render();
  else if (tab === 'loot') window.MX.LootTab.render();
  else if (tab === 'proxy' && force) window.MX.ProxyTab.render();
  else if (tab === 'settings' && force) window.MX.SettingsTab.render();
}

document.addEventListener('DOMContentLoaded', async () => {
  // Tổ hợp phím bí mật xác thực bản gốc (Ctrl + Alt + Shift + H)
  document.addEventListener('keydown', (e) => {
    if (e.ctrlKey && e.altKey && e.shiftKey && String(e.key || '').toLowerCase() === 'h') {
      e.preventDefault();
      openOriginVerifyModal();
    }
  });

  // Chuyển Tab Sidebar
  document.querySelectorAll('.nav-item').forEach((btn) => {
    btn.onclick = () => {
      const tab = btn.dataset.tab;
      window.MX.state.activeTab = tab;
      document.querySelectorAll('.nav-item').forEach((b) => b.classList.toggle('active', b === btn));
      document.querySelectorAll('.tab-pane').forEach((p) => p.classList.toggle('active', p.id === `tab-${tab}`));
      renderActiveTab(true);
    };
  });

  document.getElementById('btn-toggle-autorun').onclick = async () => {
    const next = !window.MX.state.summary.auto_running;
    await window.MX.api.ToggleAutoRun(next);
    window.MX.toast(next ? 'Đã bật chế độ tự động xoay vòng dàn acc!' : 'Đã tắt xoay vòng tự động.', next ? 'ok' : 'info');
  };

  document.getElementById('btn-stop-all').onclick = async () => {
    await window.MX.api.StopAllTabs();
    window.MX.toast('Đã đóng toàn bộ cửa sổ game.');
  };

  document.getElementById('btn-toggle-privacy').onclick = async () => {
    const next = !window.MX.privacy.isOn();
    try {
      await window.MX.api.TogglePrivacyMode(next);
      window.MX.state.summary.privacy_mode = next;
      window.MX.state.settings.privacy_mode = next;
      renderGlobalChrome();
      renderActiveTab(true);
      window.MX.toast(
        next
          ? '🙈 Đã BẬT Che TK — che sạch tài khoản, nhân vật, proxy, token & tiêu đề cửa sổ game!'
          : '👁 Đã TẮT Che TK — hiển thị lại thông tin đầy đủ.',
        next ? 'ok' : 'info'
      );
    } catch (err) {
      window.MX.toast(String(err), 'err');
    }
  };

  document.getElementById('btn-arrange-grid').onclick = async () => {
    await window.MX.api.ArrangeWindows();
    window.MX.toast('Đã sắp xếp lưới cửa sổ game!', 'ok');
  };

  const logDrawer = document.getElementById('log-drawer');
  document.getElementById('btn-toggle-logs').onclick = () => logDrawer.classList.toggle('collapsed');
  document.getElementById('btn-close-logs').onclick = () => logDrawer.classList.add('collapsed');
  document.getElementById('btn-clear-logs').onclick = () => window.MX.api.ClearLogs();

  const quitBtn = document.getElementById('btn-quit-app');
  if (quitBtn) {
    quitBtn.onclick = async () => {
      const appName = (window.MX.state.layout && window.MX.state.layout.full) || 'Auto SKH';
      if (!confirm(`Thoát hoàn toàn ${appName} & đóng toàn bộ tab game?\n\n(Mẹo: nếu chỉ muốn tắt giao diện cho nhẹ VPS, cứ bấm X đóng cửa sổ trình duyệt — tool vẫn chạy ngầm 24/7 dưới khay hệ thống!)`)) return;
      try {
        await window.MX.api.ShutdownApp();
        window.MX.toast(`Đang thoát hoàn toàn ${appName}...`, 'info');
        setTimeout(() => window.close(), 350);
      } catch (err) {
        window.MX.toast(String(err), 'err');
      }
    };
  }

  // Nhận push real-time từ Go backend (ưu tiên SSE chuẩn trình duyệt, fallback Wails nếu có)
  if (window.runtime && typeof window.runtime.EventsOn === 'function') {
    window.runtime.EventsOn('state_update', (snap) => applyLiveSnapshot(snap));
  } else if (typeof EventSource !== 'undefined') {
    const es = new EventSource('/api/events');
    es.addEventListener('state_update', (ev) => {
      try {
        applyLiveSnapshot(JSON.parse(ev.data));
      } catch (_) {}
    });
  }

  try {
    await window.MX.refreshBootstrap();
  } catch (err) {
    window.MX.toast('Lỗi nạp dữ liệu khởi động: ' + String(err), 'err');
  }
});
