namespace MovieBooking.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class SkipPermissionAttribute : Attribute
{
}
