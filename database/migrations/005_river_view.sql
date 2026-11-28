-- Sông trên đảo của từng người chơi, đọc thẳng từ saves.world (không thêm bảng).
-- Mỗi ô sông là một placement cardID = 'River'. riverFlowStep/riverIsOrigin chỉ có ở bản lưu
-- từ khi client ghi hai trường này; bản lưu cũ để null (client tính lại khi nạp).
-- Chỉ để xem và thống kê: server không đọc view này nên không thêm vào RequiredVersions.

create view river_tiles as
select s.player_id,
       p.username,
       (pl ->> 'q')::int                  as q,
       (pl ->> 'r')::int                  as r,
       nullif((pl ->> 'riverFlowStep')::int, -1) as flow_step,
       (pl ->> 'riverIsOrigin')::boolean  as is_origin,
       (pl ->> 'displayedScore')::int     as displayed_score,
       s.updated_at
from saves s
join players p on p.id = s.player_id
cross join lateral jsonb_array_elements(coalesce(s.world -> 'placements', '[]'::jsonb)) pl
where pl ->> 'cardID' = 'River';

-- Mỗi người chơi: số ô sông, số đầu nguồn, đoạn dài nhất (flow_step lớn nhất + 1).
create view river_stats as
select player_id,
       username,
       count(*)                                  as river_tiles,
       count(*) filter (where is_origin)         as springs,
       max(flow_step) + 1                        as longest_flow,
       count(*) filter (where flow_step is null) as tiles_without_flow_data
from river_tiles
group by player_id, username;

grant select on river_tiles, river_stats to lumi_report;
