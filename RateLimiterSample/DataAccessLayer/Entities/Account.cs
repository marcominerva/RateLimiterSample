namespace RateLimiterSample.DataAccessLayer.Entities;

public class Account
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = null!;

    public string ApiKey { get; set; } = null!;

    public Guid? SubscriptionId { get; set; }

    public virtual Subscription? Subscription { get; set; }
}
