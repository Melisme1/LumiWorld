# Danh sách 12 UI assets

Tất cả ảnh nằm trong `UI_PNG/`. Đây là skin rời; chữ, số lượng và icon resource được ghép riêng trong Unity. Tên mới ghi rõ loại UI, chức năng và lớp ảnh. Ảnh giữ nguyên từ bộ đã tạo.

| File PNG | Công dụng | uGUI Image |
| --- | --- | --- |
| `UI_Panel_Habitat_Background.png` | Nền panel habitat bên phải | Sliced |
| `UI_Window_Modal_Background.png` | Nền cửa sổ Kho / chọn resource / summary | Sliced |
| `UI_Panel_InfoInset_Background.png` | Nền khối thông tin / thông báo nhỏ | Sliced |
| `UI_Button_Primary_Background.png` | Nút chính: Thu tất cả / Áp dụng / Nhận | Sliced |
| `UI_Button_Secondary_Background.png` | Nút phụ: Hủy / Quay lại | Sliced |
| `UI_Slot_Resource_Default_Background.png` | Nền slot tài nguyên thường | Simple |
| `UI_Slot_Resource_Selected_Border.png` | Viền chọn, phần giữa trong suốt | Simple |
| `UI_Slot_Resource_Locked_Background.png` | Nền slot chưa mở, ghép icon khóa | Simple |
| `UI_Button_Icon_Round_Background.png` | Nền nút tròn để ghép icon | Simple |
| `UI_Icon_Warehouse.png` | Icon Kho | Simple |
| `UI_Icon_Close.png` | Icon Đóng | Simple |
| `UI_Icon_Lock.png` | Icon Khóa / chưa đủ điều kiện | Simple |

## Cách ghép nhanh

- Habitat Panel: Panel_Habitat + InfoInset + nút chính/phụ + slot.
- Kho / selector / offline summary: Window_Modal + nội dung động + nút.
- Resource slot: Default_Background + icon tài nguyên + số lượng; Selected_Border là lớp phủ.
- Slot chưa mở: Locked_Background + Icon_Lock + dòng lý do. Slot thiếu species cần hiển thị lý do khác với slot chưa mở.
- Nút Kho: Button_Icon_Round_Background + Icon_Warehouse. Nút Đóng: nền tròn + Icon_Close.
- Receipt: InfoInset + icon resource + lượng thực nhập kho.

Đọc `02_UNITY_IMPORT_GUIDE.md` và `03_UI_SPRITE_IMPORT_SETTINGS.json` để đặt Sprite Rect, border và PPU multiplier. T1/T2 là loại tài nguyên, không tự gán thành rarity.
