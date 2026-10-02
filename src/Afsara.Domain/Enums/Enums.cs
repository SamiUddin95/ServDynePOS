namespace Afsara.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    Cashier = 3,
    Waiter = 4
}

public enum OrderType
{
    DineIn = 1,
    TakeAway = 2,
    Delivery = 3
}

public enum OrderStatus
{
    Open = 1,
    Completed = 2,
    Cancelled = 3
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    Online = 3
}
