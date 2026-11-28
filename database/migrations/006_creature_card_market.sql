-- Bán thẻ thú trên chợ: mỗi lô là "card.<cardID>" trong listings.resource_id, dùng chung luồng chợ.
-- Chỉ thẻ trên tay (cardType Creature trong Assets/Data/Cards), thú đã thả xuống đảo không bán được.
-- Giá gốc 50 Coins mỗi thẻ; admin chỉnh từng thẻ được. Đã có khóa thì giữ nguyên giá đang dùng.
insert into game_config (key, value) values
    ('market.baseValue.card.creature_blossomfrog', '50'),
    ('market.baseValue.card.creature_capybara', '50'),
    ('market.baseValue.card.creature_caterpillar', '50'),
    ('market.baseValue.card.cow_1', '50'),
    ('market.baseValue.card.creature_deer', '50'),
    ('market.baseValue.card.creature_dragonfly', '50'),
    ('market.baseValue.card.creature_flowerdog', '50'),
    ('market.baseValue.card.creature_flyingsquirrel', '50'),
    ('market.baseValue.card.creature_fox', '50'),
    ('market.baseValue.card.creature_grassfox', '50'),
    ('market.baseValue.card.creature_grasshopper', '50'),
    ('market.baseValue.card.creature_highlandcow', '50'),
    ('market.baseValue.card.creature_otter', '50'),
    ('market.baseValue.card.creature_parrot', '50'),
    ('market.baseValue.card.creature_peacock', '50'),
    -- Mỗi lô thẻ tối đa 10 lá; mỗi người tối đa 3 lô thẻ đang bán (vẫn tính trong giới hạn lô chung)
    ('market.maxCardsPerListing', '10'),
    ('market.maxActiveCardListings', '3')
on conflict (key) do nothing;
