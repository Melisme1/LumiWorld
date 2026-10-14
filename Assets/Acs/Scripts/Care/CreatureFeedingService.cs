using System;

namespace LumiWorld.Acs
{
    public sealed class CreatureFeedPreview
    {
        public string IndividualId { get; internal set; }
        public string ResourceId { get; internal set; }
        public int Required { get; internal set; }
        public long Available { get; internal set; }
        public double EstimatedEatingSeconds { get; internal set; }
        public bool IsFavorite { get; internal set; }
        public bool CanFeed { get; internal set; }
        public string Reason { get; internal set; }
    }

    public static class CreatureFeedingService
    {
        public static CreatureFeedPreview Preview(CreatureCareView state, CreatureFoodChoice food,
            long available, double unitsPerSecond)
        {
            var result = new CreatureFeedPreview { IndividualId = state?.IndividualId, ResourceId = food?.ResourceId,
                Available = available, IsFavorite = food != null && food.IsFavorite };
            result.Reason = state == null ? "Không tìm thấy cá thể." :
                state.Satiety >= 100 ? "Đã no đầy." :
                state.Status == CreatureHungerStatus.Eating ? state.WaitingForFood ?
                    "Đang chờ thêm thức ăn phù hợp trong ngăn riêng." : "Đang ăn; không cần bắt đầu lại." :
                food == null || available <= 0 ? "Ngăn thức ăn của cá thể chưa có món phù hợp." : "Có thể ăn từ ngăn riêng.";
            if (state != null && food != null)
            {
                result.Required = (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0, 100 - state.Satiety) / food.SatietyPerUnit - 1e-9));
                result.EstimatedEatingSeconds = result.Required / unitsPerSecond;
            }
            result.CanFeed = state != null && state.Status != CreatureHungerStatus.Eating &&
                state.Satiety < 100 && food != null && available > 0;
            return result;
        }
    }
}
