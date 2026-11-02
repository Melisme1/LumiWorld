using System.Collections.Generic;

/// <summary>
/// Bài trên tay cho chợ và đổi thẻ. Ở đảo mình đọc và sửa qua CardDeskController (cập nhật cả UI);
/// lúc thăm đảo khác tay bài không nạp, nên đọc và sửa thẳng phần hand trong bản lưu kinh tế.
/// Các hàm *WithoutSaving không ghi file: bên gọi ghi một lần cùng thao tác chờ hoặc mục hộp thư.
/// </summary>
public static class HandCards
{
    private static CardDeskController Desk
    {
        get
        {
            CardDeskController desk = CardDeskController.Instance;
            return desk != null && desk.HandReady ? desk : null;
        }
    }

    /// <summary>Có thể cộng hoặc trừ thẻ lúc này không (ở đảo mình mà tay bài chưa nạp xong thì chưa).</summary>
    public static bool Available =>
        Desk != null || (WorldLaunch.IsVisit && EconomySaveSystem.Instance != null && EconomySaveSystem.Instance.SavedHandReady);

    public static int Count(string cardId)
    {
        CardDeskController desk = Desk;
        if (desk != null) return desk.GetHeldCount(cardId);
        return Available ? EconomySaveSystem.Instance.GetSavedHandCount(cardId) : 0;
    }

    /// <summary>Thẻ thú (đổi hoặc bán được) đang có trên tay, kèm số lá.</summary>
    public static List<KeyValuePair<CardData, int>> Creatures()
    {
        var result = new List<KeyValuePair<CardData, int>>();
        CardDeskController desk = Desk;
        if (desk != null)
        {
            foreach (KeyValuePair<CardData, int> pair in desk.GetHeldCards())
                if (CardTradeIds.IsTradable(pair.Key)) result.Add(pair);
            return result;
        }
        if (!Available) return result;

        CardCatalog catalog = WorldSaveSystem.Instance != null ? WorldSaveSystem.Instance.Cards : null;
        foreach (HandCardStack stack in EconomySaveSystem.Instance.SavedHand)
        {
            CardData card = catalog != null ? catalog.Find(stack.cardId) : null;
            if (CardTradeIds.IsTradable(card) && stack.count > 0) result.Add(new KeyValuePair<CardData, int>(card, stack.count));
        }
        return result;
    }

    public static bool TakeWithoutSaving(string cardId, int count)
    {
        CardDeskController desk = Desk;
        if (desk != null) return desk.TakeCardsWithoutSaving(cardId, count);
        return Available && EconomySaveSystem.Instance.TakeSavedHandCards(cardId, count);
    }

    public static void AddWithoutSaving(CardData card, int count)
    {
        CardDeskController desk = Desk;
        if (desk != null) desk.AddCardsWithoutSaving(card, count);
        else if (Available) EconomySaveSystem.Instance.AddSavedHandCards(card.cardID, count);
    }
}
