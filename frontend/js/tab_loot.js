// Tab 3: Kho Đồ SKH & Ma Trận Ghép Set 5 Món (Áo - Quần - Găng - Giày - Rada)
window.MX = window.MX || {};

window.MX.LootTab = (function () {
  const SLOTS = ['Áo', 'Quần', 'Găng', 'Giày', 'Rada'];
  let searchQ = '';
  let planetFilter = 'all'; // 'all' | 'TĐ' | 'NM' | 'XD' | 'owned'

  function renderCodeDigits(code) {
    const parts = String(code || '0-0-0-0-0').split('-');
    return `<span class="skh-code" title="Áo - Quần - Găng - Giày - Rada">` +
      parts.map((d) => `<span class="${Number(d) > 0 ? 'd-on' : 'd-0'}">${d}</span>`).join('<span class="d-sep">-</span>') +
      `</span>`;
  }

  function render() {
    const root = document.getElementById('tab-loot');
    const st = window.MX.state;
    const rep = st.loot_report || { global_sets: [], accounts: [], total_skh: 0, total_stars: 0, total_complete_sets: 0 };
    const hist = st.loot_history || [];
    const esc = window.MX.esc;
    const q = searchQ.trim().toLowerCase();

    const allSets = rep.global_sets || [];
    const filteredSets = allSets.filter((s) => {
      if (planetFilter === 'owned' && !(s.total_items > 0)) return false;
      if ((planetFilter === 'TĐ' || planetFilter === 'NM' || planetFilter === 'XD' || planetFilter === 'ALL') && s.planet !== planetFilter) return false;
      if (!q) return true;
      const hay = `${s.set_name} ${s.short_name || ''} ${s.planet} ${s.code || ''}`.toLowerCase();
      return hay.includes(q);
    });

    const planetCls = (pl) => pl === 'TĐ' ? 'b-td' : (pl === 'NM' ? 'b-nm' : (pl === 'XD' ? 'b-xd' : 'b-buff'));
    const planetLbl = (pl) => pl === 'ALL' ? 'Chung' : pl;

    const setsHtml = filteredSets.length === 0
      ? `<div class="card text-dim">Không có bộ SKH nào khớp bộ lọc hiện tại.</div>`
      : `<div class="set-grid">` + filteredSets.map((s) => {
          const hasAny = (s.total_items || 0) > 0;
          const shortLabel = s.short_name && s.short_name !== s.set_name ? ` (${s.short_name})` : '';
          return `
            <div class="set-card ${s.complete_sets > 0 ? 'complete' : (hasAny ? 'has-any' : 'empty-set')}">
              <div class="set-head">
                <div>
                  <b>Set ${esc(s.set_name)}${esc(shortLabel)}</b>
                  <span class="badge ${planetCls(s.planet)}">${esc(planetLbl(s.planet))}</span>
                </div>
                <div style="display:flex;align-items:center;gap:6px;">
                  ${renderCodeDigits(s.code || '0-0-0-0-0')}
                  ${s.complete_sets > 0
                    ? `<span class="badge b-done">🏆 ×${s.complete_sets}</span>`
                    : `<span class="badge ${hasAny ? 'b-sell' : 'b-idle'}">${s.have_slots || 0}/5</span>`}
                </div>
              </div>
              <div class="slot-row">
                ${SLOTS.map((sl, idx) => {
                  const cnt = (s.counts && typeof s.counts[idx] === 'number') ? s.counts[idx] : ((s.slots && s.slots[sl]) || 0);
                  const owners = (s.slot_owners && s.slot_owners[sl]) || [];
                  const dispOwners = owners.map((u) => window.MX.privacy.maskUser(u));
                  const tip = dispOwners.length ? `Acc giữ (${cnt}): ${dispOwners.join(', ')}` : 'Chưa có (0)';
                  return `<div class="slot-box ${cnt > 0 ? 'has' : ''}" title="${esc(tip)}">${sl}<br/><span class="slot-num">${cnt}</span></div>`;
                }).join('')}
              </div>
              <div class="small-sub" style="margin-top:6px;">
                ${(s.missing_slots && s.missing_slots.length)
                  ? `Thiếu: <span class="${hasAny ? 'text-gold' : 'text-dim'}">${esc(s.missing_slots.join(', '))}</span>`
                  : `<span class="text-green">Đủ trọn bộ 5 món trên dàn acc!</span>`}
              </div>
            </div>
          `;
        }).join('') + `</div>`;

    const filteredAccs = (rep.accounts || []).filter((a) => {
      if (planetFilter === 'owned' && !(a.skh_count > 0 || a.star_count > 0)) return false;
      if ((planetFilter === 'TĐ' || planetFilter === 'NM' || planetFilter === 'XD') && a.planet && a.planet !== planetFilter) return false;
      if (!q) return true;
      const setTxt = (a.sets || []).map((s) => `${s.set_name} ${s.short_name || ''} ${s.code || ''}`).join(' ');
      const itemTxt = (a.items || []).map((i) => `${i.name} ${i.set_name} ${i.slot}`).join(' ');
      const hay = `${a.username} ${a.server_name} ${a.planet || ''} ${setTxt} ${itemTxt}`.toLowerCase();
      return hay.includes(q);
    });

    const ownedSetCount = allSets.filter((s) => s.total_items > 0).length;

    root.innerHTML = `
      <div class="toolbar">
        <div class="toolbar-group">
          <h3 style="color:#fde68a;">🏆 Danh Sách Set Kích Hoạt (Áo - Quần - Găng - Giày - Rada)</h3>
          <span class="badge b-tag" title="Ví dụ: có 1 Áo + 1 Giày = 1-0-0-1-0; chưa có món nào = 0-0-0-0-0">Quy ước: 1-0-0-1-0 = 1 Áo, 1 Giày</span>
        </div>
        <div class="toolbar-group">
          <input type="text" id="loot-search" value="${esc(searchQ)}" placeholder="🔍 Tìm Set / Acc (vd SGK, Kirin, 1-0-0)..." style="width:270px;" />
        </div>
      </div>

      <div class="chip-bar">
        ${[
          ['all', `Tất cả (${allSets.length} set)`],
          ['TĐ', 'Trái Đất (SGK · Kirin · TXH · Kaio)'],
          ['NM', 'Namek (Ốc Tiêu · Daimao · Picolo · Nail)'],
          ['XD', 'Xayda (Kakarot · Cadic M · Cadic · Nappa)'],
          ['ALL', 'Dùng chung (Gohan · Champa)'],
          ['owned', `Đang có đồ (${ownedSetCount})`],
        ].map(([k, lbl]) => `<button class="chip ${planetFilter === k ? 'active' : ''}" data-pfilter="${k}">${lbl}</button>`).join('')}
      </div>

      ${setsHtml}

      <div style="display:grid;grid-template-columns:1.45fr 1fr;gap:12px;flex:1;min-height:240px;">
        <div class="card" style="display:flex;flex-direction:column;overflow:hidden;">
          <div class="card-title">📦 Bảng SKH Theo Từng Tài Khoản (${filteredAccs.length} acc)</div>
          <div class="table-wrap" style="border:none;">
            <table class="data-table">
              <thead><tr><th>Tài khoản</th><th>SV</th><th>Ma trận SKH (Áo-Quần-Găng-Giày-Rd)</th><th>Sao / Món lẻ</th></tr></thead>
              <tbody>
                ${filteredAccs.length === 0
                  ? `<tr><td colspan="4" style="text-align:center;padding:24px;color:var(--text-dim);">Chưa có tài khoản nào khớp bộ lọc.</td></tr>`
                  : filteredAccs.map((a) => {
                    const setChips = (a.sets && a.sets.length > 0)
                      ? a.sets.map((s) => `
                          <span class="skh-mini-chip ${s.total_items > 0 ? 'has' : ''}" title="Set ${esc(s.set_name)} (Áo-Quần-Găng-Giày-Rada)">
                            <b>${esc(s.short_name || s.set_name)}</b> ${renderCodeDigits(s.code || '0-0-0-0-0')}
                          </span>
                        `).join('')
                      : `<span class="text-dim">Chưa vào game · 0-0-0-0-0</span>`;

                    const starItems = (a.items || []).filter((it) => !it.is_skh && it.stars > 0);
                    return `
                      <tr>
                        <td><b>${esc(window.MX.privacy.maskUser(a.username, a.account_id))}</b> ${a.planet ? `<span class="badge ${a.planet === 'TĐ' ? 'b-td' : (a.planet === 'NM' ? 'b-nm' : 'b-xd')}">${esc(a.planet)}</span>` : '<span class="badge b-idle">?</span>'}</td>
                        <td>${esc(a.server_name)}</td>
                        <td style="white-space:normal;"><div class="skh-chip-wrap">${setChips}</div></td>
                        <td style="white-space:normal;">
                          ${a.star_count > 0
                            ? `<span class="text-sky" style="font-weight:700;">★ ${a.star_count}</span> ` +
                              starItems.slice(0, 4).map((it) => `<span class="badge b-store" style="margin:1px;">${esc(it.slot)} ${it.stars}★</span>`).join('')
                            : `<span class="text-dim">0</span>`}
                        </td>
                      </tr>
                    `;
                  }).join('')}
              </tbody>
            </table>
          </div>
        </div>

        <div class="card" style="display:flex;flex-direction:column;overflow:hidden;">
          <div class="card-title">🔥 Lịch Sử Nổ SKH Gần Đây (${hist.length})</div>
          <div class="table-wrap" style="border:none;">
            <table class="data-table">
              <thead><tr><th>Giờ</th><th>Acc</th><th>Món Nổ</th></tr></thead>
              <tbody>
                ${hist.length === 0
                  ? `<tr><td colspan="3" style="text-align:center;padding:24px;color:var(--text-dim);">Chưa ghi nhận món SKH mới nào.</td></tr>`
                  : hist.slice(0, 80).map((h) => `
                    <tr>
                      <td class="text-dim">${new Date(h.found_at).toLocaleTimeString('vi-VN')}</td>
                      <td><b>${esc(window.MX.privacy.maskUser(h.username, h.account_id))}</b></td>
                      <td><span class="text-gold">${esc(h.item.slot)} Set ${esc(h.item.set_name)}</span>${h.item.stars > 0 ? ' (' + h.item.stars + '★)' : ''}</td>
                    </tr>
                  `).join('')}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    `;

    root.querySelectorAll('button[data-pfilter]').forEach((btn) => {
      btn.onclick = () => {
        planetFilter = btn.dataset.pfilter;
        render();
      };
    });

    const inp = document.getElementById('loot-search');
    if (inp) {
      inp.oninput = (e) => {
        searchQ = e.target.value;
        render();
        const el = document.getElementById('loot-search');
        if (el) { el.focus(); el.setSelectionRange(searchQ.length, searchQ.length); }
      };
    }
  }

  return { render, renderCodeDigits };
})();
