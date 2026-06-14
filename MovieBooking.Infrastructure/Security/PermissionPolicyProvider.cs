using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MovieBooking.Infrastructure.Security;

public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // First check if a policy is defined via options
        var policy = await base.GetPolicyAsync(policyName);

        if (policy == null)
        {
            // If the policy starts with "Permissions", we dynamically create a policy
            if (policyName.StartsWith("Permissions", StringComparison.OrdinalIgnoreCase))
            {
                var policyBuilder = new AuthorizationPolicyBuilder();
                policyBuilder.AddRequirements(new PermissionRequirement(policyName));
                return policyBuilder.Build();
            }
        }

        return policy;
    }
}
