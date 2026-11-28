-- View thống kê cho dashboard (role lumi_report chỉ đọc view) và cho GET /api/v1/admin/stats/daily.
-- "Ngày" tính theo giờ Việt Nam (Asia/Ho_Chi_Minh). Mỗi view chỉ có dòng cho ngày có số liệu;
-- ngày trống do bên đọc tự điền 0. Các view *_now là ảnh chụp hiện tại, không có lịch sử theo ngày.

-- Người chơi: đăng ký mới và hoạt động (có ít nhất một hành động trong action_log, trừ thao tác admin)
create view report_players_daily as
with registered as (
    select (created_at at time zone 'Asia/Ho_Chi_Minh')::date as day, count(*) as new_players
    from players group by 1
), active as (
    select (at at time zone 'Asia/Ho_Chi_Minh')::date as day, count(distinct player_id) as active_players
    from action_log
    where player_id is not null and action not like 'admin.%'
    group by 1
)
select coalesce(r.day, a.day) as day,
       coalesce(r.new_players, 0) as new_players,
       coalesce(a.active_players, 0) as active_players
from registered r full join active a on a.day = r.day;

-- Chợ: lô đăng, lô bán, Coins giao dịch (giá cả lô), phí chợ thu được, lô hủy và hết hạn.
-- Phí lấy từ action_log market.buy vì phí đổi được và listings không lưu phí.
create view report_market_daily as
with listed as (
    select (created_at at time zone 'Asia/Ho_Chi_Minh')::date as day, count(*) as listed
    from listings group by 1
), sold as (
    select (sold_at at time zone 'Asia/Ho_Chi_Minh')::date as day, count(*) as sold, sum(price_coins) as coins_traded
    from listings where status = 'Sold' group by 1
), closed as (
    select (closed_at at time zone 'Asia/Ho_Chi_Minh')::date as day,
           count(*) filter (where status = 'Cancelled') as cancelled,
           count(*) filter (where status = 'Expired') as expired
    from listings where status in ('Cancelled', 'Expired') group by 1
), fees as (
    select (at at time zone 'Asia/Ho_Chi_Minh')::date as day, sum((details ->> 'fee')::int) as fees
    from action_log where action = 'market.buy' group by 1
), days as (
    select day from listed union select day from sold union select day from closed union select day from fees
)
select d.day,
       coalesce(l.listed, 0) as listed,
       coalesce(s.sold, 0) as sold,
       coalesce(s.coins_traded, 0) as coins_traded,
       coalesce(f.fees, 0) as fees,
       coalesce(c.cancelled, 0) as cancelled,
       coalesce(c.expired, 0) as expired
from days d
left join listed l on l.day = d.day
left join sold s on s.day = d.day
left join closed c on c.day = d.day
left join fees f on f.day = d.day;

-- Chợ theo từng món đã bán mỗi ngày (để lấy top tài nguyên và top thẻ thú)
create view report_market_items_daily as
select (sold_at at time zone 'Asia/Ho_Chi_Minh')::date as day,
       resource_id,
       resource_id like 'card.%' as is_card,
       count(*) as sold_lots,
       sum(amount) as sold_amount,
       sum(price_coins) as coins
from listings
where status = 'Sold'
group by 1, 2;

-- Đổi thẻ: đề nghị gửi trong ngày, và đề nghị kết thúc trong ngày theo trạng thái
create view report_trades_daily as
with offered as (
    select (created_at at time zone 'Asia/Ho_Chi_Minh')::date as day, count(*) as offered
    from card_trades group by 1
), resolved as (
    select (resolved_at at time zone 'Asia/Ho_Chi_Minh')::date as day,
           count(*) filter (where status = 'Accepted') as accepted,
           count(*) filter (where status = 'Declined') as declined,
           count(*) filter (where status = 'Cancelled') as cancelled,
           count(*) filter (where status = 'Expired') as expired
    from card_trades where resolved_at is not null group by 1
)
select coalesce(o.day, r.day) as day,
       coalesce(o.offered, 0) as offered,
       coalesce(r.accepted, 0) as accepted,
       coalesce(r.declined, 0) as declined,
       coalesce(r.cancelled, 0) as cancelled,
       coalesce(r.expired, 0) as expired
from offered o full join resolved r on r.day = o.day;

-- Thế giới: lượt Share đảo, lượt thăm đảo người khác, obstacle đã gửi (từ action_log)
create view report_world_daily as
select (at at time zone 'Asia/Ho_Chi_Minh')::date as day,
       count(*) filter (where action = 'world.publish') as shares,
       count(distinct player_id) filter (where action = 'world.publish') as sharing_players,
       count(*) filter (where action = 'world.visit') as visits,
       count(*) filter (where action = 'obstacle.sent') as obstacles
from action_log
where action in ('world.publish', 'world.visit', 'obstacle.sent')
group by 1;

-- Ảnh chụp hiện tại: đảo đang Share và điểm (bỏ người bị khóa)
create view report_world_now as
select count(*) as shared_worlds,
       coalesce(round(avg(w.score), 1), 0) as avg_score,
       coalesce(max(w.score), 0) as max_score
from world_snapshots w
join players p on p.id = w.player_id
where not p.is_suspended;

-- Ảnh chụp hiện tại: tổng Coins trong save của mọi người chơi (economy.coins), để theo dõi lạm phát
create view report_economy_now as
select count(*) as saves,
       coalesce(sum((economy ->> 'coins')::bigint) filter (where jsonb_typeof(economy -> 'coins') = 'number'), 0) as total_coins,
       coalesce(round(avg((economy ->> 'coins')::bigint) filter (where jsonb_typeof(economy -> 'coins') = 'number'), 1), 0) as avg_coins,
       coalesce(max((economy ->> 'coins')::bigint) filter (where jsonb_typeof(economy -> 'coins') = 'number'), 0) as max_coins
from saves;

grant select on report_players_daily, report_market_daily, report_market_items_daily, report_trades_daily,
    report_world_daily, report_world_now, report_economy_now to lumi_report;
