// Tab 4: Proxy Manager (HTTP & SOCKS5 + Check Live/Ping 16 luồng Go)
window.MX = window.MX || {};

window.MX.ProxyTab = (function () {
  let checking = false;

  function render() {
    const root = document.getElementById('tab-proxy');
    const proxies = window.MX.state.proxies || [];
    const esc = window.MX.esc;
    const priv = window.MX.privacy;
    const privOn = priv.isOn();

    root.innerHTML = `
      <div class="card">
        <div class="card-title">🌐 Nhập Danh Sách Proxy (HTTP / SOCKS5)</div>
        <div style="display:flex;gap:10px;align-items:flex-start;">
          <textarea id="prx-raw" class="${privOn ? 'privacy-mask-text' : ''}" rows="4" style="flex:1;" placeholder="Dán mỗi dòng 1 proxy — nhận ĐỦ định dạng: ip:port | ip:port:user:pass | user:pass:ip:port | user:pass@ip:port | ip:port@user:pass | http://... | socks5://... (cách bằng : | , ; khoảng trắng)"></textarea>
          <div style="display:flex;flex-direction:column;gap:8px;width:185px;">
            <select id="prx-type">
              <option value="http">Mặc định: HTTP Proxy</option>
              <option value="socks5">Mặc định: SOCKS5 Proxy</option>
            </select>
            <button class="btn btn-primary" id="btn-prx-import">＋ Nhập Proxy</button>
          </div>
        </div>
      </div>

      <div class="toolbar">
        <div class="toolbar-group">
          <b>Tổng: ${proxies.length} proxy</b>
          <span class="text-green">Live: ${proxies.filter((p) => p.status === 'live').length}</span>
          <span class="text-red">Die: ${proxies.filter((p) => p.status === 'die').length}</span>
        </div>
        <div class="toolbar-group">
          <button class="btn btn-sky" id="btn-prx-check" ${checking ? 'disabled' : ''}>${checking ? '⏳ Đang kiểm tra...' : '⚡ Check Live & Đo Ping (16 luồng)'}</button>
          <button class="btn btn-danger-ghost" id="btn-prx-del-die">🗑 Xóa Proxy Die</button>
        </div>
      </div>

      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width:44px;">#</th>
              <th>Loại</th>
              <th>Host : Port</th>
              <th>Username</th>
              <th>Trạng thái</th>
              <th>Ping</th>
              <th style="width:70px;">Xóa</th>
            </tr>
          </thead>
          <tbody>
            ${proxies.length === 0
              ? `<tr><td colspan="7" style="text-align:center;padding:28px;color:var(--text-dim);">Chưa có proxy nào (các acc sẽ chạy bằng IP Home gốc).</td></tr>`
              : proxies.map((p, i) => `
                <tr>
                  <td class="text-dim">${i + 1}</td>
                  <td><span class="badge ${p.type === 'socks5' ? 'b-buff' : 'b-store'}">${esc(p.type.toUpperCase())}</span></td>
                  <td><b>${privOn ? '•••.•••.•••:•••' : `${esc(p.host)}:${p.port}`}</b></td>
                  <td class="text-dim">${esc(priv.maskProxyUser(p.username))}</td>
                  <td>
                    ${p.status === 'live'
                      ? `<span class="badge b-farm">● LIVE</span>`
                      : (p.status === 'die' ? `<span class="badge b-err">✕ DIE</span>` : `<span class="badge b-idle">Chưa check</span>`)}
                  </td>
                  <td class="${p.ping_ms > 0 && p.ping_ms < 200 ? 'text-green' : 'text-gold'}">${p.ping_ms > 0 ? p.ping_ms + ' ms' : '—'}</td>
                  <td><button class="btn btn-xs btn-danger-ghost" data-delprx="${esc(p.id)}">✕</button></td>
                </tr>
              `).join('')}
          </tbody>
        </table>
      </div>
    `;

    document.getElementById('btn-prx-import').onclick = async () => {
      const raw = document.getElementById('prx-raw').value;
      const typ = document.getElementById('prx-type').value;
      try {
        const added = await window.MX.api.ImportProxies(raw, typ);
        window.MX.toast(`Đã nhập ${added} proxy mới!`, 'ok');
        await window.MX.refreshBootstrap();
      } catch (err) { window.MX.toast(String(err), 'err'); }
    };

    document.getElementById('btn-prx-check').onclick = async () => {
      checking = true;
      render();
      try {
        await window.MX.api.CheckAllProxies();
        window.MX.toast('Đã kiểm tra xong toàn bộ Proxy!', 'ok');
        await window.MX.refreshBootstrap();
      } catch (err) {
        window.MX.toast(String(err), 'err');
      } finally {
        checking = false;
        render();
      }
    };

    document.getElementById('btn-prx-del-die').onclick = async () => {
      try {
        const n = await window.MX.api.DeleteDeadProxies();
        window.MX.toast(`Đã xóa ${n} proxy Die.`);
        await window.MX.refreshBootstrap();
      } catch (err) { window.MX.toast(String(err), 'err'); }
    };

    root.querySelectorAll('[data-delprx]').forEach((btn) => {
      btn.onclick = async () => {
        await window.MX.api.DeleteProxies([btn.dataset.delprx]);
        await window.MX.refreshBootstrap();
      };
    });
  }

  return { render };
})();
