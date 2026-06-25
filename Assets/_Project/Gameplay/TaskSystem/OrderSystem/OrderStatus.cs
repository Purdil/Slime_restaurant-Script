namespace _Project.Gameplay.TaskSystem.OrderSystem
{
    public enum OrderStatus
    {
        Created,
        WaitingCook,
        Cooking,
        Cooked,
        WaitingDelivery,
        Delivering,
        Delivered,
        Completed,
        Canceled
    }
}
