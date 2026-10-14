# Quy tắc làm việc của dự án LumiWorld

Các quy tắc dưới đây do chủ dự án yêu cầu ngày 10/10/2026.

## Code có khả năng tái sử dụng

- Trước khi viết mới hoặc mở rộng đáng kể một class/hệ thống có khả năng dùng cho nhiều mục đích, kể cả trong tương lai, nêu rõ khả năng đó và hỏi chủ dự án các mục đích dự kiến nếu chưa được xác nhận trong cuộc trao đổi hoặc tài liệu quyết định. Không tự giới hạn class vào tính năng đang làm hoặc tự giả định các use case tương lai.
- Tái sử dụng các câu trả lời và phạm vi đã được xác nhận; không hỏi lại cùng nội dung. Làm rõ những use case mới có ảnh hưởng thực sự đến thiết kế trước khi triển khai phần phụ thuộc.
- Tách logic chung khỏi quy tắc gameplay cụ thể, UI, MonoBehaviour, nguồn thời gian và nơi lưu dữ liệu khi chúng có trách nhiệm/vòng đời khác nhau. Ví dụ timer chung không tự biết tên biome, recipe cho ăn hoặc loại obstacle.
- Giữ code sạch, trách nhiệm rõ, API dễ hiểu và cấu hình tách khỏi logic. Đặt tên theo trách nhiệm, tránh gắn tên stage vào code dùng lâu dài.
- Chỉ tạo mức trừu tượng cần thiết cho các use case đã xác nhận; không tự xây framework cho các tình huống chưa được yêu cầu.

## Phạm vi time và gameplay hiện tại

- Chủ dự án đã yêu cầu triển khai lõi time dùng chung và tích hợp production/Order Board. Các use case đã xác nhận ở mức lõi: countdown/deadline/cooldown, lịch định kỳ, đếm thời gian làm việc; API snapshot có version để lưu/khôi phục, chưa nối world save bước 8.
- Pause thủ công dừng đồng hồ gameplay trong phiên, resume không cộng bù thời gian pause. Chuyển ứng dụng/mất focus vẫn tính thời gian. Offline theo policy từng timer; đóng game khi pause không làm dừng vĩnh viễn deadline có policy chạy offline.
- Ngày 13/10/2026 chủ dự án yêu cầu triển khai đói và API quản lý cư dân trên habitat runtime hiện có: Quality Rank I/II/III đói sau 60/90/120 giây làm việc; 1 Mâm xôi đã thu (`resource.leafwood.raspberries`) hồi phục sau 5 giây. Cấu hình Inspector, chưa Care Progress/nâng rank. Hướng dẫn: `D:/Dev/LumiWorld/docs/LumiWorld_Creature_Hunger_Setup.md`.
- Bệnh, obstacles, breeding, minigame và save/cloud/visiting vẫn là định hướng cho các lượt sau. Luật ghép/visual 3/6/10 và UI avatar mới triển khai sau đói; không suy ra authorization chỉ từ việc đã có lõi time.
- Hướng dẫn code/time đã triển khai: `D:/Dev/LumiWorld/docs/LumiWorld_Time_System_Setup.md`. Logic chung tại `Assets/Acs/Scripts/Time/Core` không phụ thuộc Unity, gameplay hoặc nơi lưu dữ liệu.
- Quyết định đã thống nhất và các điểm còn mở được ghi trong `D:/Dev/LumiWorld/docs/LumiWorld_Time_Incidents_Creature_Care_Plan.md`. GDD có thể chưa cập nhật các thay đổi hướng thử nghiệm; ưu tiên quyết định trực tiếp mới nhất của chủ dự án.

## Skill hỗ trợ dự án

- Khi cần tra quyết định hoặc đối chiếu tài liệu với code LumiWorld, dùng `lumiworld-context`. Bản nguồn: `D:/Dev/LumiWorld/docs/codex-skills/lumiworld-context/SKILL.md`; chỉ đọc các nguồn liên quan theo bản đồ trong skill.
- Khi dựng hoặc chỉnh demo 3D tương tác trên trình duyệt để thử hex, bố cục Nature hoặc môi trường, dùng `lumiworld-3d-lab`. Bản nguồn: `D:/Dev/LumiWorld/docs/codex-skills/lumiworld-3d-lab/SKILL.md`. Các demo không tự xác nhận art cuối cùng hoặc cho phép triển khai gameplay.
- Khi sửa, debug hoặc review code Unity và tích hợp scene/Inspector, dùng `lumiworld-unity-workflow`. Bản nguồn: `D:/Dev/LumiWorld/docs/codex-skills/lumiworld-unity-workflow/SKILL.md`; chọn kiểm tra theo phần thay đổi và báo riêng kết quả compile, managed checks, Play Mode và hiển thị.
