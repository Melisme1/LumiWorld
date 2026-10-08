# Import và ghép UI trong Unity

## 1. Vị trí và import

Copy 12 PNG vào `Assets/Acs/UI/Skins/IvorySage`. Giữ icon resource hiện có ở `Assets/Acs/Icons`; bộ này không thay thế 15 resource icon. Prefab UI mới theo tài liệu ở `Assets/Acs/Prefabs/UI`.

Thiết lập đề xuất: Texture Type = Sprite (2D and UI); Sprite Mode = Multiple, mỗi PNG chỉ tạo một sprite; Mesh Type = Full Rect; Pixels Per Unit = 100; Alpha Source = Input Texture Alpha; Alpha Is Transparency bật; Generate Mip Maps tắt; Filter Mode = Bilinear; Wrap Mode = Clamp; Max Size = 4096; Compression = None cho lần kiểm tra đầu. Kiểm tra platform override không tự giảm ảnh xuống 1024. Sau khi nghiệm thu mới tối ưu texture/atlas.

PNG có alpha thật; màu RGB trong pixel alpha bằng 0 có thể khiến một số trình xem hiển thị nền xanh/đen. Trong Unity, hãy kiểm tra trên cả nền sáng và nền tối bằng alpha từ texture. Không đặt vật liệu bỏ qua alpha.

## 2. Sprite Rect và border

Trong Sprite Editor, tạo một rect, đặt tên theo key trong manifest, nhập X/Y/W/H bên dưới, pivot Center và border. Apply. X/Y trong bảng là gốc dưới-trái của Unity; không phải gốc trên-trái của trình xử lý ảnh.

Rect đề xuất tính từ vùng alpha ≥ 8/255, cộng đệm 2 px. Điều này bỏ padding và phần nhiễu alpha rất thấp ở xa hình. PNG gốc vẫn giữ nguyên. Trim có thể là điểm bắt đầu, nhưng rect thủ công trong bảng ổn định hơn cho những ảnh có pixel mờ ở ngoài.

Border theo thứ tự **L, B, R, T**; đơn vị pixel trong rect đã chọn. Các giá trị là điểm bắt đầu để review trong Unity, chưa được kiểm chứng qua Play Mode.

| PNG | Canvas gốc | Rect X, Y, W, H | Border L, B, R, T | Image Type | PPU multiplier bắt đầu |
| --- | --- | --- | --- | --- | --- |
| `UI_Panel_Habitat_Background.png` | 1024×1536 | 154, 16, 715, 1504 | 120, 60, 120, 130 | Sliced | 2.25 |
| `UI_Window_Modal_Background.png` | 1536×1024 | 30, 72, 1476, 883 | 128, 128, 128, 128 | Sliced | 2.5 |
| `UI_Panel_InfoInset_Background.png` | 1536×1024 | 60, 124, 1416, 777 | 96, 96, 96, 96 | Sliced | 4 |
| `UI_Button_Primary_Background.png` | 2172×724 | 0, 121, 2172, 483 | 210, 190, 210, 190 | Sliced | 8 |
| `UI_Button_Secondary_Background.png` | 2172×724 | 51, 162, 2072, 414 | 220, 170, 220, 170 | Sliced | 8 |
| `UI_Slot_Resource_Default_Background.png` | 1254×1254 | 82, 94, 1090, 1066 | 0, 0, 0, 0 | Simple | 1 |
| `UI_Slot_Resource_Selected_Border.png` | 1254×1254 | 77, 82, 1102, 1090 | 0, 0, 0, 0 | Simple | 1 |
| `UI_Slot_Resource_Locked_Background.png` | 1254×1254 | 67, 72, 1120, 1113 | 0, 0, 0, 0 | Simple | 1 |
| `UI_Button_Icon_Round_Background.png` | 1263×1246 | 12, 20, 1236, 1219 | 0, 0, 0, 0 | Simple | 1 |
| `UI_Icon_Warehouse.png` | 1358×1159 | 129, 87, 1131, 930 | 0, 0, 0, 0 | Simple | 1 |
| `UI_Icon_Close.png` | 1254×1254 | 195, 166, 859, 860 | 0, 0, 0, 0 | Simple | 1 |
| `UI_Icon_Lock.png` | 1224×1285 | 265, 214, 694, 888 | 0, 0, 0, 0 | Simple | 1 |

Các thông số tọa độ giả định ảnh được import nguyên độ phân giải. Không tự dùng border của toàn canvas nếu đã chọn rect nhỏ hơn.

## 3. Dùng Sliced và Simple

Panel, cửa sổ, inset và hai nút chữ: dùng uGUI Image Type = Sliced, Fill Center bật. Để góc lá nằm trọn trong vùng border; chỉ phần giữa và cạnh thẳng được kéo giãn. Giữ Preserve Aspect tắt cho các khung này.

Ảnh master lớn nên góc/viền có thể quá lớn khi đưa vào một nút nhỏ. Với Sprite PPU = 100 và Canvas Reference Pixels Per Unit = 100, thử `Image.pixelsPerUnitMultiplier` trong bảng rồi chỉnh theo Game View. Multiplier lớn hơn làm chi tiết border nhỏ hơn trong UI. Nếu Canvas hiện tại dùng reference PPU khác, điều chỉnh theo Canvas đó thay vì đổi cả HUD. Không dùng Set Native Size để quyết định kích thước nút.

Ba slot, viền selected, nút tròn và icon: dùng Simple, Preserve Aspect bật, đặt ở rect vuông hoặc rect phù hợp. Các biến thể slot được tạo riêng nên mép không khớp pixel tuyệt đối; căn giữa, xem ở kích thước thực tế và chỉnh inset của lớp selected nếu cần. Không kéo slot thành hình chữ nhật dài.

Gợi ý bắt đầu tại hệ tọa độ UI 1920×1080: slot 88–104 px; icon resource nằm trong khoảng 60–68% cạnh slot; số lượng ở góc phải dưới; nút chữ cao khoảng 56–64 px; nút tròn 44–56 px; icon Đóng 18–22 px, Kho 28–34 px. Đây là gợi ý thiết kế, cần điều chỉnh theo Canvas hiện có.

## 4. Lớp của slot và nút

Slot thường: nền `06_slot_resource` → resource icon → số lượng/nhãn. Slot chọn: thêm `07_slot_selected` ở phía trước nền; tắt Raycast Target trên viền để nó không chặn click. Slot khóa: `08_slot_locked` + tint muted nhẹ + `12_icon_lock` + dòng giải thích như “Cần nhóm 6 hex”. Một slot đã mở nhưng chưa đủ species phải hiển thị lý do tương ứng, không dùng chung lý do chưa mở.

Đặt mọi thành phần trang trí, icon con và TMP label thành Raycast Target = false. Hit area của nút/slot do một Image hoặc Graphic tương tác trên parent xử lý. Nền panel vẫn cần chặn input map trong vùng panel.

Nút chữ ghép TMP label riêng. Dùng chữ xanh đậm trên nền ngà; thử trắng/ngà trên nút xanh và kiểm tra độ đọc ở kích thước thật. Dùng font TMP có tiếng Việt đầy đủ. Normal tint trắng; hover/pressed/disabled dùng Color Tint và review tương phản. Các giá trị tint cần chọn trong Unity, tránh áp màu xanh lần nữa lên ảnh đã xanh.

Nút Kho: `09_button_icon` + `10_icon_warehouse`. Nút Đóng: `09_button_icon` + `11_icon_close`. Dimmer modal dùng một uGUI Image màu tối với alpha thấp, không cần PNG mới.

## 5. Bố cục theo bước 9

Habitat Panel anchor bên phải, gợi ý rộng 360–400 đơn vị ở reference resolution 1920×1080. Chừa vùng HUD trên và Hand dưới. Panel có thể cuộn nội dung khi màn hình thấp. Ngoài panel người chơi vẫn chọn nhóm khác hoặc thao tác map; pointer bên trong panel không đặt thẻ/pan camera.

Kho và Resource Selector là modal: dimmer chặn map, ESC đóng đúng tầng trên cùng. Receipt dùng inset nhỏ; offline summary/recovery claim dùng window chung. Reuse Canvas, EventSystem, EconomyHUD hiện có và chọn các anchor dựa trên layout thật. Chưa có ảnh Game View hiện tại để xác nhận vị trí không che Hand/Order/Shop.

Hiển thị rõ “Đã thu trong kho” và “Đang chờ thu”. Offline summary là buffer chờ thu. Harvest receipt chỉ báo lượng thực nhập kho; kho đầy thì báo phần còn chờ. Skin không thay đổi logic production/collect.

## 6. Kiểm tra trong dự án

- Xem alpha trên nền sáng/tối; không có nền vuông ngoài shape.
- Resize panel/window/buttons: góc không bị kéo và chữ không đè trang trí.
- Xem slot ở kích thước thật: resource icon, số lượng và selected/locked đều rõ.
- Game View 1280×720 và 1920×1080: panel cuộn được, cửa sổ không tràn, đủ 15 mục resource của stage.
- UI chặn map đúng vùng; ngoài Habitat Panel vẫn tương tác được; ESC và modal stack đúng; không nhân đôi Canvas/EventSystem.
- Đóng/mở UI không reset production/buffer; số kho và buffer khác nhau; kho đầy/thiếu species có lý do.

## Tài liệu Unity đã đối chiếu

- [Sprite Editor — vị trí rect và border](https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/sprite/sprite-editor-window-reference/reference).
- [Sprite 9-slicing — mesh và border](https://docs.unity.com/en-us/engine/6000.6/manual/unity2d/sprite/9-slice/set-sprite-9slicing).
- [uGUI Image — type, preserveAspect, pixelsPerUnitMultiplier](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Image.html).

Tên field/menu có thể khác giữa các bản Unity. Dùng tài liệu tương ứng với phiên bản của dự án.
