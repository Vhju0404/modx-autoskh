// Tab 5: Hệ Thống Đa Tab VPS, Lịch Khung Giờ Chạy, Nghỉ Bảo Trì Thông Minh, Telegram Bot & Danh Sách Server NRO Chính
window.MX = window.MX || {};

window.MX.SettingsTab = (function () {
  function defaultShifts() {
    return [
      { start: '04:30', end: '12:30' },
      { start: '16:30', end: '00:30' },
    ];
  }

  function shiftRowHtml(idx, startVal, endVal) {
    const esc = window.MX.esc;
    return `
      <div class="sched-row">
        <span class="badge b-tag sched-idx">Ca ${idx + 1}</span>
        <input type="time" class="sched-start" value="${esc(startVal || '04:30')}" />
        <span class="text-dim">→</span>
        <input type="time" class="sched-end" value="${esc(endVal || '12:30')}" />
        <button type="button" class="btn btn-xs btn-danger-ghost sched-del" title="Xóa ca này">✕</button>
      </div>
    `;
  }

  function renumberShifts() {
    document.querySelectorAll('#sched-list .sched-row').forEach((row, i) => {
      const badge = row.querySelector('.sched-idx');
      if (badge) badge.textContent = `Ca ${i + 1}`;
    });
  }

  function bindShiftRowEvents(row) {
    const delBtn = row.querySelector('.sched-del');
    if (delBtn) {
      delBtn.onclick = () => {
        row.remove();
        renumberShifts();
      };
    }
  }

  function render() {
    const root = document.getElementById('tab-settings');
    const cfg = window.MX.state.settings || {};
    const servers = window.MX.state.servers || [];
    const esc = window.MX.esc;
    const privOn = window.MX.privacy.isOn();
    const shifts = (Array.isArray(cfg.schedule_windows) && cfg.schedule_windows.length > 0)
      ? cfg.schedule_windows
      : defaultShifts();

    root.innerHTML = `
      <div class="card">
        <div class="card-title">
          <span>🖥️ Cấu Hình Đa Tab, Xả RAM 3 Chốt &amp; Chống Quá Tải VPS</span>
          <div style="display:flex;gap:8px;">
            <button class="btn btn-secondary btn-sm" id="btn-open-data">📂 Mở Thư Mục Data</button>
            <button class="btn btn-primary btn-sm" id="btn-save-settings">💾 Lưu Cài Đặt Hệ Thống</button>
          </div>
        </div>
        <div class="form-grid">
          <div class="field"><label>Số Tab chạy đồng thời (MaxTabs)</label><input type="number" id="s-maxtabs" value="${cfg.max_tabs || 5}" min="1" max="100" /></div>
          <div class="field"><label>Rộng cửa sổ game (px)</label><input type="number" id="s-w" value="${cfg.window_width || 380}" min="180" max="1280" /></div>
          <div class="field"><label>Cao cửa sổ game (px)</label><input type="number" id="s-h" value="${cfg.window_height || 240}" min="140" max="720" /></div>
          <div class="field"><label>Giãn nhịp mở tab Min (ms)</label><input type="number" id="s-dmin" value="${cfg.login_delay_min_ms || 2000}" min="500" /></div>
          <div class="field"><label>Giãn nhịp mở tab Max (ms)</label><input type="number" id="s-dmax" value="${cfg.login_delay_max_ms || 4000}" min="500" /></div>
          <div class="field"><label>Số Acc tối đa / 1 Proxy</label><input type="number" id="s-maxprx" value="${cfg.max_acc_per_proxy || 2}" min="0" max="50" /></div>
          <div class="field"><label>Timeout khởi động tab (giây)</label><input type="number" id="s-stimeout" value="${cfg.startup_timeout_sec || 45}" min="20" /></div>
          <div class="field"><label>Circuit Breaker (lỗi tối đa / 10 phút)</label><input type="number" id="s-cbreak" value="${cfg.max_restarts_10min || 5}" min="2" /></div>
          <div class="field">
            <label>Ngưỡng xả RAM tự động / tab (MB, 0 = tắt)</label>
            <input type="number" id="s-maxram" value="${cfg.max_tab_ram_mb !== undefined ? cfg.max_tab_ram_mb : 380}" min="0" max="2048" />
          </div>
          <div class="field"><label>Cổng Socket nội bộ (127.0.0.1)</label><input type="number" id="s-port" value="${cfg.socket_port || 20268}" min="1024" max="65535" /></div>
          <div class="field"><label>Cổng Web UI nội bộ (127.0.0.1)</label><input type="number" id="s-uiport" value="${cfg.ui_port || 20269}" min="1024" max="65535" /></div>
          <div class="field" style="grid-column:span 2;"><label>Đường dẫn file Game (.exe)</label><input type="${privOn ? 'password' : 'text'}" id="s-exe" value="${esc(cfg.game_exe_path || 'game\\Dragonboy_vn_v231.exe')}" /></div>
        </div>
        <div class="small-sub" style="margin-top:8px;">
          🛡️ <b>Chống tắt oan 3 chốt khi gặp Boss / map đông:</b> Vượt ngưỡng sẽ xả nhẹ RAM trước (Unity GC + EmptyWorkingSet) → chỉ restart mềm nếu giữ vượt ngưỡng <b>liên tục ≥ 3 phút</b> và đang yên ổn ở trạng thái <b>FARM</b> (không cắt ngang lúc cất đồ / bán rác / mua buff).
        </div>
        <div class="check-grid" style="margin-top:10px;">
          <label class="check-row"><input type="checkbox" id="s-grid" ${cfg.auto_arrange_grid ? 'checked' : ''} /> Tự động xếp lưới cửa sổ sau khi mở tab</label>
          <label class="check-row"><input type="checkbox" id="s-rotprx" ${cfg.auto_rotate_proxy ? 'checked' : ''} /> Tự động đổi sang Proxy Live dự phòng khi Proxy Die</label>
        </div>
      </div>

      <div style="display:grid;grid-template-columns:1.1fr 0.9fr;gap:12px;">
        <div class="card">
          <div class="card-title">
            <span>📅 Lịch Khung Giờ Chạy Tự Chọn (Nhiều Ca)</span>
            <button type="button" class="btn btn-sky btn-xs" id="btn-sched-add">＋ Thêm ca</button>
          </div>
          <label class="check-row" style="margin-bottom:8px;">
            <input type="checkbox" id="s-sched-on" ${cfg.schedule_enabled ? 'checked' : ''} />
            Bật chạy theo khung giờ (tắt = chạy 24/7; ngoài giờ tự đóng sạch tab xả RAM)
          </label>
          <div id="sched-list" class="sched-list">
            ${shifts.map((w, i) => shiftRowHtml(i, w.start, w.end)).join('')}
          </div>
          <div class="small-sub" style="margin-top:6px;">
            Hỗ trợ chia bao nhiêu ca tùy thích và ca vắt qua nửa đêm (vd: <code>04:30 → 12:30</code> và <code>16:30 → 00:30</code>). Đến giờ mới tự mở lại có giãn nhịp.
          </div>
        </div>

        <div class="card">
          <div class="card-title">🌙 Bảo Trì Thông Minh (Tự Bắt Mốc ≤ 1 Phút &amp; Dò 60s)</div>
          <div class="small-sub" style="margin-bottom:10px;color:var(--green);">
            ✅ <b>Chế độ tự động luôn bật 24/7:</b> Game báo bảo trì trước 30p/15p/5p vẫn tiếp tục farm; tới khi game báo còn <b>≤ 1 phút</b> (hoặc bảo trì đột xuất vài phút) sẽ tự đóng sạch tab cho VPS nghỉ → tự dò bắt tay máy chủ NRO mỗi <b>60 giây</b> → bung lại ngay khi server lên!
          </div>
          <label class="check-row" style="margin-bottom:10px;"><input type="checkbox" id="s-maint-on" ${cfg.maintenance_enabled ? 'checked' : ''} /> Bật thêm khung giờ nghỉ cố định dự phòng (tùy chọn)</label>
          <div class="form-grid">
            <div class="field"><label>Giờ bắt đầu nghỉ (HH:MM)</label><input type="text" id="s-maint-s" value="${esc(cfg.maintenance_start || '02:55')}" /></div>
            <div class="field"><label>Giờ mở lại (HH:MM)</label><input type="text" id="s-maint-e" value="${esc(cfg.maintenance_end || '04:05')}" /></div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-title">
          <span>📣 Thông Báo Telegram Real-Time</span>
          <button class="btn btn-sky btn-xs" id="btn-test-tg">✈ Gửi Tin Test</button>
        </div>
        <label class="check-row" style="margin-bottom:8px;"><input type="checkbox" id="s-tg-on" ${cfg.telegram_enabled ? 'checked' : ''} /> Bật bắn thông báo về Telegram</label>
        <div class="form-grid">
          <div class="field"><label>Bot Token</label><input type="password" id="s-tg-token" value="${esc(cfg.telegram_bot_token || '')}" placeholder="123456:ABC-DEF..." /></div>
          <div class="field"><label>Chat ID</label><input type="${privOn ? 'password' : 'text'}" id="s-tg-chat" value="${esc(cfg.telegram_chat_id || '')}" placeholder="Vd: 987654321" /></div>
        </div>
        <div class="check-grid" style="margin-top:8px;">
          <label class="check-row"><input type="checkbox" id="s-tg-skh" ${cfg.notify_on_skh ? 'checked' : ''} /> Rớt đồ SKH</label>
          <label class="check-row"><input type="checkbox" id="s-tg-set" ${cfg.notify_on_full_set ? 'checked' : ''} /> Đủ 5/5 Set</label>
          <label class="check-row"><input type="checkbox" id="s-tg-box" ${cfg.notify_on_box_full ? 'checked' : ''} /> Full rương nhà</label>
          <label class="check-row"><input type="checkbox" id="s-tg-gem" ${cfg.notify_on_low_gem ? 'checked' : ''} /> Sắp hết ngọc</label>
        </div>
      </div>

      <div class="card">
        <div class="card-title">
          <span>🌍 Danh Sách Server NRO Chính (Lưu Local — Không Phụ Thuộc Web Ngoài)</span>
          <div style="display:flex;gap:6px;">
            <button class="btn btn-secondary btn-xs" id="btn-sv-reset">↺ Nạp 16 SV Chính Mặc Định</button>
            <button class="btn btn-secondary btn-xs" id="btn-sv-add">＋ Thêm Server</button>
            <button class="btn btn-primary btn-xs" id="btn-sv-save">💾 Lưu Danh Sách SV</button>
          </div>
        </div>
        <div class="table-wrap" style="max-height:260px;">
          <table class="data-table">
            <thead><tr><th>ID</th><th>Tên Server</th><th>Host / IP</th><th>Port</th><th style="width:60px;">Xóa</th></tr></thead>
            <tbody id="sv-tbody">
              ${servers.map((s, idx) => {
                const hideHost = privOn && !String(s.host || '').toLowerCase().endsWith('teamobi.com');
                return `
                <tr data-svidx="${idx}">
                  <td><input type="text" class="sv-id" value="${esc(s.id)}" style="width:90px;" /></td>
                  <td><input type="text" class="sv-name" value="${esc(s.name)}" style="width:150px;" /></td>
                  <td><input type="${hideHost ? 'password' : 'text'}" class="sv-host" value="${esc(s.host)}" style="width:230px;" /></td>
                  <td><input type="number" class="sv-port" value="${s.port}" style="width:90px;" /></td>
                  <td><button class="btn btn-xs btn-danger-ghost sv-del">✕</button></td>
                </tr>
              `;
              }).join('')}
            </tbody>
          </table>
        </div>
      </div>
    `;

    root.querySelectorAll('#sched-list .sched-row').forEach(bindShiftRowEvents);

    document.getElementById('btn-sched-add').onclick = () => {
      const listEl = document.getElementById('sched-list');
      const count = listEl.querySelectorAll('.sched-row').length;
      const wrap = document.createElement('div');
      wrap.innerHTML = shiftRowHtml(count, '08:00', '16:00').trim();
      const row = wrap.firstElementChild;
      bindShiftRowEvents(row);
      listEl.appendChild(row);
      renumberShifts();
    };

    document.getElementById('btn-open-data').onclick = () => window.MX.api.OpenDataFolder();

    document.getElementById('btn-save-settings').onclick = async () => {
      try {
        const schedWindows = Array.from(document.querySelectorAll('#sched-list .sched-row')).map((row) => ({
          start: (row.querySelector('.sched-start')?.value || '').trim(),
          end: (row.querySelector('.sched-end')?.value || '').trim(),
        })).filter((w) => w.start && w.end);

        const maxRamRaw = parseInt(document.getElementById('s-maxram').value, 10);
        await window.MX.api.SaveSettings({
          max_tabs: parseInt(document.getElementById('s-maxtabs').value, 10) || 5,
          window_width: parseInt(document.getElementById('s-w').value, 10) || 380,
          window_height: parseInt(document.getElementById('s-h').value, 10) || 240,
          auto_arrange_grid: document.getElementById('s-grid').checked,
          login_delay_min_ms: parseInt(document.getElementById('s-dmin').value, 10) || 2000,
          login_delay_max_ms: parseInt(document.getElementById('s-dmax').value, 10) || 4000,
          max_acc_per_proxy: parseInt(document.getElementById('s-maxprx').value, 10) || 0,
          auto_rotate_proxy: document.getElementById('s-rotprx').checked,
          startup_timeout_sec: parseInt(document.getElementById('s-stimeout').value, 10) || 45,
          max_restarts_10min: parseInt(document.getElementById('s-cbreak').value, 10) || 5,
          max_tab_ram_mb: Number.isNaN(maxRamRaw) ? 380 : Math.max(0, maxRamRaw),
          socket_port: parseInt(document.getElementById('s-port').value, 10) || 20268,
          ui_port: parseInt(document.getElementById('s-uiport').value, 10) || 20269,
          game_exe_path: document.getElementById('s-exe').value.trim(),
          schedule_enabled: document.getElementById('s-sched-on').checked,
          schedule_windows: schedWindows,
          maintenance_enabled: document.getElementById('s-maint-on').checked,
          maintenance_start: document.getElementById('s-maint-s').value.trim(),
          maintenance_end: document.getElementById('s-maint-e').value.trim(),
          telegram_enabled: document.getElementById('s-tg-on').checked,
          telegram_bot_token: document.getElementById('s-tg-token').value.trim(),
          telegram_chat_id: document.getElementById('s-tg-chat').value.trim(),
          notify_on_skh: document.getElementById('s-tg-skh').checked,
          notify_on_full_set: document.getElementById('s-tg-set').checked,
          notify_on_box_full: document.getElementById('s-tg-box').checked,
          notify_on_low_gem: document.getElementById('s-tg-gem').checked,
          notify_on_proxy_die: true,
          privacy_mode: Boolean(window.MX.privacy.isOn()),
        });
        window.MX.toast('Đã lưu cài đặt hệ thống!', 'ok');
        await window.MX.refreshBootstrap();
      } catch (err) { window.MX.toast(String(err), 'err'); }
    };

    document.getElementById('btn-test-tg').onclick = async () => {
      try {
        await document.getElementById('btn-save-settings').onclick();
        await window.MX.api.TestTelegram();
        window.MX.toast('Đã bắn tin test về Telegram thành công!', 'ok');
      } catch (err) { window.MX.toast('Lỗi Telegram: ' + String(err), 'err'); }
    };

    document.getElementById('btn-sv-add').onclick = () => {
      const tb = document.getElementById('sv-tbody');
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td><input type="text" class="sv-id" value="sv_custom" style="width:90px;" /></td>
        <td><input type="text" class="sv-name" value="Server Mới" style="width:150px;" /></td>
        <td><input type="text" class="sv-host" value="dragon1.teamobi.com" style="width:230px;" /></td>
        <td><input type="number" class="sv-port" value="14445" style="width:90px;" /></td>
        <td><button class="btn btn-xs btn-danger-ghost sv-del">✕</button></td>
      `;
      tr.querySelector('.sv-del').onclick = () => tr.remove();
      tb.appendChild(tr);
    };

    root.querySelectorAll('.sv-del').forEach((b) => {
      b.onclick = () => b.closest('tr').remove();
    });

    document.getElementById('btn-sv-save').onclick = async () => {
      const list = Array.from(document.querySelectorAll('#sv-tbody tr')).map((tr) => ({
        id: tr.querySelector('.sv-id').value.trim(),
        name: tr.querySelector('.sv-name').value.trim(),
        host: tr.querySelector('.sv-host').value.trim(),
        port: parseInt(tr.querySelector('.sv-port').value, 10) || 14445,
      })).filter((s) => s.host);
      try {
        await window.MX.api.SaveServers(list);
        window.MX.toast(`Đã lưu ${list.length} server!`, 'ok');
        await window.MX.refreshBootstrap();
      } catch (err) { window.MX.toast(String(err), 'err'); }
    };

    document.getElementById('btn-sv-reset').onclick = async () => {
      await window.MX.api.ResetDefaultServers();
      window.MX.toast('Đã khôi phục 16 Server NRO chính mặc định!', 'ok');
      await window.MX.refreshBootstrap();
    };
  }

  return { render };
})();
