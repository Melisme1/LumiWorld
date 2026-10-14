namespace LumiWorld.Economy.Crafting
{
    /// <summary>
    /// Phân loại nhóm công thức chế biến trong LumiWorld.
    /// </summary>
    public enum CraftingCategory
    {
        Food,       // Thức ăn & chế biến dinh dưỡng cho thú (Ăn trực tiếp = Có)
        Refined,    // Bán thành phẩm tinh chế (Bột mì, Tinh chất mật hoa, ...)
        Herbal,     // Dược liệu & Thảo mộc rừng (Bột hoa thảo mộc, Siro thảo mộc rừng, ...)
        Paper       // Thủ công & Chế tác giấy (Giấy vỏ cây, Giấy hoa ép, Giấy thơm)
    }
}
