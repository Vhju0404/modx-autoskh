// Tab 2: Quản Lý Farm Profiles (Nhiều bộ luật farm độc lập, đồng bộ live qua Socket)
window.MX = window.MX || {};

window.MX.ProfilesTab = (function () {
  const CHARM_LIST = [
    { id: 213, name: 'Trí Tuệ' },
    { id: 214, name: 'Mạnh Mẽ' },
    { id: 215, name: 'Da Trâu' },
    { id: 216, name: 'Oai Hùng' },
    { id: 217, name: 'Bất Tử' },
    { id: 218, name: 'Dẻo Dai' },
    { id: 219, name: 'Thu Hút' },
    { id: 671, name: 'Trí Tuệ x3' },
    { id: 672, name: 'Trí Tuệ x4' },
  ];

  const FARM_MODES = {
    sp_tdlt: 'Sư Phụ TDLT (Auto Train Pro)',
    sp_normal: 'Sư Phụ Đánh Thường (Free Ngọc)',
    up_pet: 'Up Đệ Tử (Bảo Vệ + Fix Đệ Lười)',
    private_ticket: 'Map Riêng (Vé 1825)',
  };

  let activeId = '';

  function render() {
    const root = document.getElementById('tab-profiles');
    const st = window.MX.state;
    const profiles = st.profiles || [];
    if (!activeId || !profiles.some((p) => p.id === activeId)) {
      activeId = profiles[0]?.id || '';
    }
    const cur = profiles.find((p) => p.id === activeId);
    const esc = window.MX.esc;

    root.innerHTML = `
      <div class="split-layout">
        <div class="split-left">
          <div style="display:flex;gap:6px;">
            <button class="btn btn-primary btn-sm" style="flex:1;" id="btn-prof-new">＋ Tạo Profile</button>
            <button class="btn btn-secondary btn-sm" id="btn-prof-dup" title="Nhân bản Profile đang chọn">❐</button>
          </div>
          <div id="prof-list" style="display:flex;flex-direction:column;gap:6px;">
            ${profiles.map((p) => `
              <div class="profile-card ${p.id === activeId ? 'active' : ''}" data-pid="${esc(p.id)}">
                <div class="profile-card-name">${esc(p.name)}</div>
                <div class="profile-card-mode">${esc(FARM_MODES[p.farm_mode] || p.farm_mode)}</div>
              </div>
            `).join('')}
          </div>
        </div>
        <div class="split-right" id="prof-editor">
          ${cur ? renderEditor(cur) : '<div class="card text-dim">Chọn hoặc tạo Profile mới.</div>'}
        </div>
      </div>
    `;

    root.querySelectorAll('.profile-card').forEach((el) => {
      el.onclick = () => {
        activeId = el.dataset.pid;
        render();
      };
    });

    document.getElementById('btn-prof-new').onclick = async () => {
      const base = cur ? { ...cur, id: '', name: 'Profile Mới' } : null;
      if (!base) return;
      try {
        const saved = await window.MX.api.SaveProfile(base);
        activeId = saved.id;
        window.MX.toast('Đã tạo Profile mới!', 'ok');
        await window.MX.refreshBootstrap();
      } catch (err) { window.MX.toast(String(err), 'err'); }
    };

    document.getElementById('btn-prof-dup').onclick = async () => {
      if (!cur) return;
      try {
        const saved = await window.MX.api.SaveProfile({ ...cur, id: '', name: `${cur.name} (Copy)` });
        activeId = saved.id;
        window.MX.toast('Đã nhân bản Profile!', 'ok');
        await window.MX.refreshBootstrap();
      } catch (err) { window.MX.toast(String(err), 'err'); }
    };

    if (cur) bindEditorEvents(cur);
  }

  function mapSelectHtml(id, planetFilter, curVal) {
    const presets = (window.MX.state.map_presets || []).filter((m) => m.id >= 0 && (m.planet === planetFilter || !planetFilter));
    return `<select id="${id}">` + presets.map((m) =>
      `<option value="${m.id}" ${m.id === curVal ? 'selected' : ''}>${window.MX.esc(m.name)}</option>`
    ).join('') + `</select>`;
  }

  function renderEditor(p) {
    const esc = window.MX.esc;
    const skhSets = window.MX.state.skh_sets || [];
    const keepSet = new Set(p.keep_skh_sets || []);
    const charmSet = new Set(p.charm_ids || []);

    return `
      <div class="card">
        <div class="card-title">
          <span>⚙️ Thông Tin &amp; Chế Độ Farm</span>
          <div style="display:flex;gap:8px;">
            <button class="btn btn-danger-ghost btn-sm" id="btn-prof-del">🗑 Xóa</button>
            <button class="btn btn-primary btn-sm" id="btn-prof-save">💾 Lưu &amp; Đồng Bộ Live</button>
          </div>
        </div>
        <div class="form-grid">
          <div class="field"><label>Tên Profile</label><input type="text" id="p-name" value="${esc(p.name)}" /></div>
          <div class="field"><label>Chế độ Farm</label>
            <select id="p-mode">
              ${Object.entries(FARM_MODES).map(([k, v]) => `<option value="${k}" ${p.farm_mode === k ? 'selected' : ''}>${esc(v)}</option>`).join('')}
            </select>
          </div>
          <div class="field" style="grid-column:span 2;"><label>Mô tả ngắn</label><input type="text" id="p-desc" value="${esc(p.description || '')}" /></div>
        </div>
      </div>

      <div class="card">
        <div class="card-title">🗺️ Map Mặc Định Theo Hành Tinh &amp; Chống KS Khu</div>
        <div class="form-grid">
          <div class="field"><label>Map Trái Đất</label>${mapSelectHtml('p-map-td', 'TĐ', p.default_map_td)}</div>
          <div class="field"><label>Map Namek</label>${mapSelectHtml('p-map-nm', 'NM', p.default_map_nm)}</div>
          <div class="field"><label>Map Xayda</label>${mapSelectHtml('p-map-xd', 'XD', p.default_map_xd)}</div>
          <div class="field"><label>Chiến thuật Khu</label>
            <select id="p-zstrat">
              <option value="empty" ${p.zone_strategy === 'empty' ? 'selected' : ''}>Tự tìm khu vắng nhất</option>
              <option value="fixed" ${p.zone_strategy === 'fixed' ? 'selected' : ''}>Khu cố định</option>
            </select>
          </div>
          <div class="field"><label>Khu cố định (nếu chọn)</label><input type="number" id="p-fzone" value="${p.fixed_zone_id || 0}" min="0" max="99" /></div>
          <div class="field"><label>Đổi khu nếu người &gt;</label><input type="number" id="p-maxpl" value="${p.max_players_zone || 3}" min="1" max="15" /></div>
          <div class="field"><label>Đổi khu nếu không TN (giây)</label><input type="number" id="p-noexpsw" value="${p.no_exp_switch_sec || 35}" min="0" max="300" /></div>
          <div class="field"><label>Thoát tab nếu không TN (giây)</label><input type="number" id="p-noexprs" value="${p.no_exp_restart_sec || 95}" min="30" max="600" /></div>
        </div>
      </div>

      <div class="card">
        <div class="card-title">🏆 Luật Lọc Đồ SKH &amp; Đồ Sao (Cất Rương / Bán Rác)</div>
        <div class="form-grid">
          <div class="field">
            <label class="check-row"><input type="checkbox" id="p-keepall" ${p.keep_all_skh ? 'checked' : ''} /> <b>Giữ tất cả các bộ Set Kích Hoạt</b></label>
            <span class="small-sub">Tắt mục này nếu bạn chỉ muốn giữ những bộ SKH xịn bên dưới, còn lại bán sạch!</span>
          </div>
          <div class="field">
            <label>Giữ đồ Sao pha lê từ ≥ N sao (0 = không giữ đồ sao thường)</label>
            <select id="p-minstars">
              <option value="0" ${p.min_keep_stars === 0 ? 'selected' : ''}>0 — Không giữ đồ sao thường (chỉ giữ SKH)</option>
              <option value="1" ${p.min_keep_stars === 1 ? 'selected' : ''}>≥ 1 sao (giữ từ 1 đến 7 sao)</option>
              <option value="2" ${p.min_keep_stars === 2 ? 'selected' : ''}>≥ 2 sao (bán 1 sao, giữ 2–7 sao)</option>
              <option value="3" ${p.min_keep_stars === 3 ? 'selected' : ''}>≥ 3 sao (bán 1–2 sao, giữ 3–7 sao)</option>
              <option value="4" ${p.min_keep_stars === 4 ? 'selected' : ''}>≥ 4 sao (chỉ giữ 4–7 sao)</option>
            </select>
          </div>
          <div class="field">
            <label>Khi Rương Nhà Đầy</label>
            <select id="p-boxfull">
              <option value="keep_farming" ${(!p.on_box_full || p.on_box_full === 'keep_farming') ? 'selected' : ''}>Kệ rương đầy — Cứ đánh tiếp (tận dụng hành trang ẩn)</option>
              <option value="mark_done_rotate" ${p.on_box_full === 'mark_done_rotate' ? 'selected' : ''}>Đánh dấu Xong → Tắt acc → Xoay acc kế tiếp</option>
              <option value="stop_at_home" ${p.on_box_full === 'stop_at_home' ? 'selected' : ''}>Đứng yên ở nhà chờ (không thoát game)</option>
            </select>
          </div>
        </div>

        <div style="margin-top:10px;">
          <div class="text-dim" style="font-size:11.5px;margin-bottom:4px;">Chọn bộ SKH được giữ (khi tắt "Giữ tất cả" — đủ 14 bộ chuẩn NRO mới nhất):</div>
          <div class="check-grid" id="p-skh-sets">
            ${skhSets.map((s) => `
              <label class="check-row">
                <input type="checkbox" class="chk-skhset" value="${esc(s.name)}" ${keepSet.has(s.name) ? 'checked' : ''} />
                <span>${esc(s.name)} <span class="badge ${s.planet === 'TĐ' ? 'b-td' : (s.planet === 'NM' ? 'b-nm' : (s.planet === 'XD' ? 'b-xd' : 'b-buff'))}" style="font-size:10px;padding:1px 5px;">${s.planet === 'ALL' ? 'Chung' : esc(s.planet)}</span></span>
              </label>
            `).join('')}
          </div>
        </div>

        <div class="check-grid" style="margin-top:12px;">
          <label class="check-row"><input type="checkbox" id="p-store" ${p.auto_store_house ? 'checked' : ''} /> Tự về nhà cất đồ SKH/Sao</label>
          <label class="check-row"><input type="checkbox" id="p-sell" ${p.auto_sell_trash ? 'checked' : ''} /> Full hành trang tự qua Trạm Tàu (Map 24) bán rác</label>
          <label class="check-row"><input type="checkbox" id="p-pickall" ${p.pick_all ? 'checked' : ''} /> Nhặt tất cả vật phẩm rơi</label>
        </div>

        <div class="form-grid" style="margin-top:10px;">
          <div class="field"><label>ID Item luôn nhặt thêm (phẩy)</label><input type="text" id="p-pickids" value="${(p.pick_item_ids || []).join(',')}" placeholder="77,861" /></div>
          <div class="field"><label>ID Item luôn bán (phẩy)</label><input type="text" id="p-sellids" value="${(p.sell_item_ids || []).join(',')}" placeholder="Vd: 225,190" /></div>
          <div class="field"><label>ID Item luôn vứt bỏ (phẩy)</label><input type="text" id="p-dropids" value="${(p.drop_item_ids || []).join(',')}" placeholder="Vd: 190" /></div>
        </div>
      </div>

      <div class="card">
        <div class="card-title">✨ Buff, TDLT, Cỏ 4 Lá, Bùa &amp; Sinh Tồn</div>
        <div class="form-grid">
          <div class="field">
            <label class="check-row"><input type="checkbox" id="p-buytdlt" ${p.auto_buy_tdlt ? 'checked' : ''} /> Tự mua &amp; dùng TDLT</label>
            <select id="p-tdltid">
              <option value="521" ${p.tdlt_item_id === 521 ? 'selected' : ''}>TDLT 1 (521 — 1 ngọc)</option>
              <option value="1523" ${p.tdlt_item_id === 1523 ? 'selected' : ''}>TDLT 2 (1523 — 9 ngọc)</option>
              <option value="1524" ${p.tdlt_item_id === 1524 ? 'selected' : ''}>TDLT 3 (1524 — 22 ngọc)</option>
            </select>
          </div>
          <div class="field"><label>Mua TDLT đạt tối thiểu (phút)</label><input type="number" id="p-tdltmin" value="${p.tdlt_min_minutes || 20}" min="0" /></div>
          <div class="field">
            <label class="check-row"><input type="checkbox" id="p-buylucky" ${p.auto_buy_lucky ? 'checked' : ''} /> Tự mua &amp; dùng Cỏ 4 lá (1635)</label>
            <input type="number" id="p-luckyqty" value="${p.lucky_grass_qty || 1}" min="1" max="99" placeholder="Số lượng" />
          </div>
          <div class="field"><label>Giữ lại tối thiểu (Ngọc)</label><input type="number" id="p-mingem" value="${p.min_gem_keep || 0}" min="0" /></div>
          <div class="field"><label>Ăn đậu khi HP &lt; (%)</label><input type="number" id="p-beanhp" value="${p.auto_bean_hp || 25}" min="0" max="95" /></div>
          <div class="field"><label>Ăn đậu khi MP &lt; (%)</label><input type="number" id="p-beanmp" value="${p.auto_bean_mp || 20}" min="0" max="95" /></div>
          <div class="field"><label>Ăn nho tím thể lực &lt; (%)</label><input type="number" id="p-grape" value="${p.auto_purple_grape || 10}" min="0" max="95" /></div>
          <div class="field"><label class="check-row" style="margin-top:20px;"><input type="checkbox" id="p-askbean" ${p.auto_ask_clan_bean ? 'checked' : ''} /> Tự xin đậu Bang (mỗi 5p)</label></div>
        </div>

        <div class="check-grid" style="margin-top:10px;">
          <label class="check-row"><input type="checkbox" id="p-buyticket" ${p.auto_buy_ticket ? 'checked' : ''} /> 🎫 Tự mua Vé Map Riêng (1825) khi còn mầm</label>
          <label class="check-row"><input type="checkbox" id="p-buygoldbar" ${p.auto_buy_goldbar ? 'checked' : ''} /> 🪙 Tự đổi Thỏi Vàng (457) khi vàng ≥ 1.5 tỷ</label>
          <label class="check-row"><input type="checkbox" id="p-gettitle" ${p.auto_get_title ? 'checked' : ''} /> 🏅 Tự nhận &amp; gia hạn Danh hiệu "Tay nhanh hơn não" (+20% May mắn, 10 Loa LVT, HSD 7 ngày)</label>
          <label class="check-row"><input type="checkbox" id="p-petwiggle" ${p.pet_wiggle ? 'checked' : ''} /> 🕺 Wiggle/KOK nhích qua lại chống đứng im (Up Đệ)</label>
        </div>

        <div style="margin-top:12px;border-top:1px solid var(--border);padding-top:10px;">
          <div class="form-grid">
            <div class="field"><label class="check-row"><input type="checkbox" id="p-buycharm" ${p.auto_buy_charm ? 'checked' : ''} /> Tự mua Bùa Hạt Mít</label></div>
            <div class="field"><label>Gói Bùa</label>
              <select id="p-charmdur">
                <option value="1h" ${p.charm_duration === '1h' ? 'selected' : ''}>Bùa 1 giờ</option>
                <option value="8h" ${p.charm_duration === '8h' ? 'selected' : ''}>Bùa 8 giờ</option>
                <option value="1m" ${p.charm_duration === '1m' ? 'selected' : ''}>Bùa 1 tháng</option>
              </select>
            </div>
          </div>
          <div class="check-grid">
            ${CHARM_LIST.map((c) => `
              <label class="check-row"><input type="checkbox" class="chk-charm" value="${c.id}" ${charmSet.has(c.id) ? 'checked' : ''} /> ${c.name}</label>
            `).join('')}
          </div>
        </div>

        <div style="margin-top:12px;border-top:1px solid var(--border);padding-top:10px;">
          <label class="check-row"><input type="checkbox" id="p-uppot" ${p.auto_up_potential ? 'checked' : ''} /> Tự cộng chỉ số (có kiểm tra đủ Tiềm Năng, không spam gói)</label>
          <div class="form-grid" style="margin-top:8px;">
            <div class="field"><label>HP Gốc mục tiêu</label><input type="number" id="p-thp" value="${p.target_hp || 0}" min="0" /></div>
            <div class="field"><label>KI Gốc mục tiêu</label><input type="number" id="p-tki" value="${p.target_ki || 0}" min="0" /></div>
            <div class="field"><label>Sức Đánh mục tiêu</label><input type="number" id="p-tsd" value="${p.target_sd || 0}" min="0" /></div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-title">⚡ Tối Ưu Hiệu Năng Client &amp; Chiến Đấu</div>
        <div class="form-grid" style="margin-bottom:10px;">
          <div class="field" style="grid-column:span 2;">
            <label>👊 Chiêu đánh quái khi Tàn sát (Đồng bộ Live — không cần mở game)</label>
            <select id="p-skillmode">
              <option value="punch_only" ${(!p.attack_skill_mode || p.attack_skill_mode === 'punch_only') ? 'selected' : ''}>👊 Chỉ Đấm cơ bản (Dragon / Demon / Galick — chuẩn Up SKH, không tốn KI)</option>
              <option value="melee_only" ${p.attack_skill_mode === 'melee_only' ? 'selected' : ''}>🥊 Đấm + Kaioken / Liên Hoàn (bỏ Chưởng)</option>
              <option value="auto_all" ${p.attack_skill_mode === 'auto_all' ? 'selected' : ''}>⚡ Tự động (Đấm + Chưởng + Liên Hoàn — tối đa nhịp đánh)</option>
              <option value="slots" ${p.attack_skill_mode === 'slots' ? 'selected' : ''}>🎯 Tự chọn theo Ô phím trên thanh Skill (Ô 1 – Ô 5)</option>
            </select>
          </div>
          <div class="field" id="p-skillslots-wrap" style="display:${p.attack_skill_mode === 'slots' ? 'flex' : 'none'};">
            <label>Chọn các Ô phím đánh (Ô 1 thường là Đấm)</label>
            <div style="display:flex;gap:10px;align-items:center;height:32px;flex-wrap:wrap;">
              ${[1, 2, 3, 4, 5].map((n) => {
                const slots = Array.isArray(p.attack_skill_slots) && p.attack_skill_slots.length ? p.attack_skill_slots : [1];
                return `<label class="check-row" style="margin:0;"><input type="checkbox" class="chk-skillslot" value="${n}" ${slots.includes(n) ? 'checked' : ''} /> Ô ${n}</label>`;
              }).join('')}
            </div>
          </div>
        </div>
        <div class="form-grid">
          <div class="field"><label>Tốc độ game (TimeScale)</label><input type="number" step="0.5" id="p-gspeed" value="${p.game_speed || 2}" min="1" max="5" /></div>
          <div class="field"><label>Tốc độ chạy (cspeed)</label><input type="number" id="p-cspeed" value="${p.char_speed || 6}" min="4" max="12" /></div>
          <div class="field"><label>Khóa FPS nền (10–30)</label><input type="number" id="p-fps" value="${p.target_fps || 15}" min="8" max="60" /></div>
        </div>
        <div class="check-grid" style="margin-top:10px;">
          <label class="check-row"><input type="checkbox" id="p-fastatk" ${p.fast_attack_no_anim ? 'checked' : ''} /> ⚡ Đánh quái xóa anim (TDLT đứng 1 chỗ đánh toàn map, không chờ hiệu ứng)</label>
          <label class="check-row"><input type="checkbox" id="p-teletdlt" ${p.tele_to_mob_tdlt ? 'checked' : ''} /> 🏃 Dịch đến quái khi TDLT (tắt để giữ max tốc độ train)</label>
          <label class="check-row"><input type="checkbox" id="p-freezemob" ${p.freeze_mobs ? 'checked' : ''} /> ❄️ Đóng băng quái tại chỗ (quái không chạy lang thang)</label>
          <label class="check-row"><input type="checkbox" id="p-showmob" ${p.show_mob_graphics ? 'checked' : ''} /> 👾 Hiện hình ảnh quái (tắt để ẩn quái siêu nhẹ)</label>
          <label class="check-row"><input type="checkbox" id="p-showchar" ${p.show_char_graphics ? 'checked' : ''} /> 🧍 Hiện nhân vật &amp; phụ kiện đang mặc (cải trang, đeo lưng, ván bay, danh hiệu, linh thú — tắt để vẽ khung nhẹ VPS)</label>
          <label class="check-row"><input type="checkbox" id="p-horizinv" ${p.horizontal_inventory ? 'checked' : ''} /> 🎒 Hành trang ngang 5 cột (bật = ô lưới ngang + hiện sao/dòng ép; tắt = danh sách dọc + hiện sao/dòng ép)</label>
          <label class="check-row"><input type="checkbox" id="p-black" ${p.black_screen_vps ? 'checked' : ''} /> 🌑 Màn đen siêu nhẹ VPS (tắt vẽ toàn bộ map)</label>
          <label class="check-row"><input type="checkbox" id="p-headless" ${p.headless_mode ? 'checked' : ''} /> 👻 <b>Treo ngầm không cửa sổ (Headless VPS)</b> — 0% GPU, ~0% CPU, giảm 3× RAM, quản lý live trên QLTK (mở lại có hình bất cứ lúc nào)</label>
        </div>
      </div>
    `;
  }

  function parseIntList(str) {
    return String(str || '')
      .split(/[,\s]+/)
      .map((x) => parseInt(x.trim(), 10))
      .filter((n) => !Number.isNaN(n) && n >= 0);
  }

  function bindEditorEvents(cur) {
    const skillSel = document.getElementById('p-skillmode');
    const slotWrap = document.getElementById('p-skillslots-wrap');
    if (skillSel && slotWrap) {
      skillSel.onchange = () => {
        slotWrap.style.display = skillSel.value === 'slots' ? 'flex' : 'none';
      };
    }

    document.getElementById('btn-prof-save').onclick = async () => {
      try {
        const keepSets = Array.from(document.querySelectorAll('.chk-skhset:checked')).map((el) => el.value);
        const charmIds = Array.from(document.querySelectorAll('.chk-charm:checked')).map((el) => parseInt(el.value, 10));
        const skillSlots = Array.from(document.querySelectorAll('.chk-skillslot:checked')).map((el) => parseInt(el.value, 10));
        const payload = {
          ...cur,
          name: document.getElementById('p-name').value.trim(),
          description: document.getElementById('p-desc').value.trim(),
          farm_mode: document.getElementById('p-mode').value,
          default_map_td: parseInt(document.getElementById('p-map-td').value, 10),
          default_map_nm: parseInt(document.getElementById('p-map-nm').value, 10),
          default_map_xd: parseInt(document.getElementById('p-map-xd').value, 10),
          zone_strategy: document.getElementById('p-zstrat').value,
          fixed_zone_id: parseInt(document.getElementById('p-fzone').value, 10) || 0,
          max_players_zone: parseInt(document.getElementById('p-maxpl').value, 10) || 3,
          no_exp_switch_sec: parseInt(document.getElementById('p-noexpsw').value, 10) || 0,
          no_exp_restart_sec: parseInt(document.getElementById('p-noexprs').value, 10) || 95,
          keep_all_skh: document.getElementById('p-keepall').checked,
          keep_skh_sets: keepSets,
          min_keep_stars: parseInt(document.getElementById('p-minstars').value, 10) || 0,
          on_box_full: document.getElementById('p-boxfull').value,
          auto_store_house: document.getElementById('p-store').checked,
          auto_sell_trash: document.getElementById('p-sell').checked,
          pick_all: document.getElementById('p-pickall').checked,
          pick_item_ids: parseIntList(document.getElementById('p-pickids').value),
          sell_item_ids: parseIntList(document.getElementById('p-sellids').value),
          drop_item_ids: parseIntList(document.getElementById('p-dropids').value),
          auto_buy_tdlt: document.getElementById('p-buytdlt').checked,
          tdlt_item_id: parseInt(document.getElementById('p-tdltid').value, 10) || 521,
          tdlt_min_minutes: parseInt(document.getElementById('p-tdltmin').value, 10) || 0,
          auto_buy_lucky: document.getElementById('p-buylucky').checked,
          lucky_grass_qty: parseInt(document.getElementById('p-luckyqty').value, 10) || 1,
          auto_buy_ticket: document.getElementById('p-buyticket').checked,
          auto_buy_goldbar: document.getElementById('p-buygoldbar').checked,
          auto_get_title: document.getElementById('p-gettitle').checked,
          pet_wiggle: document.getElementById('p-petwiggle').checked,
          min_gem_keep: parseInt(document.getElementById('p-mingem').value, 10) || 0,
          auto_bean_hp: parseInt(document.getElementById('p-beanhp').value, 10) || 25,
          auto_bean_mp: parseInt(document.getElementById('p-beanmp').value, 10) || 20,
          auto_purple_grape: parseInt(document.getElementById('p-grape').value, 10) || 10,
          auto_ask_clan_bean: document.getElementById('p-askbean').checked,
          auto_buy_charm: document.getElementById('p-buycharm').checked,
          charm_duration: document.getElementById('p-charmdur').value,
          charm_ids: charmIds,
          auto_up_potential: document.getElementById('p-uppot').checked,
          target_hp: parseInt(document.getElementById('p-thp').value, 10) || 0,
          target_ki: parseInt(document.getElementById('p-tki').value, 10) || 0,
          target_sd: parseInt(document.getElementById('p-tsd').value, 10) || 0,
          game_speed: parseFloat(document.getElementById('p-gspeed').value) || 2.0,
          char_speed: parseInt(document.getElementById('p-cspeed').value, 10) || 6,
          target_fps: parseInt(document.getElementById('p-fps').value, 10) || 15,
          black_screen_vps: document.getElementById('p-black').checked,
          show_mob_graphics: document.getElementById('p-showmob').checked,
          show_char_graphics: document.getElementById('p-showchar').checked,
          horizontal_inventory: document.getElementById('p-horizinv').checked,
          fast_attack_no_anim: document.getElementById('p-fastatk').checked,
          attack_skill_mode: document.getElementById('p-skillmode').value || 'punch_only',
          attack_skill_slots: skillSlots.length > 0 ? skillSlots : [1],
          tele_to_mob_tdlt: document.getElementById('p-teletdlt').checked,
          freeze_mobs: document.getElementById('p-freezemob').checked,
          headless_mode: document.getElementById('p-headless').checked,
        };
        await window.MX.api.SaveProfile(payload);
        window.MX.toast(`Đã lưu & đẩy live Profile "${payload.name}"!`, 'ok');
        await window.MX.refreshBootstrap();
      } catch (err) {
        window.MX.toast(String(err), 'err');
      }
    };

    document.getElementById('btn-prof-del').onclick = async () => {
      if (!confirm(`Xóa Profile "${cur.name}"?`)) return;
      try {
        await window.MX.api.DeleteProfile(cur.id);
        activeId = '';
        window.MX.toast('Đã xóa Profile.');
        await window.MX.refreshBootstrap();
      } catch (err) {
        window.MX.toast(String(err), 'err');
      }
    };
  }

  return { render };
})();
