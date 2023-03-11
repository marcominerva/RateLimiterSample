namespace RateLimiterSample.DataAccessLayer.Entities;

public class Account
{
    public Guid Id { get; set; }

    public string UserName { get; set; }

    public string ApiKey { get; set; }

    public Guid? SubscriptionId { get; set; }

    public virtual Subscription Subscription { get; set; }
}
