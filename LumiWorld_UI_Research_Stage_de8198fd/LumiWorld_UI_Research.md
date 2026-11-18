# LumiWorld — Khảo sát UI và reference

Cập nhật 15/11/2026. Giai đoạn 1–2, đang khảo sát. Chưa chốt phong cách.

## Mục tiêu

Tìm một ngôn ngữ UI thống nhất cho game xây đảo hex và chăm Lumi. Giữ đảo dễ quan sát khi đặt thẻ hoặc kiểm tra biome. Cho phép người chơi tìm công thức, nguyên liệu, hàng chờ và hành động chăm sóc từ đúng ngữ cảnh.

## Hiện trạng từ 20 ảnh

- Inventory có lưới, bộ lọc, chi tiết vật phẩm khi chọn. Workshop có danh sách công thức, bảng chi tiết và Queue dưới dạng tab. Ảnh chỉ có Queue rỗng.
- Card Shop đang trộn Creature và Terrain trong danh sách hai cột. Order Board đặt đơn giao hàng chung màn với bán nguyên liệu. Tay thẻ và HUD chỉ lộ một phần.
- Có hai nhóm thao tác tại đảo là Habitat và Creature. Harvest Resource có chọn tài nguyên và bảng chi tiết. Có ảnh Automatic Feeding ba slot và ảnh Creature hiển thị Warehouse/Pantry, cần xác nhận đường vào hiện hành ở lượt tiếp.
- Achievements, Settings và Creation Lab đều có ảnh. Creation Lab chỉ có trạng thái một buồng trống, hai buồng khóa.
- Dấu hiệu từ ảnh gồm nhiều kiểu nút đóng, tab vuông và tab viên thuốc, nền panel khác nhau, thông tin công thức lặp lại và trạng thái khóa chiếm nhiều chỗ.

## Reference ưu tiên

| Game | Màn cần xem | Quyết định được hỗ trợ |
| --- | --- | --- |
| [Fae Farm](https://foxrocket.com/faefarm.html) | Inventory, House Storage, Craft and Conversion, Animal Care, Shop, Settings | Một hệ component cozy áp dụng được cho nhiều menu |
| [Preserve](https://www.preserve-game.com/) · [Ảnh gameplay có tay thẻ](https://image.api.playstation.com/vulcan/ap/rnd/202503/2513/1234ab48b3f11879295b855dc759979184969a9efcd0febf.jpg) | Harmony gameplay, tay thẻ, preview đặt | Tỉ lệ HUD/đảo, nhận diện thẻ và phản hồi đặt |
| [TerraScape](https://store.steampowered.com/app/2290000/TerraScape/) | Tay thẻ và màn chọn/mở deck | Nhận diện nhóm thẻ và trình bày trạng thái khóa |
| [Palia](https://gamertweak.com/repair-upgrade-tools-palia/) | Worktable, công thức thiếu nguyên liệu | Danh sách → chi tiết → điều kiện → hành động |
| [Genshin Impact](https://cdn.gosu-noob.com/static/img/1/2021/10/Starconch-Locations-Genshin-Impact.jpg) | Materials, lưới và chi tiết Starconch | Biến thể UI toàn màn hình, phân lớp dữ liệu |
| [Roots of Pacha](https://rootsofpacha.wiki.gg/wiki/Crafting) · [Coral Island](https://www.rpgfan.com/gallery/coral-island-screenshots/) | Inventory → Crafting, thông tin theo item, trạng thái đủ/thiếu | Liên kết công việc và tiết lộ chi tiết khi cần |
| [Aniimo](https://gamewith.ai/aniimo/en/articles/how-to-hatch-aniimo-eggs) · [Sandrock](https://www.gamepressure.com/my-time-at-sandrock/commissions/z1fe15) | Hatchinator, Commission Board | Trạng thái buồng ấp và thông tin quyết định trên đơn |
| [Animal Crossing](https://interfaceingame.com/games/animal-crossing-new-horizons/) · [Hay Day](https://support.supercell.com/hay-day/en/articles/products.html) | Nook Miles, Recipe Inventory, Bakery | Thành tựu dễ quét, sản xuất đang chạy và chờ nhận |

## Cặp phương án để so ở bước sau

- Cozy, ấm và có chất liệu nhẹ. Xem Fae Farm và TerraScape để so cách dùng nền, viền và card frame. Đánh đổi chính là effort làm bộ icon/khung đồng nhất và nguy cơ texture làm giảm độ đọc.
- Sạch, kho và xưởng toàn màn hình. Xem Genshin và Palia để so độ tương phản, thứ bậc và tiết lộ theo lựa chọn. Đánh đổi chính là ngắt tầm nhìn đảo và phải khôi phục ngữ cảnh khi quay lại.
- Xem Preserve và Dorfromantik riêng cho tương tác trên đảo. Đem cùng một luồng Oak Wood → Bark Paper ra so ở hai cách trình bày, giữ nguyên nội dung và thao tác.

## Ảnh LumiWorld cần cho lượt so sánh

HUD khi chưa mở panel, tay thẻ lúc hover/chọn thẻ và preview đặt hex. Queue lúc có đơn đang chạy và có món chờ nhận. Luồng chăm Lumi hiện hành từ lúc chọn cư dân đến nạp thức ăn. Ảnh/video ngắn giúp kiểm tra đường vào, lý do bị khóa và điều gì xảy ra khi quay lại.
