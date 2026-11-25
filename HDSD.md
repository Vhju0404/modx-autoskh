# 📖 HƯỚNG DẪN SỬ DỤNG CHI TIẾT — MOD X · H TEAM (Auto Up Set Kích Hoạt)

> Tài liệu hướng dẫn đầy đủ từng tính năng cho **MOD X · H TEAM — Auto Up SKH (NRO Chính)**, áp dụng cho cả máy cá nhân (Windows 10/11) lẫn **VPS cấu hình thấp (Windows Server 2012 R2 · 1 Core / 1–2 GB RAM)**.

---

## 📑 Mục lục
1. [Yêu cầu máy & Cách khởi động](#1-yêu-cầu-máy--cách-khởi-động)
2. [Làm quen giao diện 5 Tab & Thanh KPI](#2-làm-quen-giao-diện-5-tab--thanh-kpi)
3. [Tab 1 — Quản lý Tài Khoản (Thêm, Sửa, Treo ngầm, Soi X-Quang)](#3-tab-1--quản-lý-tài-khoản)
4. [Tab 2 — Farm Profiles (Chọn Skill Tàn Sát, Lọc SKH, Map Riêng, Buff)](#4-tab-2--farm-profiles)
5. [Tab 3 — Kho SKH & Ma Trận Ghép Set 5 Món](#5-tab-3--kho-skh--ma-trận-ghép-set-5-món)
6. [Tab 4 — Proxy Manager (HTTP & SOCKS5)](#6-tab-4--proxy-manager-http--socks5)
7. [Tab 5 — Cấu hình VPS, Lịch Ca, Bảo Trì Thông Minh & Telegram](#7-tab-5--cấu-hình-vps-lịch-ca-bảo-trì-thông-minh--telegram)
8. [Chế độ Treo Ngầm Headless & Soi X-Quang Live](#8-chế-độ-treo-ngầm-headless--soi-x-quang-live)
9. [Phím tắt nhanh trong Game & QLTK](#9-phím-tắt-nhanh-trong-game--qltk)
10. [Cấu hình khuyến nghị cho VPS 1-1 / 1-2 & Câu hỏi thường gặp (FAQ)](#10-cấu-hình-khuyến-nghị-vps--câu-hỏi-thường-gặp-faq)

---

## 1. Yêu cầu máy & Cách khởi động

- **Hệ điều hành:** Windows Server 2012 R2 / 2016 / 2019 / 2022, Windows 10, Windows 11 (64-bit).
- **Không cần cài thêm** WebView2, .NET mới hay runtime ngoài — chỉ cần 1 file `ModX_HTeam_AutoSKH.exe` đặt cạnh thư mục `game/`.
- **Khởi động:**
  1. Giải nén trọn bộ vào 1 thư mục gọn (ví dụ `C:\ModX_AutoSKH\`).
  2. Bấm đúp **`ModX_HTeam_AutoSKH.exe`** → trình duyệt tự mở bảng quản lý tại `http://127.0.0.1:20269`.
  3. Hỗ trợ khởi động ẩn thẳng xuống khay hệ thống (dùng cho VPS Startup):
     ```bat
     ModX_HTeam_AutoSKH.exe --minimized
     ```
- **Chạy ngầm 24/7 dưới System Tray:**
  - Khi bấm dấu **✕** đóng cửa sổ trình duyệt QLTK, **tool KHÔNG bị tắt** — nó vẫn chạy ngầm (~17–25 MB RAM, ~0% CPU) để giữ Watchdog 24/7.
  - Muốn mở lại bảng quản lý: bấm vào icon **MOD X** ở góc phải thanh Taskbar (khay hệ thống) hoặc mở trình duyệt gõ `http://127.0.0.1:20269`.
  - Muốn **tắt hẳn tool và đóng hết mọi tab game**: bấm nút **`⏻ Thoát Hoàn Toàn`** ở góc dưới bên trái sidebar.

---

## 2. Làm quen giao diện 5 Tab & Thanh KPI

### Thanh KPI trên cùng
| Ô KPI | Ý nghĩa |
|---|---|
| **Tab Đang Chạy** | Số tab đang mở / giới hạn `MaxTabs` (vd `3 / 5`) |
| **Full Rương (Xong)** | Số acc đã đánh dấu hoàn thành (khi bật chế độ tự xoay acc khi full rương) |
| **Tổng Món SKH** | Tổng toàn bộ món đồ Set Kích Hoạt trên cả dàn acc |
| **Bộ Đủ 5/5** | Số bộ SKH đã gom đủ 5 món (`Áo - Quần - Găng - Giày - Rada`) |
| **Proxy Live** | Số proxy sống / tổng proxy trong Proxy Manager |
| **Cảnh Báo / Lỗi** | Số acc bị lỗi đăng nhập (sai pass/khóa), proxy die hoặc dừng Circuit Breaker |

### 3 nút tiện ích góc trên phải
- **`👁 Che TK`:** Che toàn bộ tên tài khoản, tên nhân vật, IP proxy, đường dẫn, Bot Token và tiêu đề cửa sổ game — cực tiện khi chụp ảnh khoe đồ hoặc quay video hướng dẫn.
- **`⊞ Xếp Cửa Sổ`:** Tự động xếp gọn toàn bộ cửa sổ game đang mở (chế độ có hình) thành lưới đều nhau.
- **`📜 Log`:** Mở/thu gọn khay Nhật Ký Sự Kiện (ghi lại toàn bộ lúc nổ SKH, đổi khu, xả RAM, bảo trì, lỗi...).

### Nút điều khiển bên trái (Sidebar)
- **`▶ Bật Auto Xoay Vòng`:** Tự động mở lần lượt các acc chưa xong cho đủ `MaxTabs` (có giãn nhịp chống nghẽn), acc nào xong tự tắt và xoay sang acc kế tiếp.
- **`⏹ Dừng Toàn Bộ Tab`:** Tắt sạch toàn bộ tab game đang chạy và hủy giữ 24/7.
- **`⏻ Thoát Hoàn Toàn`:** Đóng toàn bộ tab game và thoát hẳn QLTK khỏi khay hệ thống.

---

## 3. Tab 1 — Quản lý Tài Khoản

### 3.1. Thêm tài khoản hàng loạt (`＋ Thêm Acc`)
Hỗ trợ dán nhiều dòng cùng lúc (mỗi dòng 1 acc) với các định dạng linh hoạt:
```text
taikhoan01|matkhau01
taikhoan02|matkhau02|103.1.2.3:8080:userprx:passprx
taikhoan03|matkhau03|7|1|0|Nhom-TD
taikhoan04|matkhau04|7|15|0|Nhom-XD|socks5://user:pass@103.1.2.3:1080
```
- **Thứ tự đủ 7 cột:** `user|pass|số_server|map_id|khu|nhóm|proxy`
- Có thể chọn mặc định **Nhóm / Tag**, **Server**, **Farm Profile**, **Map**, **Chiến thuật Khu** và **Proxy** ngay bên dưới ô dán.
- Hỗ trợ dán **Proxy riêng** trực tiếp cho acc và bấm **`⚡ Check Proxy`** để tool tự nhận diện `HTTP` hay `SOCKS5` + kiểm tra kết nối tới đúng máy chủ NRO!
- Mọi mật khẩu đều được mã hóa **AES-256-GCM** ngay trong thư mục `data/` trên máy bạn (không lưu dạng chữ thường).

### 3.2. Bộ lọc & Thanh thao tác hàng loạt
- **Lọc nhanh theo chip:** `Tất cả`, `Đang chạy`, `Chưa xong`, `Full rương (Xong)`, `🌱 Còn mầm SKH`, `Có SKH`, `Cảnh báo / Lỗi`.
- **Tìm kiếm & Dropdown:** Lọc theo tên acc, nhóm, map, trạng thái, Server hoặc Profile.
- **Tick nhiều acc (hoặc chuột phải lên 1 dòng)** để ra lệnh nóng ngay không cần tắt game:
  - **`▶ Chạy`** — Mở theo chế độ mặc định của Profile.
  - **`👻 Treo ngầm`** — Mở chế độ Headless 0 cửa sổ (siêu nhẹ VPS).
  - **`👁 Mở có hình`** — Chuyển sang chế độ có cửa sổ để tự cầm tay kiểm tra/giao dịch.
  - **`🩻 Soi X-Quang Live`** — Xem toàn bộ thông số live & 25 bước gần nhất của acc.
  - **`⏹ Dừng` / `↻ Mở lại`** — Đóng hoặc khởi động lại tab.
  - **`🏷️ Gán hàng loạt`** — Đổi nhanh Nhóm / Profile / Map / Proxy cho cả chục acc cùng lúc.
  - **`🔀 Đổi khu vắng` / `🏠 Ép cất đồ` / `💰 Ép bán rác`** — Gửi lệnh nóng trực tiếp qua Socket.
  - **`⏸ Tạm dừng auto` / `▶ Tiếp tục auto`** — Dừng/chạy lại FSM mà không đóng game.
  - **`👾 Ẩn/Hiện quái` · `🧍 Ẩn/Hiện NV & PK` · `🎒 Túi Dọc/Ngang` · `⚡ Đánh xóa anim` · `🌑 Màn đen VPS`** — Bật/tắt nóng ngay trong lúc farm.
  - **`↺ Reset Done`** — Bỏ đánh dấu Xong để cho acc chạy lại.
- **Mẹo hay:** Bấm vào cột **`Đồ SKH · Sao`** của bất kỳ acc nào để mở bảng chi tiết **Ma trận 0-0-0-0-0** và danh sách từng món SKH/Sao đang nằm ở Hành trang hay Rương nhà!

---

## 4. Tab 2 — Farm Profiles

Mỗi **Farm Profile** là 1 bộ luật farm độc lập. Bạn có thể tạo nhiều Profile khác nhau (ví dụ: 1 Profile cho dàn TDLT, 1 Profile cho dàn Map Riêng, 1 Profile Up Đệ, 1 Profile treo VPS) và gán cho từng nhóm acc.

> 💡 **Đồng bộ LIVE:** Sửa bất kỳ mục nào trong Profile rồi bấm **`💾 Lưu & Đồng Bộ Live`**, toàn bộ tab đang dùng Profile đó (kể cả tab **👻 Treo ngầm**) sẽ nhận cài đặt mới ngay lập tức mà **không cần tắt game**!

### 4.1. 5 Profile mẫu nạp sẵn
1. **`TDLT — Up SKH Chuẩn`**: Sư phụ dùng TDLT + Cỏ 4 lá, tự cất mọi SKH & đồ ≥ 3 sao, full túi bán rác Map 24, tự đổi khu vắng.
2. **`Sư Phụ Đánh Thường (Free Ngọc)`**: Không tốn ngọc mua TDLT/Cỏ, tự đánh quái nhặt đồ SKH & ≥ 3 sao.
3. **`Up Đệ Tử Săn SKH`**: Bảo vệ đệ, tự sửa đệ lười, hỗ trợ Wiggle nhích qua lại chống đứng im.
4. **`Map Riêng (Vé 1823/1825)`**: Tự mua vé khi còn mầm, kiểm tra xong dùng vé `[1825]` vào map riêng ngay từ bất cứ map nào.
5. **`VPS 1-1 / 1-2 Siêu Nhẹ`**: Cài sẵn **👻 Treo ngầm Headless**, FPS 10, ẩn quái & nhân vật, tối ưu cho VPS yếu.

### 4.2. Các mục quan trọng cần biết trong Profile

#### A. Map & Chống KS Khu
- **Map Trái Đất / Namek / Xayda:** Tự chọn đúng map theo hành tinh nhân vật (nếu trên acc để `Theo Profile`).
- **Chiến thuật Khu:** `Tự tìm khu vắng nhất` hoặc `Khu cố định`.
- **Đổi khu nếu người > X:** Tự động dò danh sách khu thật từ server và nhảy sang khu ít người nhất.
- **Watchdog không nhảy TN (2 tầng):**
  - Không thấy nhảy tiềm năng sau **X giây** (vd `35s`) → tự đổi khu vắng.
  - Vẫn không nhảy sau **Y giây** (vd `95s`) → tự thoát tab để Watchdog mở lại sạch sẽ.

#### B. Luật Lọc Đồ SKH & Đồ Sao
- **Giữ tất cả các bộ Set Kích Hoạt:** Bật = giữ sạch 14 bộ SKH; Tắt = chỉ giữ đúng các bộ bạn tick bên dưới (các bộ SKH cùi tự bán rác luôn!).
- **Giữ đồ Sao pha lê từ ≥ N sao:** Chọn `0` (chỉ giữ SKH) hoặc `≥ 1 / 2 / 3 / 4 sao`.
- **Khi Rương Nhà Đầy:**
  - `Kệ rương đầy — Cứ đánh tiếp` *(tận dụng hành trang ẩn)*.
  - `Đánh dấu Xong → Tắt acc → Xoay acc kế tiếp`.
  - `Đứng yên ở nhà chờ`.
- **Tự về nhà cất đồ / Full túi qua Map 24 bán rác / Nhặt tất cả / ID luôn nhặt-bán-vứt:** Tuỳ chỉnh linh hoạt theo từng server hoặc sự kiện.

#### C. Buff, TDLT, Cỏ 4 Lá, Danh Hiệu & Sinh Tồn
- **Tự mua & dùng TDLT:** Chọn gói `521 (1 ngọc)`, `1523 (9 ngọc)`, `1524 (22 ngọc)` và mốc phút tối thiểu.
- **Tự mua & dùng Cỏ 4 lá (1635):** Tự ra Santa mua khi hết buff Cỏ.
- **🎫 Tự mua Vé Map Riêng (1825) khi còn mầm:** Dùng cho chế độ `Map Riêng (Vé 1825)` — kiểm tra/mua xong ở Santa hay Trạm tàu là dùng vé `[1825]` nhảy thẳng vào map riêng luôn, không chạy thừa về map cũ.
- **🪙 Tự đổi Thỏi Vàng (457):** Tự đổi khi vàng ≥ 1.5 tỷ tránh tràn giới hạn 2 tỷ vàng.
- **🏅 Tự nhận & gia hạn Danh hiệu "Tay nhanh hơn não" (+20% May mắn, HSD 7 ngày):** Tự kiểm tra Santa → mua đúng số Loa LVT `[1653]` còn thiếu → chat đủ 10 loa → nhận & đeo danh hiệu `[1673]`.
- **Giữ lại tối thiểu (Ngọc):** Chốt an toàn không bao giờ tiêu lố số ngọc muốn chừa lại.
- **Ăn đậu HP/MP, Nho tím thể lực, Xin đậu Bang (5p/lần), Tự cộng chỉ số tiềm năng.**

#### D. 👊 Chọn Chiêu Đánh Quái (Tàn sát) & Tối Ưu Chiến Đấu
Nằm trong khung **`⚡ Tối Ưu Hiệu Năng Client & Chiến Đấu`**:
- **👊 Chiêu đánh quái khi Tàn sát** *(đồng bộ Live)*:
  1. **`👊 Chỉ Đấm cơ bản (Dragon / Demon / Galick)`** *(Mặc định)* — Khóa cứng chỉ dùng chiêu Đấm cơ bản theo hành tinh, **tuyệt đối không xài Chưởng**, tiết kiệm tối đa KI & đậu khi up SKH.
  2. **`🥊 Đấm + Kaioken / Liên Hoàn`** — Bỏ hoàn toàn Chưởng.
  3. **`⚡ Tự động (Đấm + Chưởng + Liên Hoàn)`** — Xen kẽ mọi chiêu đánh để ép tối đa nhịp đánh/phút.
  4. **`🎯 Tự chọn theo Ô phím trên thanh Skill (Ô 1 – Ô 5)`** — Hiện 5 ô tick `Ô 1` đến `Ô 5` tương ứng thanh phím kỹ năng trong game, muốn đánh ô nào tick đúng ô đó!
- **⚡ Đánh quái xóa anim:** Gửi trực tiếp gói tấn công không chờ khung hình hiệu ứng; khi có TDLT và tắt `Dịch đến quái khi TDLT`, nhân vật đứng yên 1 chỗ đánh toàn map ở tốc độ tối đa.
- **❄️ Đóng băng quái tại chỗ:** Quái không chạy lung tung giúp gom mục tiêu gọn.
- **👾 Ẩn hình quái / 🧍 Ẩn NV & phụ kiện / 🌑 Màn đen VPS / 👻 Treo ngầm Headless:** Giảm tải tối đa cho VPS.

---

## 5. Tab 3 — Kho SKH & Ma Trận Ghép Set 5 Món

- Tự động gom toàn bộ đồ SKH trong **Hành trang + Rương nhà** của cả dàn acc vào bảng tổng hợp **14 bộ SKH chuẩn NRO** (Trái Đất: Songoku, Kirin, Thiên Xin Hăng, Kaio · Namek: Ốc Tiêu, Pikkoro Daimao, Picolo, Nail · Xayda: Kakarot, Cadic M, Cadic, Nappa · Dùng chung: Gohan, Thần Hủy Diệt Champa).
- **Quy ước mã 5 số (`Áo - Quần - Găng - Giày - Rada`):**
  - Ví dụ `1-0-2-1-0` nghĩa là đang có **1 Áo, 0 Quần, 2 Găng, 1 Giày, 0 Rada**.
  - Rê chuột lên từng ô để xem **chính xác acc nào đang giữ món đó**.
  - Bộ nào đủ cả 5 món tự hiện huy hiệu **`🏆 ×1`** (hoặc `×2`, `×3`...).
- Có khung **Lịch sử nổ SKH** ghi theo thời gian thực kèm acc, server và món vừa rớt.

---

## 6. Tab 4 — Proxy Manager (HTTP & SOCKS5)

- **Hỗ trợ dán mọi định dạng phổ biến** (mỗi dòng 1 proxy):
  ```text
  103.1.2.3:8080
  103.1.2.3:8080:user:pass
  user:pass@103.1.2.3:8080
  socks5://user:pass@103.1.2.3:1080
  http://103.1.2.3:8080
  ```
  *(Nhận cả định dạng dán ngược `user:pass:ip:port`, `ip:port@user:pass`, cách nhau bằng `:`, `|`, `,`, `;` hoặc khoảng trắng.)*
- Bấm **`⚡ Check Live & Đo Ping (16 luồng)`** để kiểm tra nhanh toàn bộ danh sách và **`🗑 Xóa Proxy Die`** cho gọn.
- Khi acc chọn Proxy = **`Tự động phân bổ`**, tool tự chia đều tối đa `MaxAccPerProxy` acc trên mỗi Proxy Live (cài ở Tab 5) và tự xoay sang Proxy Live dự phòng nếu có Proxy bị Die.

---

## 7. Tab 5 — Cấu hình VPS, Lịch Ca, Bảo Trì Thông Minh & Telegram

### 7.1. Cấu hình Đa Tab & Xả RAM 3 Chốt
- **MaxTabs:** Số tab chạy song song khi bật Auto Xoay Vòng.
- **Giãn nhịp mở tab Min / Max (ms):** Mặc định `2000–4000 ms` để tránh mở dồn làm lag CPU/Proxy.
- **Circuit Breaker:** Nếu 1 acc crash/mất kết nối quá `N lần / 10 phút` (mặc định 5 lần), tool tự tạm dừng acc đó và báo đỏ để tránh spam đăng nhập.
- **Ngưỡng xả RAM tự động / tab (MB):** Mặc định `380 MB` (`0` = tắt). Áp dụng **3 chốt chống tắt oan**:
  1. Vượt ngưỡng → xả nhẹ rác Unity GC + `EmptyWorkingSet` trước.
  2. Chỉ restart mềm nếu vẫn giữ vượt ngưỡng **liên tục ≥ 3 phút**.
  3. **Chỉ restart khi đang ở trạng thái `FARM`** (tuyệt đối không cắt ngang lúc cất đồ, bán rác hay mua buff).

### 7.2. 📅 Lịch Khung Giờ Chạy Tự Chọn (Nhiều Ca)
- Tắt = chạy 24/7.
- Bật = bấm **`＋ Thêm ca`** để chia bao nhiêu ca tùy thích, hỗ trợ **ca vắt qua nửa đêm** (ví dụ Ca 1: `04:30 → 12:30`, Ca 2: `16:30 → 00:30`).
- Hết ca tự đóng sạch toàn bộ tab để VPS nghỉ & xả 100% RAM; đúng giờ ca mới tự mở lại có giãn nhịp.

### 7.3. 🌙 Bảo Trì Thông Minh (Luôn bật 24/7)
- Khi máy chủ NRO phát thông báo sắp bảo trì trước `30 phút / 15 phút / 5 phút`: tool ghi nhận nhưng **vẫn tiếp tục farm** cho đỡ phí thời gian.
- Ngay khi máy chủ báo còn **≤ 1 phút** (hoặc gặp đợt bảo trì đột xuất vài phút):
  1. Tool **tự đóng sạch toàn bộ tab** cho VPS nghỉ (không tính lỗi Circuit Breaker).
  2. Chờ server tắt hẳn rồi **tự dò bắt tay chuẩn giao thức NRO (`cmd -27`) mỗi 60 giây** (chỉ mở lại khi server thực sự đã khởi động xong và nhả khóa mã hóa, không bị lừa bởi cổng TCP mở sớm).
  3. Server vừa mở lại → **tự động bung lại toàn bộ dàn acc**!
- Ngoài ra vẫn có thể tick thêm **Khung giờ nghỉ cố định dự phòng** nếu muốn nghỉ cứng giờ cố định mỗi ngày.

### 7.4. 📣 Thông Báo Telegram Real-Time
1. Tạo bot tại `@BotFather` trên Telegram để lấy **Bot Token**.
2. Lấy **Chat ID** của bạn (qua `@userinfobot` hoặc nhóm riêng).
3. Dán vào Tab 5 → bấm **`✈ Gửi Tin Test`** để kiểm tra → tick các loại cảnh báo muốn nhận (`Rớt đồ SKH`, `Đủ 5/5 Set`, `Full rương nhà`, `Sắp hết ngọc`) → **`💾 Lưu Cài Đặt Hệ Thống`**.

---

## 8. Chế độ Treo Ngầm Headless & Soi X-Quang Live

### Cách dùng cực gọn
- **Mở treo ngầm:** Bấm nút **`👻`** ngay trên dòng acc (hoặc tick nhiều acc → bấm **`👻 Treo ngầm`**). Game chạy hoàn toàn không cửa sổ (`0% GPU`, `~0.2% CPU`, `~10–25 MB RAM / tab`).
- **Mở lại có hình để cầm tay:** Khi cần xem tận mắt, mua bán, hoặc giao dịch đồ → bấm nút **`👁`** trên dòng acc (hoặc nút **`👁 Mở có hình cầm tay`** trong cửa sổ X-Quang). Tool sẽ tự đóng phiên ngầm sạch sẽ, chờ 2.5s cho server NRO xả phiên cũ rồi bật cửa sổ game lên (không bị lỗi đăng nhập chồng!).
- **Kiểm tra acc đang treo ngầm có đánh thật không (`🩻 Soi X-Quang Live`):**
  Bấm nút **`🩻`** trên dòng acc (hoặc xem badge xanh **`⚡0s · 87/p`** ngay trên bảng):
  - **Nhịp ăn TN:** Hiện xanh `⚡ Đang ăn TN sống (0s trước)` = đang đánh ra tiềm năng thật 100%!
  - **Tốc độ farm:** Số nhịp ăn TN/phút + tổng nhịp cả phiên.
  - **Mục tiêu & Chiêu đang dùng:** Biết chính xác đang đánh con quái nào, còn bao nhiêu máu, và đang dùng chiêu gì (`Đấm Galick (Chỉ Đấm)`...).
  - **Buff & Tài nguyên:** TDLT còn bao nhiêu phút, Cỏ 4 lá bật chưa, **🎫 Vé Map Riêng** còn bao nhiêu ngày/giờ/phút, Túi/Rương và Ngọc xanh/hồng.
  - **25 Bước Hoạt Động Gần Nhất:** Theo dõi từng bước từ lúc mở tab, nối Socket, vào map, đổi khu, cất đồ, bán rác hay lỗi phát sinh.

---

## 9. Phím tắt nhanh trong Game & QLTK

### Trong cửa sổ Game (chế độ có hình)
| Phím | Chức năng |
|---|---|
| `B` | Bật / Tắt **Màn đen VPS** (tắt vẽ bản đồ) |
| `M` | **Ẩn / Hiện hình ảnh quái** |
| `N` | **Ẩn / Hiện nhân vật & phụ kiện** (cải trang, đeo lưng, ván bay, danh hiệu, linh thú) |
| `G` | Chuyển **Hành trang Dọc / Ngang (lưới 5 cột)** |
| `K` | Bật / Tắt **Đánh quái xóa anim** |
| `P` | **Tạm dừng / Tiếp tục Auto** |
| `Ctrl + Shift + H` | Kiểm tra xác thực bản gốc trong game |

### Trên bảng điều khiển QLTK
| Phím | Chức năng |
|---|---|
| `Ctrl + Alt + Shift + H` | Kiểm tra xác thực bản gốc QLTK |
| Chuột phải lên dòng acc | Mở menu điều khiển nhanh đầy đủ |

---

## 10. Cấu hình khuyến nghị VPS & Câu hỏi thường gặp (FAQ)

### Mức tài nguyên tham khảo khi chạy Treo Ngầm Headless
- **VPS 1 Core / 1 GB RAM:** Chạy mượt **4–6 tab Headless** (`MaxTabs = 5`, Profile `VPS 1-1 / 1-2 Siêu Nhẹ`).
- **VPS 1 Core / 2 GB RAM:** Chạy mượt **8–12 tab Headless**.
- **VPS 2 Core / 4 GB RAM:** Chạy mượt **18–25 tab Headless** *(nên gắn Proxy theo tỷ lệ 2–3 acc / 1 proxy để tránh giới hạn IP của server)*.

### Câu hỏi thường gặp (FAQ)

**1. Bấm `👻 Treo ngầm` xong mở lại `👁 Mở có hình` có cần thoát game không?**
- Có, Unity bắt buộc đổi cờ khởi động giữa chế độ không đồ họa (`-batchmode -nographics`) và chế độ có cửa sổ. Tool đã **tự động làm trọn gói** (đóng kết nối sạch → chờ 2.5s cho server xả phiên → tự bật lại và đăng nhập vào đúng map farm).

**2. Sao mình đóng cửa sổ trình duyệt mà game vẫn chạy?**
- Đây là thiết kế chuẩn treo 24/7: QLTK chạy ngầm ở **khay hệ thống (System Tray)** để tốn ~0% CPU khi bạn không nhìn vào bảng. Muốn tắt hoàn toàn tool + game hãy bấm **`⏻ Thoát Hoàn Toàn`**.

**3. Acc báo lỗi đỏ `Sai mật khẩu / Tài khoản bị khóa` thì có bị spam đăng nhập không?**
- Không! Tool nhận diện lỗi nghiêm trọng và **khóa tự động đăng nhập lại ngay lập tức**, bắn cảnh báo về Telegram (nếu bật). Khi bạn sửa lại mật khẩu xong bấm **`▶ Chạy`** lại bình thường.

**4. Làm sao để acc chỉ Đấm mà không dùng Chưởng tốn KI?**
- Vào **Tab 2 (`Farm Profiles`)** → khung **`⚡ Tối Ưu Hiệu Năng Client & Chiến Đấu`** → chọn **`👊 Chỉ Đấm cơ bản`** → bấm **`💾 Lưu & Đồng Bộ Live`**. Mặc định tất cả Profile Up SKH đều đã đặt sẵn **`👊 Chỉ Đấm cơ bản`**.

**5. Chuyển tool sang máy khác / VPS khác mà không lộ acc cá nhân làm thế nào?**
- Không copy thư mục `data/` nếu gửi cho người khác. Chạy **`pack_release.bat`** để xuất file `release\ModX_HTeam_AutoSKH_Public.zip` sạch 100% (đã lọc sạch `data/` và thông tin đăng nhập tạm trong `game/Data/rms`).
