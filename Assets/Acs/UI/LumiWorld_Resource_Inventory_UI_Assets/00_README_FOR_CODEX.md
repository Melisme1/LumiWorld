# Context ngắn cho Codex — LumiWorld Resource & Inventory UI, bước 9

Đọc `LumiWorld_Resources_Inventory_Step_By_Step.md`, kiểm tra services/classes và scene hiện có, rồi triển khai UI trong MapBuilding. Stage hiện tại: 7 species, 15 resources thuộc Leafwood, Bloomfield, Windheath.

**Định hướng đã chốt:** fantasy sáng, dễ đọc theo tinh thần Genshin/Aniimo; nền kem/ngà, xanh sage, điểm nhấn vàng/lá. Habitat Panel ở bên phải, không modal; ngoài panel vẫn thao tác map. Warehouse và Resource Selector là modal, ESC đóng tầng trên cùng.

**Assets:** 12 PNG trong `UI_PNG/`; xem `01_UI_ASSET_LIST.md`. Copy vào `Assets/Acs/UI/Skins/IvorySage`, import theo `02_UNITY_IMPORT_GUIDE.md` và `03_UI_SPRITE_IMPORT_SETTINGS.json`. Panel/nút chữ dùng Sliced; slot/icon dùng Simple. Giữ 15 resource icons hiện có ở `Assets/Acs/Icons`, ghép vào nền slot riêng. Text/số dùng TMP. Selected border tắt Raycast Target; locked ghép icon khóa và lý do.

**UI cần nối:**

- Kho: tên/icon, số đã thu, biome và T1/T2 của 15 resources.
- Habitat: Nature/affinity, hex/capacity/residents, slots, selections, rate, buffer, lý do thiếu, Thu tất cả.
- Selector: catalog đúng biome/tier, species đủ/thiếu, primary/secondary, Áp dụng/Hủy.
- Receipt: lượng thực nhập kho; kho đầy thì báo phần còn chờ.
- Offline summary: buffer chờ thu. Recovery claim gọi cùng collect service.

**Logic:** số trong kho khác buffer; offline không tự nhập kho. UI đọc model và gọi services, không tự cộng stock hoặc tính production. Eligibility/slot limits lấy từ core: 1–2 hex chưa mở, 3 hex = 1 T1, 4–5 hex = 2 T1, 6 hex = 2 T1 + 1 T2; 1 creature/hex, nhóm tối đa 6. Giữ species/resource IDs và mapping hiện có; bỏ test cow/fox/canoc, Craggle thiếu model.

**Tích hợp:** reuse Canvas, EventSystem, EconomyHUD; prefab trong `Assets/Acs/Prefabs/UI`. Chặn input map trong vùng UI/modal; production tiếp tục theo clock. Kiểm tra 1280×720 và 1920×1080, đủ 15 icons, tiếng Việt, kho đầy/thiếu species, đóng/mở không reset.

Gói này chỉ có skin/tài liệu; chưa có prefab, scripts hoặc Unity Play Mode test. Border/PPU multiplier là đề xuất cần review. Nếu đã import bản tên cũ, rename/move trong Unity để giữ `.meta`/GUID; `previous_filename` trong JSON hỗ trợ đối chiếu.
