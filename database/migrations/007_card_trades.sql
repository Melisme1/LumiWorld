-- Đổi thẻ thú khi thăm đảo: người thăm (from) đề nghị đưa offer_count lá offer_card_id,
-- đổi lấy request_count lá request_card_id của chủ đảo (to). Một loại đổi một loại, không kèm Coins, không phí.
-- Server giữ thẻ người gửi từ lúc gửi; đồng ý thì mỗi bên nhận thẻ qua hộp thư (TradeReceived),
-- từ chối, hủy hoặc hết hạn thì thẻ về người gửi (TradeReturned). Card id dạng "card.<cardID>" như chợ.

create table card_trades (
    id              uuid primary key,
    from_player     uuid not null references players (id) on delete cascade,
    to_player       uuid not null references players (id) on delete cascade,
    offer_card_id   text not null,
    offer_count     int not null check (offer_count > 0),
    request_card_id text not null,
    request_count   int not null check (request_count > 0),
    status          text not null default 'Pending'
                    check (status in ('Pending', 'Accepted', 'Declined', 'Cancelled', 'Expired')),
    created_at      timestamptz not null default now(),
    expires_at      timestamptz not null,
    resolved_at     timestamptz,
    check (from_player <> to_player)
);

create index card_trades_incoming on card_trades (to_player, status, expires_at);
create index card_trades_outgoing on card_trades (from_player, status, expires_at);

-- Ba loại mục hộp thư mới: báo có đề nghị, nhận thẻ khi đổi xong, thẻ trả về người gửi
alter table inbox_items drop constraint inbox_items_kind_check;
alter table inbox_items add constraint inbox_items_kind_check check (kind in (
    'System', 'CoinsFromSale', 'ItemsBought', 'ItemsReturned', 'Obstacle',
    'TradeOffered', 'TradeReceived', 'TradeReturned'));

-- Hạn 72 giờ, tối đa 5 đề nghị đang chờ mỗi người gửi, mỗi bên tối đa 10 lá
insert into game_config (key, value) values
    ('trade.offerHours', '72'),
    ('trade.maxPendingOutgoing', '5'),
    ('trade.maxCardsPerSide', '10')
on conflict (key) do nothing;
