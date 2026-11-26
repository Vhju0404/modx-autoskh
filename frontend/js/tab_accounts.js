// Tab 1: Bảng Quản Lý Tài Khoản Thông Minh (Live Telemetry + Bulk Actions + Menu Chuột Phải)
window.MX = window.MX || {};

window.MX.AccountsTab = (function () {
  const ui = {
    filter: 'all', // all | running | pending | done | skh | warn
    search: '',
    serverFilter: '',
    tagFilter: '',
    profileFilter: '',
    selected: new Set(),
    xrayId: '',
    initialized: false,
  };

  function getFilteredRows() {
    const st = window.MX.state;
    const q = ui.search.trim().toLowerCase();
    return (st.rows || []).filter((r) => {
      const a = r.account;
      const rt = r.runtime;
      if (ui.filter === 'running' && !rt.running) return false;
      if (ui.filter === 'pending' && a.done) return false;
      if (ui.filter === 'done' && !a.done) return false;
      if (ui.filter === 'skh' && (rt.skh_count || 0) <= 0) return false;
      if (ui.filter === 'mam' && !rt.is_new_member) return false;
      if (ui.filter === 'warn' && !(rt.fsm_state === 'ERROR' || rt.last_error || r.proxy_status === 'die')) return false;
      if (ui.serverFilter && a.server_id !== ui.serverFilter) return false;
      if (ui.tagFilter && (a.tag || '') !== ui.tagFilter) return false;
      if (ui.profileFilter && a.profile_id !== ui.profileFilter) return false;
      if (q) {
        const hay = `${a.username} ${a.tag || ''} ${r.server_name} ${r.profile_name} ${rt.map_name || ''} ${rt.status_text || ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }

  function render() {
    const root = document.getElementById('tab-accounts');
    if (!ui.initialized) {
      root.innerHTML = `
        <div class="toolbar">
          <div class="toolbar-group">
            <input type="text" id="acc-search" placeholder="🔍 Tìm tài khoản, nhóm, map, trạng thái..." style="width:240px;" />
            <select id="acc-filter-sv"><option value="">Tất cả Server</option></select>
            <select id="acc-filter-prof"><option value="">Tất cả Profile</option></select>
            <select id="acc-filter-tag"><option value="">Tất cả Nhóm</option></select>
          </div>
          <div class="toolbar-group">
            <button class="btn btn-primary" id="btn-add-acc-bulk">＋ Thêm Acc</button>
            <button class="btn btn-secondary" id="btn-export-acc">⤓ Xuất Acc</button>
          </div>
        </div>

        <div class="chip-bar" id="acc-chips">
          <button class="chip active" data-f="all">Tất cả</button>
          <button class="chip" data-f="running">Đang chạy</button>
          <button class="chip" data-f="pending">Chưa xong</button>
          <button class="chip" data-f="done">Full rương (Xong)</button>
          <button class="chip" data-f="mam">🌱 Còn mầm SKH</button>
          <button class="chip" data-f="skh">Có SKH</button>
          <button class="chip" data-f="warn">Cảnh báo / Lỗi</button>
        </div>

        <div class="bulk-bar hidden" id="acc-bulk-bar">
          <strong id="bulk-count" class="text-gold">Đã chọn 0</strong>
          <button class="btn btn-xs btn-success" data-bulk="start">▶ Chạy</button>
          <button class="btn btn-xs btn-sky" data-bulk="start_headless" title="Mở ngầm không cửa sổ (0% GPU, ~0% CPU, giảm 3× RAM)">👻 Treo ngầm</button>
          <button class="btn btn-xs btn-secondary" data-bulk="open_visible" title="Mở lại có cửa sổ game để cầm tay">👁 Mở có hình</button>
          <button class="btn btn-xs btn-danger-ghost" data-bulk="stop">⏹ Dừng</button>
          <button class="btn btn-xs btn-secondary" data-bulk="restart">↻ Mở lại</button>
          <button class="btn btn-xs btn-secondary" data-bulk="assign">🏷️ Gán hàng loạt</button>
          <button class="btn btn-xs btn-sky" data-bulk="switch_zone">🔀 Đổi khu vắng</button>
          <button class="btn btn-xs btn-sky" data-bulk="force_store">🏠 Ép cất đồ</button>
          <button class="btn btn-xs btn-sky" data-bulk="force_sell">💰 Ép bán rác</button>
          <button class="btn btn-xs btn-secondary" data-bulk="pause">⏸ Tạm dừng auto</button>
          <button class="btn btn-xs btn-secondary" data-bulk="resume">▶ Tiếp tục auto</button>
          <button class="btn btn-xs btn-secondary" data-bulk="toggle_mob">👾 Ẩn/Hiện quái</button>
          <button class="btn btn-xs btn-secondary" data-bulk="toggle_char">🧍 Ẩn/Hiện NV &amp; PK</button>
          <button class="btn btn-xs btn-secondary" data-bulk="toggle_inv">🎒 Túi Dọc/Ngang</button>
          <button class="btn btn-xs btn-secondary" data-bulk="toggle_fastatk">⚡ Đánh xóa anim</button>
          <button class="btn btn-xs btn-secondary" data-bulk="black">🌑 Màn đen VPS</button>
          <button class="btn btn-xs btn-secondary" data-bulk="reset_done">↺ Reset Done</button>
          <button class="btn btn-xs btn-danger" data-bulk="delete">🗑 Xóa</button>
        </div>

        <div class="table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th style="width:26px;"><input type="checkbox" id="acc-check-all" /></th>
                <th style="width:26px;">#</th>
                <th>Nhóm</th>
                <th>Tài khoản / NV</th>
                <th>SV · Profile</th>
                <th>Map · Khu</th>
                <th>Ngọc · Buff</th>
                <th>Túi / Rương</th>
                <th>Đồ SKH · Sao</th>
                <th>Proxy</th>
                <th>Trạng thái</th>
                <th class="col-actions" style="width:112px;">Thao tác</th>
              </tr>
            </thead>
            <tbody id="acc-tbody"></tbody>
          </table>
        </div>
      `;
      bindStaticEvents();
      ui.initialized = true;
    }

    syncDropdownFilters();
    renderTableBody();
    updateBulkBar();
    refreshOpenXRayIfNeeded();
  }

  function bindStaticEvents() {
    document.getElementById('acc-search').oninput = (e) => {
      ui.search = e.target.value;
      renderTableBody();
    };
    document.getElementById('acc-filter-sv').onchange = (e) => {
      ui.serverFilter = e.target.value;
      renderTableBody();
    };
    document.getElementById('acc-filter-prof').onchange = (e) => {
      ui.profileFilter = e.target.value;
      renderTableBody();
    };
    document.getElementById('acc-filter-tag').onchange = (e) => {
      ui.tagFilter = e.target.value;
      renderTableBody();
    };
    document.getElementById('acc-chips').addEventListener('click', (e) => {
      const btn = e.target.closest('.chip');
      if (!btn) return;
      ui.filter = btn.dataset.f;
      document.querySelectorAll('#acc-chips .chip').forEach((c) => c.classList.toggle('active', c === btn));
      renderTableBody();
    });
    document.getElementById('acc-check-all').onchange = (e) => {
      const rows = getFilteredRows();
      if (e.target.checked) rows.forEach((r) => ui.selected.add(r.account.id));
      else rows.forEach((r) => ui.selected.delete(r.account.id));
      renderTableBody();
      updateBulkBar();
    };
    document.getElementById('btn-add-acc-bulk').onclick = openBulkAddModal;
    document.getElementById('btn-export-acc').onclick = openExportModal;

    document.getElementById('acc-bulk-bar').addEventListener('click', async (e) => {
      const btn = e.target.closest('button[data-bulk]');
      if (!btn) return;
      const ids = Array.from(ui.selected);
      if (ids.length === 0) return;
      await executeBulkAction(btn.dataset.bulk, ids);
    });
  }

  function syncDropdownFilters() {
    const st = window.MX.state;
    const svSel = document.getElementById('acc-filter-sv');
    const profSel = document.getElementById('acc-filter-prof');
    const tagSel = document.getElementById('acc-filter-tag');

    const svList = st.servers || [];
    const profList = st.profiles || [];
    const tags = Array.from(new Set((st.rows || []).map((r) => r.account.tag).filter(Boolean))).sort();

    // Nếu bộ lọc cũ trỏ vào server/profile/nhóm vừa bị xóa -> tự reset về "Tất cả" để không ẩn nhầm acc mới nhập
    if (ui.serverFilter && !svList.some((s) => s.id === ui.serverFilter)) ui.serverFilter = '';
    if (ui.profileFilter && !profList.some((p) => p.id === ui.profileFilter)) ui.profileFilter = '';
    if (ui.tagFilter && !tags.includes(ui.tagFilter)) ui.tagFilter = '';

    fillSelect(svSel, 'Tất cả Server', svList.map((s) => [s.id, s.name]), ui.serverFilter);
    fillSelect(profSel, 'Tất cả Profile', profList.map((p) => [p.id, p.name]), ui.profileFilter);
    const privOn = window.MX.privacy.isOn();
    fillSelect(tagSel, 'Tất cả Nhóm', tags.map((t, i) => [t, privOn ? `Nhóm #${i + 1}` : t]), ui.tagFilter);
  }

  function fillSelect(el, defaultLabel, items, curVal) {
    const sig = items.map((x) => x.join('=')).join('|');
    if (el.dataset.sig !== sig) {
      el.dataset.sig = sig;
      el.innerHTML = `<option value="">${window.MX.esc(defaultLabel)}</option>` +
        items.map(([v, l]) => `<option value="${window.MX.esc(v)}">${window.MX.esc(l)}</option>`).join('');
    }
    el.value = curVal;
  }

  function fsmBadge(rt, acc) {
    if (acc.done || rt.fsm_state === 'DONE') return `<span class="badge b-done">✓ XONG</span>`;
    if (rt.paused) return `<span class="badge b-pause">⏸ TẠM DỪNG</span>`;
    switch (rt.fsm_state) {
      case 'FARM': return `<span class="badge b-farm">⚔ FARM</span>`;
      case 'STORE': return `<span class="badge b-store">🏠 CẤT ĐỒ</span>`;
      case 'SELL': return `<span class="badge b-sell">💰 BÁN RÁC</span>`;
      case 'BUFF': return `<span class="badge b-buff">✨ MUA BUFF</span>`;
      case 'TRAVEL':
      case 'ZONE': return `<span class="badge b-store">🧭 DI CHUYỂN</span>`;
      case 'LOGIN': return `<span class="badge b-buff">🔑 ĐĂNG NHẬP</span>`;
      case 'DEAD': return `<span class="badge b-err">💀 HỒI SINH</span>`;
      case 'ERROR': return `<span class="badge b-err">⚠ LỖI</span>`;
      default: return `<span class="badge b-idle">● NGHỈ</span>`;
    }
  }

  function planetBadge(planet) {
    if (planet === 'TĐ') return `<span class="badge b-td">TĐ</span>`;
    if (planet === 'NM') return `<span class="badge b-nm">NM</span>`;
    if (planet === 'XD') return `<span class="badge b-xd">XD</span>`;
    return '';
  }

  function renderTableBody() {
    const tbody = document.getElementById('acc-tbody');
    if (!tbody) return;
    // Tự dọn các ID đã bị xóa khỏi tập selected để không bao giờ kẹt thanh bulk hoặc gửi nhầm ID cũ
    const validIds = new Set((window.MX.state.rows || []).map((r) => r.account.id));
    for (const id of Array.from(ui.selected)) {
      if (!validIds.has(id)) ui.selected.delete(id);
    }
    const rows = getFilteredRows();
    const chkAll = document.getElementById('acc-check-all');
    if (chkAll) {
      chkAll.checked = rows.length > 0 && rows.every((r) => ui.selected.has(r.account.id));
    }
    const esc = window.MX.esc;

    if (rows.length === 0) {
      tbody.innerHTML = `<tr><td colspan="12" style="text-align:center;padding:34px;color:var(--text-dim);">Chưa có tài khoản nào khớp bộ lọc — bấm <b>＋ Thêm Acc</b> để dán danh sách tài khoản vào nhé!</td></tr>`;
      return;
    }

    const accLootMap = new Map();
    for (const accRep of ((window.MX.state.loot_report && window.MX.state.loot_report.accounts) || [])) {
      accLootMap.set(accRep.account_id, accRep);
    }

    tbody.innerHTML = rows.map((r, idx) => {
      const a = r.account;
      const rt = r.runtime;
      const checked = ui.selected.has(a.id);

      const inGame = rt.running && rt.connected && Boolean(rt.char_name) && rt.fsm_state !== 'LOGIN' && rt.fsm_state !== 'IDLE';

      const mapNameRaw = inGame
        ? (rt.map_name || ('Map ' + rt.map_id))
        : (a.map_id >= 0 ? ('Map ' + a.map_id) : 'Theo Profile');
      const privMapPill = (inGame && rt.in_private_map)
        ? `<span class="badge b-buff" style="font-size:10px;padding:1px 5px;" title="Đang ở trong Map Riêng Tư (Vé 1825)">🎫 Riêng</span>`
        : `<b>K${rt.zone_id}</b> <span class="text-dim">[${rt.zone_players}/${rt.zone_max || 15}]</span>`;
      const mapText = inGame
        ? `<span class="cell-trunc cell-map" title="${esc(mapNameRaw)} · ${rt.in_private_map ? 'Map Riêng' : ('K' + rt.zone_id + ' [' + rt.zone_players + '/' + (rt.zone_max || 15) + ']')} · (${rt.cx || 0}, ${rt.cy || 0})">${esc(mapNameRaw)}</span><span class="small-sub">${privMapPill}</span>`
        : (a.map_id >= 0
            ? `<span class="text-dim cell-trunc cell-map">${esc(mapNameRaw)}</span><span class="small-sub">${a.zone_mode === 'fixed' ? 'K' + a.zone_id : 'Khu vắng'}</span>`
            : `<span class="text-dim">Theo Profile</span>`);

      const totalGem = (rt.gem || 0) + (rt.gem_lock || 0);
      const gemCls = inGame && totalGem < 10 ? 'text-red' : 'text-sky';
      const luckBadge = rt.luck_percent > 0
        ? ` · <span class="text-green" title="${esc(rt.luck_detail || '')}">🍀+${rt.luck_percent}%</span>`
        : '';
      const titleName = rt.title_name || a.title_name || '';
      let titleShort = '';
      let titleTip = '';
      if (rt.title_name) {
        const rem = rt.title_remain ? rt.title_remain : 'OK';
        titleTip = `Danh hiệu: ${rt.title_name}${rt.title_remain ? ' (' + rt.title_remain + ')' : ''}`;
        titleShort = ` · <span class="text-gold" title="${esc(titleTip)}">🏅${esc(rem)}</span>`;
      } else if (a.title_expiry_ms > 0) {
        const remMs = a.title_expiry_ms - Date.now();
        if (remMs > 0) {
          const d = Math.floor(remMs / 86400000);
          const h = Math.floor((remMs % 86400000) / 3600000);
          const rem = d > 0 ? `${d}n${h}g` : `${h}g`;
          titleTip = `Danh hiệu: ${titleName || 'Danh hiệu'} (${rem})`;
          titleShort = ` · <span class="text-gold" title="${esc(titleTip)}">🏅${esc(rem)}</span>`;
        } else {
          titleTip = 'Danh hiệu đã hết hạn';
          titleShort = ` · <span class="text-red" title="${esc(titleTip)}">🏅Hết</span>`;
        }
      }
      const tdltTag = `${rt.has_tdlt_active ? 'TDLT' : 'Off'}:${rt.tdlt_minutes}p`;
      const ticketShort = (inGame && rt.ticket_remain)
        ? ` · <span class="text-gold" title="Thời gian Vé Riêng Tư [1825] còn lại">🎫${esc(rt.ticket_remain)}</span>`
        : '';
      const buffFullTip = `TDLT: ${rt.has_tdlt_active ? 'Bật' : 'Tắt'} (${rt.tdlt_minutes}p) · Cỏ 4 lá: ${rt.has_lucky_grass ? 'Có' : 'Chưa'}${rt.ticket_remain ? ' · Vé riêng: ' + rt.ticket_remain : ''}${titleTip ? ' · ' + titleTip : ''}`;
      const resText = inGame
        ? `<span class="${gemCls}">💎${rt.gem}/${rt.gem_lock}</span>${luckBadge}<span class="small-sub cell-trunc cell-buff" title="${esc(buffFullTip)}">${tdltTag} · Cỏ:${rt.has_lucky_grass ? '✓' : '✗'}${ticketShort}${titleShort}</span>`
        : (titleShort ? `<span class="small-sub">${titleShort.replace(/^ · /, '')}</span>` : `<span class="text-dim">—</span>`);

      const maxRam = Number((window.MX.state.settings && window.MX.state.settings.max_tab_ram_mb) || 0);
      const ramCls = (maxRam > 0 && rt.ram_mb >= maxRam) ? 'text-red' : ((maxRam > 0 && rt.ram_mb >= maxRam - 50) ? 'text-gold' : 'text-dim');
      const hlIcon = (rt.running && rt.headless) ? `<span title="Đang treo ngầm Headless không cửa sổ (siêu nhẹ VPS)">👻</span>` : '';
      const ramTag = (rt.running && rt.ram_mb > 0) ? ` · ${hlIcon}<span class="${ramCls}" title="RAM thực tế của tab game (WorkingSet)">${rt.ram_mb}MB</span>` : (hlIcon ? ` · ${hlIcon}` : '');
      const bagBox = inGame
        ? `<span class="${rt.bag_used >= rt.bag_max && rt.bag_max > 0 ? 'text-red' : ''}">T ${rt.bag_used}/${rt.bag_max}</span><span class="small-sub">R ${rt.box_used}/${rt.box_max}${ramTag}</span>`
        : (rt.running && (rt.ram_mb > 0 || rt.headless)
            ? `<span class="text-dim">—</span><span class="small-sub">${hlIcon}${rt.ram_mb > 0 ? `<span class="${ramCls}" title="RAM thực tế của tab game (WorkingSet)">${rt.ram_mb}MB</span>` : ''}</span>`
            : `<span class="text-dim">—</span>`);

      const mamBadge = inGame
        ? (rt.is_new_member
            ? ` <span class="badge b-farm" title="Thời gian mầm SKH còn lại">🌱${rt.mam_remain ? esc(rt.mam_remain) : 'Mầm'}</span>`
            : ` <span class="badge b-idle">Hết mầm</span>`)
        : '';

      const accLoot = accLootMap.get(a.id);
      const fmtCode = (code) => window.MX.LootTab && window.MX.LootTab.renderCodeDigits
        ? window.MX.LootTab.renderCodeDigits(code)
        : esc(code || '0-0-0-0-0');

      let lootHtml;
      if (accLoot && accLoot.sets && accLoot.sets.length > 0) {
        const lines = accLoot.sets.map((s) =>
          `<div class="acc-skh-row ${s.total_items > 0 ? 'has' : ''}"><span>${esc(s.short_name || s.set_name)}</span>${fmtCode(s.code || '0-0-0-0-0')}</div>`
        ).join('');
        const starLine = rt.star_count > 0 ? `<div class="small-sub text-sky">★ ${rt.star_count} sao</div>` : '';
        lootHtml = `<div class="acc-skh-box" data-loot="${esc(a.id)}" title="Áo - Quần - Găng - Giày - Rada (bấm xem chi tiết)">${lines}${starLine}</div>`;
      } else {
        lootHtml = `<span class="acc-skh-empty" data-loot="${esc(a.id)}" title="Áo - Quần - Găng - Giày - Rada · Chưa vào game">0-0-0-0-0</span>`;
      }

      const prxCls = r.proxy_status === 'die' ? 'text-red' : (r.proxy_label.startsWith('Home') ? 'text-dim' : 'text-green');
      const priv = window.MX.privacy;
      const dispTag = priv.maskTag(a.tag);
      const dispUser = priv.maskUser(a.username, a.id);
      const dispChar = priv.maskChar(rt.char_name, a.note || 'Chưa vào game');
      const dispProxyRaw = priv.maskProxyLabel(r.proxy_label);
      const dispProxyShort = dispProxyRaw.replace(' (riêng)', ' (R)');
      const dispStatus = priv.maskText(rt.status_text || '');
      const svLabel = r.server_name || a.server_id;
      const profLabel = r.profile_name || 'Mặc định';

      const expBadge = (inGame && rt.fsm_state === 'FARM' && typeof rt.last_exp_sec === 'number' && rt.last_exp_sec >= 0)
        ? (() => {
            const cls = rt.last_exp_sec <= 15 ? 'text-green' : (rt.last_exp_sec <= 45 ? 'text-gold' : 'text-red');
            const ic = rt.last_exp_sec <= 15 ? '⚡' : (rt.last_exp_sec <= 45 ? '⏳' : '⚠');
            return ` <span class="${cls}" style="font-size:11px;font-weight:700;" title="Lần cuối ăn Tiềm Năng cách đây ${rt.last_exp_sec}s · Tốc độ ${rt.hits_per_min || 0} nhịp TN/phút (bấm để Soi X-Quang)">${ic}${rt.last_exp_sec}s · ${rt.hits_per_min || 0}/p</span>`;
          })()
        : '';

      return `
        <tr class="${checked ? 'selected' : ''}" data-id="${esc(a.id)}">
          <td><input type="checkbox" class="row-chk" data-id="${esc(a.id)}" ${checked ? 'checked' : ''} /></td>
          <td class="text-dim">${idx + 1}</td>
          <td>${dispTag ? `<span class="badge b-tag cell-trunc cell-tag" title="${esc(dispTag)}">${esc(dispTag)}</span>` : '<span class="text-dim">—</span>'}</td>
          <td>
            <div style="display:flex;align-items:center;gap:4px;">
              <b class="cell-trunc cell-user" title="${esc(dispUser)}">${esc(dispUser)}</b>${planetBadge(rt.planet)}
            </div>
            <span class="small-sub cell-trunc cell-user" title="${esc(dispChar)}">${esc(dispChar)}${mamBadge}</span>
          </td>
          <td>
            <span class="cell-trunc cell-prof" title="${esc(svLabel)}">${esc(svLabel)}</span>
            <span class="small-sub cell-trunc cell-prof" title="${esc(profLabel)}">${esc(profLabel)}</span>
          </td>
          <td>${mapText}</td>
          <td>${resText}</td>
          <td>${bagBox}</td>
          <td>${lootHtml}</td>
          <td class="${prxCls}"><span class="cell-trunc cell-proxy" title="${esc(dispProxyRaw)}">${esc(dispProxyShort)}</span></td>
          <td data-xray="${esc(a.id)}" style="cursor:pointer;" title="Bấm để Soi X-Quang chi tiết acc (tọa độ, mục tiêu, tốc độ farm, 25 bước gần nhất)">
            <div>${fsmBadge(rt, a)}${expBadge}</div>
            <span class="small-sub cell-trunc cell-status" title="${esc(dispStatus)}">${esc(dispStatus)}</span>
          </td>
          <td class="col-actions">
            <div class="act-btns">
              ${rt.running
                ? `<button class="btn btn-xs btn-danger-ghost" data-act="stop" data-id="${esc(a.id)}" title="Dừng tab">⏹</button>
                   ${rt.headless
                     ? `<button class="btn btn-xs btn-sky" data-act="open_visible" data-id="${esc(a.id)}" title="Đang treo ngầm Headless — bấm để mở lại CÓ CỬA SỔ cầm tay">👁</button>`
                     : `<button class="btn btn-xs btn-sky" data-act="focus" data-id="${esc(a.id)}" title="Hiện cửa sổ game">👁</button>
                        <button class="btn btn-xs btn-secondary" data-act="start_headless" data-id="${esc(a.id)}" title="Chuyển sang Treo ngầm không cửa sổ (Headless siêu nhẹ VPS)">👻</button>`
                   }`
                : `<button class="btn btn-xs btn-success" data-act="start" data-id="${esc(a.id)}" title="Chạy acc (theo Profile)">▶</button>
                   <button class="btn btn-xs btn-sky" data-act="start_headless" data-id="${esc(a.id)}" title="Treo ngầm không cửa sổ (Headless — 0% GPU, ~0% CPU, siêu nhẹ VPS)">👻</button>`
              }
              <button class="btn btn-xs btn-secondary" data-act="edit" data-id="${esc(a.id)}" title="Sửa cấu hình acc">✎</button>
            </div>
          </td>
        </tr>
      `;
    }).join('');

    // Bind sự kiện dòng
    tbody.querySelectorAll('.row-chk').forEach((chk) => {
      chk.onchange = () => {
        if (chk.checked) ui.selected.add(chk.dataset.id);
        else ui.selected.delete(chk.dataset.id);
        chk.closest('tr').classList.toggle('selected', chk.checked);
        updateBulkBar();
      };
    });
    tbody.querySelectorAll('button[data-act]').forEach((btn) => {
      btn.onclick = async (e) => {
        e.stopPropagation();
        const id = btn.dataset.id;
        const act = btn.dataset.act;
        if (act === 'start') await window.MX.api.StartSelected([id]);
        if (act === 'start_headless') {
          await window.MX.api.StartHeadless([id]);
          window.MX.toast('👻 Đang mở tab Treo ngầm không cửa sổ (Headless VPS)...', 'ok');
        }
        if (act === 'stop') await window.MX.api.StopSelected([id]);
        if (act === 'focus') await window.MX.api.FocusWindow(id);
        if (act === 'open_visible') {
          await window.MX.api.StartVisible([id]);
          window.MX.toast('👁 Đang mở lại tab ở chế độ có hình (cầm tay)...', 'ok');
        }
        if (act === 'edit') openEditAccountModal(id);
      };
    });
    tbody.querySelectorAll('[data-loot]').forEach((el) => {
      el.onclick = (e) => {
        e.stopPropagation();
        openAccountLootModal(el.dataset.loot);
      };
    });
    tbody.querySelectorAll('[data-xray]').forEach((el) => {
      el.onclick = (e) => {
        e.stopPropagation();
        openAccountXRayModal(el.dataset.xray);
      };
    });
    tbody.querySelectorAll('tr[data-id]').forEach((tr) => {
      tr.oncontextmenu = (e) => {
        e.preventDefault();
        const id = tr.dataset.id;
        if (!ui.selected.has(id)) {
          ui.selected.clear();
          ui.selected.add(id);
          renderTableBody();
          updateBulkBar();
        }
        openContextMenu(e.clientX, e.clientY, Array.from(ui.selected));
      };
    });
  }

  function updateBulkBar() {
    const bar = document.getElementById('acc-bulk-bar');
    if (!bar) return;
    if (ui.selected.size > 0) {
      bar.classList.remove('hidden');
      document.getElementById('bulk-count').textContent = `Đã chọn ${ui.selected.size} acc`;
    } else {
      bar.classList.add('hidden');
    }
  }

  function openContextMenu(x, y, ids) {
    const cm = document.getElementById('ctx-menu');
    const items = [
      { label: `▶ Chạy theo Profile (${ids.length} acc)`, act: 'start' },
      { label: `👻 Treo ngầm không cửa sổ (${ids.length} acc)`, act: 'start_headless' },
      { label: `👁 Mở có hình cầm tay (${ids.length} acc)`, act: 'open_visible' },
      { label: '🩻 Soi X-Quang chi tiết acc', act: 'xray' },
      { label: `⏹ Dừng (${ids.length} acc)`, act: 'stop' },
      { label: '↻ Khởi động lại', act: 'restart' },
      { sep: true },
      { label: '🔀 Gửi lệnh: Đổi khu vắng ngay', act: 'switch_zone' },
      { label: '🏠 Gửi lệnh: Ép về nhà cất đồ', act: 'force_store' },
      { label: '💰 Gửi lệnh: Ép đi bán rác', act: 'force_sell' },
      { label: '⏸ Tạm dừng Auto (cầm tay)', act: 'pause' },
      { label: '▶ Tiếp tục Auto', act: 'resume' },
      { label: '👾 Ẩn / Hiện hình ảnh quái', act: 'toggle_mob' },
      { label: '🧍 Ẩn / Hiện nhân vật & phụ kiện', act: 'toggle_char' },
      { label: '🎒 Chuyển Hành trang Dọc / Ngang', act: 'toggle_inv' },
      { label: '⚡ Bật / Tắt đánh xóa anim', act: 'toggle_fastatk' },
      { label: '🌑 Bật/Tắt màn đen VPS', act: 'black' },
      { sep: true },
      { label: '🏷️ Gán Nhóm / Profile / Map / Proxy', act: 'assign' },
      { label: '↺ Reset cờ Hoàn thành (Done)', act: 'reset_done' },
      { label: '🗑 Xóa tài khoản đã chọn', act: 'delete', danger: true },
    ];
    cm.innerHTML = items.map((it) => it.sep
      ? `<div class="ctx-sep"></div>`
      : `<button class="ctx-item ${it.danger ? 'danger' : ''}" data-act="${it.act}">${it.label}</button>`
    ).join('');
    cm.style.left = `${Math.min(x, window.innerWidth - 230)}px`;
    cm.style.top = `${Math.min(y, window.innerHeight - 360)}px`;
    cm.classList.remove('hidden');

    cm.querySelectorAll('.ctx-item').forEach((btn) => {
      btn.onclick = async () => {
        cm.classList.add('hidden');
        await executeBulkAction(btn.dataset.act, ids);
      };
    });
  }

  async function executeBulkAction(act, ids) {
    try {
      if (act === 'start') {
        await window.MX.api.StartSelected(ids);
        window.MX.toast(`Đang mở ${ids.length} tài khoản (giãn nhịp tự động)...`, 'ok');
      } else if (act === 'start_headless') {
        await window.MX.api.StartHeadless(ids);
        window.MX.toast(`👻 Đang mở ${ids.length} tài khoản Treo ngầm (Headless 0 cửa sổ)...`, 'ok');
      } else if (act === 'open_visible') {
        await window.MX.api.StartVisible(ids);
        window.MX.toast(`Đang mở ${ids.length} tài khoản ở chế độ CÓ CỬA SỔ...`, 'ok');
      } else if (act === 'xray') {
        if (ids.length > 0) openAccountXRayModal(ids[0]);
      } else if (act === 'stop') {
        await window.MX.api.StopSelected(ids);
        window.MX.toast(`Đã dừng ${ids.length} tài khoản.`);
      } else if (act === 'restart') {
        await window.MX.api.RestartSelected(ids);
        window.MX.toast(`Đang khởi động lại ${ids.length} tài khoản...`, 'ok');
      } else if (act === 'assign') {
        openBulkAssignModal(ids);
      } else if (act === 'switch_zone') {
        const n = await window.MX.api.SendHotCommand(ids, 'switch_zone', '');
        window.MX.toast(`Đã gửi lệnh đổi khu vắng tới ${n} tab đang chạy.`, 'ok');
      } else if (act === 'force_store') {
        const n = await window.MX.api.SendHotCommand(ids, 'force_store', '');
        window.MX.toast(`Đã ép ${n} tab về nhà cất đồ.`, 'ok');
      } else if (act === 'force_sell') {
        const n = await window.MX.api.SendHotCommand(ids, 'force_sell', '');
        window.MX.toast(`Đã ép ${n} tab đi bán đồ rác.`, 'ok');
      } else if (act === 'pause') {
        const n = await window.MX.api.SendHotCommand(ids, 'pause', '');
        window.MX.toast(`Đã tạm dừng auto trên ${n} tab.`);
      } else if (act === 'resume') {
        const n = await window.MX.api.SendHotCommand(ids, 'resume', '');
        window.MX.toast(`Đã tiếp tục auto trên ${n} tab.`, 'ok');
      } else if (act === 'black') {
        const n = await window.MX.api.SendHotCommand(ids, 'toggle_black_screen', '');
        window.MX.toast(`Đã bật/tắt màn đen VPS cho ${n} tab.`);
      } else if (act === 'toggle_mob') {
        const n = await window.MX.api.SendHotCommand(ids, 'toggle_mob_graphics', '');
        window.MX.toast(`Đã chuyển Ẩn/Hiện hình quái cho ${n} tab.`);
      } else if (act === 'toggle_char') {
        const n = await window.MX.api.SendHotCommand(ids, 'toggle_char_graphics', '');
        window.MX.toast(`Đã chuyển Ẩn/Hiện nhân vật & phụ kiện cho ${n} tab.`);
      } else if (act === 'toggle_inv') {
        const n = await window.MX.api.SendHotCommand(ids, 'toggle_inventory_mode', '');
        window.MX.toast(`Đã chuyển Hành trang Dọc / Ngang cho ${n} tab.`);
      } else if (act === 'toggle_fastatk') {
        const n = await window.MX.api.SendHotCommand(ids, 'toggle_fast_attack', '');
        window.MX.toast(`Đã bật/tắt Đánh quái xóa anim cho ${n} tab.`);
      } else if (act === 'reset_done') {
        await window.MX.api.ResetDoneAccounts(ids);
        window.MX.toast(`Đã reset trạng thái Done cho ${ids.length} tài khoản.`, 'ok');
      } else if (act === 'delete') {
        if (!confirm(`Xóa vĩnh viễn ${ids.length} tài khoản đã chọn?`)) return;
        await window.MX.api.DeleteAccounts(ids);
        ids.forEach((id) => ui.selected.delete(id));
        const chkAll = document.getElementById('acc-check-all');
        if (chkAll) chkAll.checked = false;
        window.MX.toast(`Đã xóa sạch ${ids.length} tài khoản & dữ liệu liên quan.`);
        await window.MX.refreshBootstrap();
      }
    } catch (err) {
      window.MX.toast(String(err), 'err');
    }
  }

  function serverOptionsHtml(selectedId) {
    return (window.MX.state.servers || []).map((s) =>
      `<option value="${window.MX.esc(s.id)}" ${s.id === selectedId ? 'selected' : ''}>${window.MX.esc(s.name)} (${window.MX.esc(s.host)})</option>`
    ).join('');
  }

  function profileOptionsHtml(selectedId) {
    return (window.MX.state.profiles || []).map((p) =>
      `<option value="${window.MX.esc(p.id)}" ${p.id === selectedId ? 'selected' : ''}>${window.MX.esc(p.name)}</option>`
    ).join('');
  }

  function mapOptionsHtml(selectedId) {
    return (window.MX.state.map_presets || []).map((m) =>
      `<option value="${m.id}" ${m.id === selectedId ? 'selected' : ''}>${window.MX.esc(m.name)}</option>`
    ).join('');
  }

  function proxyOptionsHtml(selectedId) {
    const privOn = window.MX.privacy.isOn();
    const base = [
      `<option value="auto" ${selectedId === 'auto' ? 'selected' : ''}>Tự động phân bổ (theo Proxy Manager)</option>`,
      `<option value="home" ${selectedId === 'home' ? 'selected' : ''}>Home (Dùng IP gốc máy)</option>`,
      `<option value="custom" ${selectedId === 'custom' ? 'selected' : ''}>🌐 Proxy riêng (nhập trực tiếp bên dưới)</option>`,
    ];
    (window.MX.state.proxies || []).forEach((p, idx) => {
      const lbl = privOn ? `Proxy #${idx + 1} (•••.•••.•••:••• · ${p.status})` : `${p.host}:${p.port} (${p.status})`;
      base.push(`<option value="${window.MX.esc(p.id)}" ${p.id === selectedId ? 'selected' : ''}>${window.MX.esc(lbl)}</option>`);
    });
    return base.join('');
  }

  function renderCustomProxyBlock(prefix, customVal) {
    const esc = window.MX.esc;
    const privOn = window.MX.privacy.isOn();
    return `
      <div class="prx-custom-box">
        <div class="prx-custom-row">
          <input type="${privOn ? 'password' : 'text'}" id="${prefix}-custom" value="${esc(customVal || '')}" placeholder="Dán proxy riêng mọi định dạng: ip:port | ip:port:user:pass | user:pass@ip:port | socks5://..." />
          <button type="button" class="btn btn-sky btn-sm" id="${prefix}-chk">⚡ Check Proxy</button>
        </div>
        <div id="${prefix}-chk-res" class="prx-check-status text-dim">
          Nhận đủ định dạng (kể cả dán ngược <code>user:pass:ip:port</code>, <code>ip:port@user:pass</code>, dấu <code>: | , ; space</code>) · Bấm <b>Check Proxy</b> sẽ tự dò HTTP/SOCKS5!
        </div>
      </div>
    `;
  }

  function bindCustomProxyBlock(prefix, selectId, getServerId, onAutoCheckCb) {
    const sel = document.getElementById(selectId);
    const inp = document.getElementById(`${prefix}-custom`);
    const btn = document.getElementById(`${prefix}-chk`);
    const resEl = document.getElementById(`${prefix}-chk-res`);
    if (!sel || !inp || !btn || !resEl) return;

    inp.oninput = () => {
      if (inp.value.trim() !== '') {
        sel.value = 'custom';
        if (onAutoCheckCb) onAutoCheckCb();
      }
    };

    sel.onchange = () => {
      if (sel.value !== 'custom' && sel.value !== 'auto' && sel.value !== 'home') {
        inp.value = '';
      } else if (sel.value === 'auto' || sel.value === 'home') {
        inp.value = '';
      }
      if (onAutoCheckCb) onAutoCheckCb();
    };

    btn.onclick = async () => {
      let raw = inp.value.trim();
      if (!raw) {
        // Nếu ô riêng trống nhưng dropdown đang chọn 1 proxy cụ thể từ Proxy Manager -> check luôn proxy đó!
        const picked = (window.MX.state.proxies || []).find((p) => p.id === sel.value);
        if (picked) {
          raw = picked.raw || (picked.username ? `${picked.type}://${picked.username}:${picked.password}@${picked.host}:${picked.port}` : `${picked.type}://${picked.host}:${picked.port}`);
        }
      }
      if (!raw) {
        resEl.className = 'prx-check-status text-gold';
        resEl.textContent = '⚠ Hãy dán chuỗi proxy vào ô trên (hoặc chọn 1 proxy cụ thể) rồi bấm Check nhé!';
        return;
      }

      btn.disabled = true;
      btn.textContent = '⏳ Đang check...';
      resEl.className = 'prx-check-status text-sky';
      resEl.textContent = 'Đang kiểm tra kết nối thật tới máy chủ NRO (tự dò HTTP / SOCKS5)...';
      try {
        const svId = typeof getServerId === 'function' ? getServerId() : '';
        const r = await window.MX.api.CheckProxyString(raw, svId);
        if (r && r.ok) {
          resEl.className = 'prx-check-status text-green';
          resEl.textContent = `✅ ${window.MX.privacy.maskText(r.message)}`;
          if (inp.value.trim() !== '' && r.normalized) {
            inp.value = r.normalized;
            sel.value = 'custom';
          }
        } else {
          resEl.className = 'prx-check-status text-red';
          resEl.textContent = `❌ ${window.MX.privacy.maskText((r && r.message) || 'Proxy DIE hoặc sai định dạng')}`;
        }
      } catch (err) {
        resEl.className = 'prx-check-status text-red';
        resEl.textContent = `❌ Lỗi check proxy: ${String(err)}`;
      } finally {
        btn.disabled = false;
        btn.textContent = '⚡ Check Proxy';
      }
    };
  }

  function openBulkAddModal() {
    const st = window.MX.state;
    const defSv = st.servers[0]?.id || 'sv1';
    const defProf = st.profiles[0]?.id || 'prof_tdlt';
    const privOn = window.MX.privacy.isOn();
    const body = `
      <div class="field">
        <label>Dán danh sách tài khoản (mỗi dòng 1 acc — hỗ trợ <code>user|pass</code>, <code>user|pass|proxy</code> hoặc <code>user|pass|sv|map|zone|nhóm|proxy</code>)</label>
        <textarea id="m-add-raw" class="${privOn ? 'privacy-mask-text' : ''}" rows="6" placeholder="taikhoan01|matkhau01&#10;taikhoan02|matkhau02|103.1.2.3:8080:u:p&#10;taikhoan03|matkhau03|7|1|0|TD-Aru"></textarea>
      </div>
      <div class="form-grid">
        <div class="field"><label>Nhóm / Tag mặc định</label><input type="text" id="m-add-tag" placeholder="Vd: Dan-TD-1" /></div>
        <div class="field"><label>Server mặc định</label><select id="m-add-sv">${serverOptionsHtml(defSv)}</select></div>
        <div class="field"><label>Farm Profile</label><select id="m-add-prof">${profileOptionsHtml(defProf)}</select></div>
        <div class="field"><label>Map farm</label><select id="m-add-map">${mapOptionsHtml(-1)}</select></div>
        <div class="field"><label>Chiến thuật Khu</label>
          <select id="m-add-zmode">
            <option value="profile">Theo Profile</option>
            <option value="empty">Tự tìm khu vắng nhất</option>
            <option value="fixed">Khu cố định</option>
          </select>
        </div>
        <div class="field"><label>Số Khu (nếu cố định)</label><input type="number" id="m-add-zid" value="0" min="0" max="99" /></div>
        <div class="field" style="grid-column:span 2;">
          <label>Proxy (chọn sẵn hoặc dán proxy riêng cho acc ở ngay bên dưới)</label>
          <select id="m-add-prx">${proxyOptionsHtml('auto')}</select>
          ${renderCustomProxyBlock('m-add-prx', '')}
        </div>
      </div>
    `;
    window.MX.modal.open('＋ Thêm Tài Khoản Hàng Loạt', body, [
      { label: 'Hủy', cls: 'btn-secondary', onClick: () => window.MX.modal.close() },
      {
        label: 'Thêm Vào Bảng',
        cls: 'btn-primary',
        onClick: async () => {
          try {
            const customPrx = document.getElementById('m-add-prx-custom').value.trim();
            const res = await window.MX.api.AddAccountsBulk({
              raw_text: document.getElementById('m-add-raw').value,
              tag: document.getElementById('m-add-tag').value,
              server_id: document.getElementById('m-add-sv').value,
              profile_id: document.getElementById('m-add-prof').value,
              map_id: parseInt(document.getElementById('m-add-map').value, 10),
              zone_mode: document.getElementById('m-add-zmode').value,
              zone_id: parseInt(document.getElementById('m-add-zid').value, 10) || 0,
              proxy_id: customPrx ? 'custom' : document.getElementById('m-add-prx').value,
              custom_proxy: customPrx,
            });
            window.MX.modal.close();
            if (res.added > 0) {
              // Đảm bảo acc mới nhập hiện ngay trong bảng (không bị ẩn bởi bộ lọc/nhóm cũ)
              ui.filter = 'all';
              ui.search = '';
              ui.tagFilter = '';
              ui.serverFilter = '';
              ui.profileFilter = '';
              const sEl = document.getElementById('acc-search');
              if (sEl) sEl.value = '';
              document.querySelectorAll('#acc-chips .chip').forEach((c) => c.classList.toggle('active', c.dataset.f === 'all'));
            }
            window.MX.toast(`Đã thêm ${res.added} acc (bỏ qua ${res.skipped} dòng trùng/lỗi).`, 'ok');
            await window.MX.refreshBootstrap();
          } catch (err) {
            window.MX.toast(String(err), 'err');
          }
        }
      }
    ]);
    bindCustomProxyBlock('m-add-prx', 'm-add-prx', () => document.getElementById('m-add-sv')?.value || '');
  }

  function openEditAccountModal(id) {
    const row = (window.MX.state.rows || []).find((r) => r.account.id === id);
    if (!row) return;
    const a = row.account;
    const esc = window.MX.esc;
    const priv = window.MX.privacy;
    const privOn = priv.isOn();
    const initPrxId = a.custom_proxy ? 'custom' : (a.proxy_id || 'auto');
    const body = `
      <div class="form-grid">
        <div class="field"><label>Tên đăng nhập</label><input type="${privOn ? 'password' : 'text'}" id="e-user" value="${esc(a.username)}" /></div>
        <div class="field"><label>Mật khẩu mới (để trống = giữ nguyên)</label><input type="password" id="e-pass" placeholder="••••••••" /></div>
        <div class="field"><label>Nhóm / Tag</label><input type="${privOn ? 'password' : 'text'}" id="e-tag" value="${esc(a.tag || '')}" /></div>
        <div class="field"><label>Server</label><select id="e-sv">${serverOptionsHtml(a.server_id)}</select></div>
        <div class="field"><label>Farm Profile</label><select id="e-prof">${profileOptionsHtml(a.profile_id)}</select></div>
        <div class="field"><label>Map farm</label><select id="e-map">${mapOptionsHtml(a.map_id)}</select></div>
        <div class="field"><label>Chiến thuật Khu</label>
          <select id="e-zmode">
            <option value="profile" ${a.zone_mode === 'profile' ? 'selected' : ''}>Theo Profile</option>
            <option value="empty" ${a.zone_mode === 'empty' ? 'selected' : ''}>Tự tìm khu vắng nhất</option>
            <option value="fixed" ${a.zone_mode === 'fixed' ? 'selected' : ''}>Khu cố định</option>
          </select>
        </div>
        <div class="field"><label>Số Khu cố định</label><input type="number" id="e-zid" value="${a.zone_id || 0}" min="0" max="99" /></div>
        <div class="field"><label>Tọa độ X (Up Đệ)</label><input type="number" id="e-cx" value="${a.coord_x || 0}" /></div>
        <div class="field"><label>Tọa độ Y (Up Đệ)</label><input type="number" id="e-cy" value="${a.coord_y || 0}" /></div>
        <div class="field" style="grid-column:span 2;">
          <label>Proxy (chọn sẵn hoặc nhập proxy riêng cho acc này bên dưới)</label>
          <select id="e-prx">${proxyOptionsHtml(initPrxId)}</select>
          ${renderCustomProxyBlock('e-prx', a.custom_proxy || '')}
        </div>
        <div class="field" style="grid-column:span 2;"><label>Ghi chú</label><input type="${privOn ? 'password' : 'text'}" id="e-note" value="${esc(a.note || '')}" /></div>
      </div>
    `;
    window.MX.modal.open(`✎ Sửa Tài Khoản: ${priv.maskUser(a.username, a.id)}`, body, [
      { label: 'Hủy', cls: 'btn-secondary', onClick: () => window.MX.modal.close() },
      {
        label: 'Lưu Thay Đổi',
        cls: 'btn-primary',
        onClick: async () => {
          try {
            const customPrx = document.getElementById('e-prx-custom').value.trim();
            await window.MX.api.UpdateAccount({
              ...a,
              username: document.getElementById('e-user').value.trim(),
              password: document.getElementById('e-pass').value,
              tag: document.getElementById('e-tag').value.trim(),
              server_id: document.getElementById('e-sv').value,
              profile_id: document.getElementById('e-prof').value,
              map_id: parseInt(document.getElementById('e-map').value, 10),
              zone_mode: document.getElementById('e-zmode').value,
              zone_id: parseInt(document.getElementById('e-zid').value, 10) || 0,
              coord_x: parseInt(document.getElementById('e-cx').value, 10) || 0,
              coord_y: parseInt(document.getElementById('e-cy').value, 10) || 0,
              proxy_id: customPrx ? 'custom' : document.getElementById('e-prx').value,
              custom_proxy: customPrx,
              note: document.getElementById('e-note').value.trim(),
            });
            window.MX.modal.close();
            window.MX.toast('Đã lưu tài khoản.', 'ok');
            await window.MX.refreshBootstrap();
          } catch (err) {
            window.MX.toast(String(err), 'err');
          }
        }
      }
    ]);
    bindCustomProxyBlock('e-prx', 'e-prx', () => document.getElementById('e-sv')?.value || '');
  }

  function openBulkAssignModal(ids) {
    const body = `
      <div class="text-dim">Tick vào trường nào bạn muốn ghi đè cho <b>${ids.length} tài khoản</b> đã chọn:</div>
      <div class="form-grid">
        <div class="field">
          <label class="check-row"><input type="checkbox" id="ba-chk-tag" /> Đổi Nhóm / Tag</label>
          <input type="text" id="ba-tag" placeholder="Vd: Dan-SKH-1" />
        </div>
        <div class="field">
          <label class="check-row"><input type="checkbox" id="ba-chk-sv" /> Đổi Server</label>
          <select id="ba-sv">${serverOptionsHtml('')}</select>
        </div>
        <div class="field">
          <label class="check-row"><input type="checkbox" id="ba-chk-prof" /> Đổi Farm Profile (áp dụng live!)</label>
          <select id="ba-prof">${profileOptionsHtml('')}</select>
        </div>
        <div class="field">
          <label class="check-row"><input type="checkbox" id="ba-chk-map" /> Đổi Map</label>
          <select id="ba-map">${mapOptionsHtml(-1)}</select>
        </div>
        <div class="field">
          <label class="check-row"><input type="checkbox" id="ba-chk-zone" /> Đổi Khu</label>
          <div style="display:flex;gap:6px;">
            <select id="ba-zmode" style="flex:1;"><option value="profile">Theo Profile</option><option value="empty">Khu vắng</option><option value="fixed">Cố định</option></select>
            <input type="number" id="ba-zid" value="0" style="width:70px;" />
          </div>
        </div>
        <div class="field" style="grid-column:span 2;">
          <label class="check-row"><input type="checkbox" id="ba-chk-prx" /> Đổi Proxy</label>
          <select id="ba-prx">${proxyOptionsHtml('auto')}</select>
          ${renderCustomProxyBlock('ba-prx', '')}
        </div>
      </div>
    `;
    window.MX.modal.open(`🏷️ Gán Cấu Hình Hàng Loạt (${ids.length} acc)`, body, [
      { label: 'Hủy', cls: 'btn-secondary', onClick: () => window.MX.modal.close() },
      {
        label: 'Áp Dụng Ngay',
        cls: 'btn-primary',
        onClick: async () => {
          try {
            const customPrx = document.getElementById('ba-prx-custom').value.trim();
            await window.MX.api.BulkAssignAccounts({
              ids,
              set_tag: document.getElementById('ba-chk-tag').checked,
              tag: document.getElementById('ba-tag').value,
              set_server: document.getElementById('ba-chk-sv').checked,
              server_id: document.getElementById('ba-sv').value,
              set_profile: document.getElementById('ba-chk-prof').checked,
              profile_id: document.getElementById('ba-prof').value,
              set_map: document.getElementById('ba-chk-map').checked,
              map_id: parseInt(document.getElementById('ba-map').value, 10),
              set_zone: document.getElementById('ba-chk-zone').checked,
              zone_mode: document.getElementById('ba-zmode').value,
              zone_id: parseInt(document.getElementById('ba-zid').value, 10) || 0,
              set_proxy: document.getElementById('ba-chk-prx').checked,
              proxy_id: customPrx ? 'custom' : document.getElementById('ba-prx').value,
              custom_proxy: customPrx,
            });
            window.MX.modal.close();
            window.MX.toast(`Đã cập nhật ${ids.length} tài khoản!`, 'ok');
            await window.MX.refreshBootstrap();
          } catch (err) {
            window.MX.toast(String(err), 'err');
          }
        }
      }
    ]);
    bindCustomProxyBlock('ba-prx', 'ba-prx', () => document.getElementById('ba-sv')?.value || '', () => {
      const chk = document.getElementById('ba-chk-prx');
      if (chk) chk.checked = true;
    });
  }

  function openAccountLootModal(id) {
    const row = (window.MX.state.rows || []).find((r) => r.account.id === id);
    if (!row) return;
    const esc = window.MX.esc;
    const accRep = ((window.MX.state.loot_report && window.MX.state.loot_report.accounts) || []).find((x) => x.account_id === id);
    const sets = (accRep && accRep.sets && accRep.sets.length > 0)
      ? accRep.sets
      : ((window.MX.state.loot_report && window.MX.state.loot_report.global_sets) || []).map((s) => ({
          set_name: s.set_name, short_name: s.short_name, planet: s.planet, counts: [0, 0, 0, 0, 0], code: '0-0-0-0-0', total_items: 0
        }));
    const fmtCode = (code) => window.MX.LootTab && window.MX.LootTab.renderCodeDigits
      ? window.MX.LootTab.renderCodeDigits(code)
      : esc(code || '0-0-0-0-0');

    const matrixHtml = `
      <div class="small-sub" style="margin-bottom:4px;">Quy ước 5 số: <b>Áo - Quần - Găng - Giày - Rada</b> (0 = chưa có)</div>
      <table class="data-table">
        <thead><tr><th>Set SKH</th><th>Mã 5 món</th><th>Áo</th><th>Quần</th><th>Găng</th><th>Giày</th><th>Rada</th></tr></thead>
        <tbody>
          ${sets.map((s) => {
            const c = s.counts || [0, 0, 0, 0, 0];
            return `
              <tr>
                <td><b>${esc(s.set_name)}</b> <span class="badge ${s.planet === 'TĐ' ? 'b-td' : (s.planet === 'NM' ? 'b-nm' : (s.planet === 'XD' ? 'b-xd' : 'b-buff'))}">${s.planet === 'ALL' ? 'Chung' : esc(s.planet)}</span></td>
                <td>${fmtCode(s.code || '0-0-0-0-0')}</td>
                ${c.map((n) => `<td class="${n > 0 ? 'text-gold' : 'text-dim'}" style="font-weight:${n > 0 ? '700' : '400'};">${n}</td>`).join('')}
              </tr>
            `;
          }).join('')}
        </tbody>
      </table>
    `;

    const items = row.runtime.loot || [];
    const itemsHtml = items.length === 0
      ? `<div class="text-dim" style="margin-top:6px;">Chưa có món đồ SKH / Sao nào trong hành trang &amp; rương nhà.</div>`
      : `<div style="font-weight:700;color:#fde68a;margin-top:6px;">Chi tiết từng món đang giữ (${items.length})</div>
        <table class="data-table"><thead><tr><th>Vị trí</th><th>Món</th><th>Loại</th><th>Set SKH</th><th>Sao</th></tr></thead><tbody>` +
        items.map((it) => `
          <tr>
            <td><span class="badge ${it.location === 'box' ? 'b-store' : 'b-sell'}">${it.location === 'box' ? 'Rương nhà' : 'Hành trang'}</span></td>
            <td><b>${esc(it.name)}</b></td>
            <td>${esc(it.slot)}</td>
            <td class="text-gold">${it.is_skh ? 'Set ' + esc(it.set_name) : '—'}</td>
            <td class="text-sky">${it.stars > 0 ? it.stars + ' ★' : '—'}</td>
          </tr>
        `).join('') + `</tbody></table>`;

    window.MX.modal.open(`🏆 Bảng SKH 0-0-0-0-0 — ${window.MX.privacy.maskUser(row.account.username, row.account.id)}`, matrixHtml + itemsHtml, [
      { label: 'Đóng', cls: 'btn-primary', onClick: () => window.MX.modal.close() }
    ]);
  }

  function buildXRayInnerHtml(row) {
    const a = row.account;
    const rt = row.runtime;
    const esc = window.MX.esc;
    const priv = window.MX.privacy;
    const dispChar = priv.maskChar(rt.char_name, 'Chưa vào game');
    const modeLabel = rt.running ? (rt.headless ? '👻 Treo ngầm Headless (0 cửa sổ)' : '👁 Có cửa sổ') : '● Đang nghỉ';
    const expTxt = (typeof rt.last_exp_sec === 'number' && rt.last_exp_sec >= 0)
      ? (rt.last_exp_sec <= 15
          ? `<span class="text-green">⚡ Đang ăn TN sống (${rt.last_exp_sec}s trước)</span>`
          : (rt.last_exp_sec <= 45
              ? `<span class="text-gold">⏳ ${rt.last_exp_sec}s chưa thấy TN</span>`
              : `<span class="text-red">⚠ ${rt.last_exp_sec}s không ăn TN (Watchdog đang theo dõi)</span>`))
      : `<span class="text-dim">Chưa bắt đầu farm</span>`;
    const events = Array.isArray(rt.recent_events) ? rt.recent_events : [];
    const logsHtml = events.length === 0
      ? `<div class="text-dim" style="padding:10px;">Chưa có lịch sử hoạt động trong phiên này.</div>`
      : events.map((line) => `<div style="padding:3px 0;border-bottom:1px solid rgba(255,255,255,0.06);font-family:Consolas,monospace;font-size:11.5px;">${esc(priv.maskText(line))}</div>`).join('');

    return `
      <div class="form-grid" style="margin-bottom:10px;">
        <div class="card" style="padding:10px;margin:0;">
          <div style="font-weight:700;color:#fde68a;margin-bottom:6px;">📡 Trạng Thái Tiến Trình &amp; Vị Trí</div>
          <div class="small-sub"><b>Chế độ:</b> ${modeLabel} ${rt.pid ? `· PID ${rt.pid}` : ''} ${rt.ram_mb ? `· ${rt.ram_mb} MB` : ''}</div>
          <div class="small-sub"><b>Nhân vật:</b> ${esc(dispChar)} (${esc(rt.planet || '—')}) · <b>Server:</b> ${esc(row.server_name || a.server_id)}</div>
          <div class="small-sub"><b>Bản đồ:</b> ${esc(rt.map_name || '—')} ${rt.in_private_map ? '<span class="badge b-buff">🎫 Map Riêng</span>' : `(K${rt.zone_id} · ${rt.zone_players}/${rt.zone_max || 15})`}</div>
          <div class="small-sub"><b>Tọa độ:</b> (${rt.cx || 0}, ${rt.cy || 0}) · <b>Proxy:</b> ${esc(priv.maskProxyLabel(row.proxy_label))}</div>
          <div class="small-sub"><b>Trạng thái FSM:</b> ${fsmBadge(rt, a)} — ${esc(priv.maskText(rt.status_text || '—'))}</div>
        </div>
        <div class="card" style="padding:10px;margin:0;">
          <div style="font-weight:700;color:#93c5fd;margin-bottom:6px;">⚔️ Nhịp Chiến Đấu &amp; Buff (Không Cần Mở Hình)</div>
          <div class="small-sub"><b>Nhịp ăn TN:</b> ${expTxt}</div>
          <div class="small-sub"><b>Tốc độ farm:</b> <b class="text-green">${rt.hits_per_min || 0} nhịp TN/phút</b> (tổng phiên: ${rt.total_hits || 0})</div>
          <div class="small-sub"><b>Mục tiêu:</b> ${esc(rt.target_mob || 'Đang quét quái...')}</div>
          <div class="small-sub"><b>Chiêu đang dùng:</b> <span class="text-sky">${esc(rt.active_skill || '—')}</span></div>
          <div class="small-sub"><b>Buff:</b> TDLT ${rt.has_tdlt_active ? '✓' : '✗'} (${rt.tdlt_minutes || 0}p) · Cỏ ${rt.has_lucky_grass ? '✓' : '✗'} ${rt.ticket_remain ? `· 🎫 Vé: <b class="text-gold">${esc(rt.ticket_remain)}</b>` : ''}</div>
          <div class="small-sub"><b>Túi / Rương:</b> T ${rt.bag_used || 0}/${rt.bag_max || 0} · R ${rt.box_used || 0}/${rt.box_max || 0} · <b>Ngọc:</b> 💎${rt.gem || 0}/${rt.gem_lock || 0}</div>
        </div>
      </div>
      <div style="font-weight:700;color:#cbd5e1;margin-bottom:6px;">📜 25 Bước Hoạt Động Gần Nhất Của Riêng Acc Này (Tự cập nhật Live)</div>
      <div id="xray-log-scroll" style="max-height:240px;overflow:auto;background:#0b101b;border:1px solid var(--border);border-radius:8px;padding:8px 10px;">
        ${logsHtml}
      </div>
    `;
  }

  function refreshOpenXRayIfNeeded() {
    if (!ui.xrayId) return;
    const backdrop = document.getElementById('modal-backdrop');
    const rootEl = document.getElementById('xray-live-root');
    if (!backdrop || backdrop.classList.contains('hidden') || !rootEl) {
      ui.xrayId = '';
      return;
    }
    const row = (window.MX.state.rows || []).find((r) => r.account.id === ui.xrayId);
    if (!row) {
      ui.xrayId = '';
      return;
    }
    const prevScroll = document.getElementById('xray-log-scroll')?.scrollTop || 0;
    rootEl.innerHTML = buildXRayInnerHtml(row);
    const nextScrollEl = document.getElementById('xray-log-scroll');
    if (nextScrollEl) nextScrollEl.scrollTop = prevScroll;
  }

  function openAccountXRayModal(id) {
    const row = (window.MX.state.rows || []).find((r) => r.account.id === id);
    if (!row) return;
    ui.xrayId = id;
    const a = row.account;
    const rt = row.runtime;
    const dispUser = window.MX.privacy.maskUser(a.username, a.id);
    const body = `<div id="xray-live-root">${buildXRayInnerHtml(row)}</div>`;

    const footBtns = [];
    if (rt.running && rt.headless) {
      footBtns.push({
        label: '👁 Mở có hình cầm tay',
        cls: 'btn-sky',
        onClick: async () => {
          ui.xrayId = '';
          window.MX.modal.close();
          await window.MX.api.StartVisible([id]);
          window.MX.toast('👁 Đang mở lại tab ở chế độ có hình...', 'ok');
        }
      });
    } else if (rt.running && !rt.headless) {
      footBtns.push({
        label: '👻 Chuyển Treo ngầm',
        cls: 'btn-secondary',
        onClick: async () => {
          ui.xrayId = '';
          window.MX.modal.close();
          await window.MX.api.StartHeadless([id]);
          window.MX.toast('👻 Đang chuyển sang Treo ngầm Headless...', 'ok');
        }
      });
    }
    footBtns.push({
      label: 'Đóng',
      cls: 'btn-primary',
      onClick: () => {
        ui.xrayId = '';
        window.MX.modal.close();
      }
    });
    window.MX.modal.open(`🩻 Soi X-Quang Live — ${dispUser}`, body, footBtns);
  }

  function openExportModal() {
    const ids = Array.from(ui.selected);
    const privOn = window.MX.privacy.isOn();
    const body = privOn
      ? `<div class="card text-gold">🙈 Đang bật chế độ <b>Che TK</b> — tạm khóa xuất danh sách để tránh lộ tài khoản / mật khẩu khi quay demo hoặc chụp màn hình. Tắt nút <b>Đang Che TK</b> trên thanh công cụ nếu bạn cần xuất thật nhé!</div>`
      : `
      <div class="check-grid">
        <label class="check-row"><input type="checkbox" id="ex-only-skh" /> Chỉ xuất acc có đồ SKH</label>
        <label class="check-row"><input type="checkbox" id="ex-pass" /> Kèm mật khẩu</label>
      </div>
      <textarea id="ex-out" rows="9" readonly placeholder="Bấm Tạo Danh Sách để xuất..."></textarea>
    `;
    if (privOn) {
      window.MX.modal.open('⤓ Xuất Danh Sách Tài Khoản (Đang Che TK)', body, [
        { label: 'Đóng', cls: 'btn-primary', onClick: () => window.MX.modal.close() }
      ]);
      return;
    }
    window.MX.modal.open(`⤓ Xuất Danh Sách Tài Khoản (${ids.length > 0 ? ids.length + ' đã chọn' : 'Tất cả'})`, body, [
      {
        label: 'Tạo Danh Sách',
        cls: 'btn-secondary',
        onClick: async () => {
          const txt = await window.MX.api.ExportAccountsText(
            ids,
            document.getElementById('ex-only-skh').checked,
            document.getElementById('ex-pass').checked
          );
          document.getElementById('ex-out').value = txt || '(Không có tài khoản nào thỏa điều kiện)';
        }
      },
      {
        label: 'Copy Clipboard',
        cls: 'btn-primary',
        onClick: () => {
          const val = document.getElementById('ex-out').value;
          navigator.clipboard?.writeText(val);
          window.MX.toast('Đã copy danh sách ra Clipboard!', 'ok');
        }
      }
    ]);
  }

  return { render };
})();
