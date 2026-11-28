-- Khi thăm đảo chỉ còn gửi được Cháy rừng và Lốc xoáy; bỏ Vật cản (Obstacle) và Hạn hán (Drought).
-- Tên trùng DisasterKind phía client. Admin vẫn đổi lại được ở trang /admin (obstacle.allowedKinds).
update game_config set value = 'ForestFire,Cyclone', updated_at = now() where key = 'obstacle.allowedKinds';
