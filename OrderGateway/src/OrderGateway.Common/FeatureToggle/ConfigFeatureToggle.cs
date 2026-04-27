using Serilog;

namespace OrderGateway.Common.FeatureToggle;

public class ConfigFeatureToggle(Dictionary<string, List<FeatureUser>> flags) : IFeatureToggle
{
    public bool IsFeatureEnabled(FeatureFlag featureFlag, FeatureUser? featureUser = null)
    {
        if (!flags.TryGetValue(featureFlag.Key, out var enabledFeatureUsers))
        {
            Log.Debug("Feature flag '{FlagKey}' not configured, defaulting to false", featureFlag.Key);
            return false;
        }

        if (enabledFeatureUsers.Count == 0)
        {
            Log.Debug("Feature flag '{FlagKey}' has no enabled users, defaulting to false", featureFlag.Key);
            return false;
        }

        // If no specific user provided, check against a default user (will check for global enablement)
        var userToCheck = featureUser ?? new FeatureUser { Key = "", StoreId = null };

        var isEnabled = enabledFeatureUsers.Any(u =>
            // Key and StoreId both empty/null means feature is enabled for all users
            (string.IsNullOrWhiteSpace(u.Key) && u.StoreId == null)
            // Otherwise, match specific user (both Key and StoreId must match)
            || (string.Equals(u.Key, userToCheck.Key, StringComparison.OrdinalIgnoreCase)
                && u.StoreId == userToCheck.StoreId));

        Log.Debug("Feature flag '{FlagKey}' evaluated to {IsEnabled} for user Key='{Key}', StoreId='{StoreId}'",
            featureFlag.Key, isEnabled, userToCheck.Key, userToCheck.StoreId);

        return isEnabled;
    }
}
