# Quy tắc làm việc của dự án LumiWorld

Các quy tắc dưới đây do chủ dự án yêu cầu ngày 10/10/2026.

## Code có khả năng tái sử dụng

- Trước khi viết mới hoặc mở rộng đáng kể một class/hệ thống có khả năng dùng cho nhiều mục đích, kể cả trong tương lai, nêu rõ khả năng đó và hỏi chủ dự án các mục đích dự kiến nếu chưa được xác nhận trong cuộc trao đổi hoặc tài liệu quyết định. Không tự giới hạn class vào tính năng đang làm hoặc tự giả định các use case tương lai.
- Tái sử dụng các câu trả lời và phạm vi đã được xác nhận; không hỏi lại cùng nội dung. Làm rõ những use case mới có ảnh hưởng thực sự đến thiết kế trước khi triển khai phần phụ thuộc.
- Tách logic chung khỏi quy tắc gameplay cụ thể, UI, MonoBehaviour, nguồn thời gian và nơi lưu dữ liệu khi chúng có trách nhiệm/vòng đời khác nhau. Ví dụ timer chung không tự biết tên biome, recipe cho ăn hoặc loại obstacle.
- Giữ code sạch, trách nhiệm rõ, API dễ hiểu và cấu hình tách khỏi logic. Đặt tên theo trách nhiệm, tránh gắn tên stage vào code dùng lâu dài.
- Chỉ tạo mức trừu tượng cần thiết cho các use case đã xác nhận; không tự xây framework cho các tình huống chưa được yêu cầu.

## Phạm vi planning hiện tại

- Yêu cầu về timer, chăm sóc, obstacles và save/cloud hiện đang ở bước phân tích/định hướng. Không xem việc chủ dự án trả lời câu hỏi planning là lệnh triển khai gameplay.
- Quyết định đã thống nhất và các điểm còn mở được ghi trong `D:/Dev/LumiWorld/docs/LumiWorld_Time_Incidents_Creature_Care_Plan.md`. GDD có thể chưa cập nhật các thay đổi hướng thử nghiệm; ưu tiên quyết định trực tiếp mới nhất của chủ dự án.
