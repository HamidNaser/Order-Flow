using Serilog;

namespace OrderHub.Common.FeatureToggle;

public class ConfigFeatureToggle(Dictionary<string, List<FeatureUser>> flags) : IFeatureToggle
{
    public bool IsFeatureEnabled(string flagKey, FeatureUser? featureUser = null)
    {
        if (!flags.TryGetValue(flagKey, out var enabledFeatureUsers))
        {
            Log.Debug("Feature flag '{FlagKey}' not configured, defaulting to false", flagKey);
            return false;
        }

        if (enabledFeatureUsers.Count == 0)
        {
            Log.Debug("Feature flag '{FlagKey}' has no enabled users, defaulting to false", flagKey);
            return false;
        }

        // If no specific user provided, check against a default user (will check fo global enablement
        var userToCheck = featureUser ?? new FeatureUser { Key = "", CommonOrgId = "" };

        var isEnabled = enabledFeatureUsers.Any(u =>
            // Key and CommonOrgId both empty means feature is enabled for all users
            (string.IsNullOrWhiteSpace(u.Key) && string.IsNullOrWhiteSpace(u.CommonOrgId))
            // Otherwise, match specific user
            || (string.Equals(u.Key, userToCheck.Key, StringComparison.OrdinalIgnoreCase)
                && string.Equals(u.CommonOrgId, userToCheck.CommonOrgId, StringComparison.OrdinalIgnoreCase)));

        Log.Debug("Feature flag '{FlagKey}' evaluated to {IsEnabled} for user Key='{Key}', CommonOrgId='{CommonOrgId}'",
            flagKey, isEnabled, userToCheck.Key, userToCheck.CommonOrgId);

        return isEnabled;
    }
}
