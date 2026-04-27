using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace OrderHub.Common.FeatureToggle
{
    public class LaunchDarklyFeatureToggle : IFeatureToggle
    {
        private ILdClient launchDarklyClient;
        private IFeatureToggleUser defaultFeatureToggleUser;

        public LaunchDarklyFeatureToggle(ILdClient launchDarklyClient, IFeatureToggleUser featureToggleUser)
        {
            this.launchDarklyClient = launchDarklyClient;
            defaultFeatureToggleUser = featureToggleUser;
        }

        public bool IsFeatureEnabled(string flagKey, FeatureUser? featureUser = null)
        {
            var user = featureUser != null
                ? featureUser.CreateUser()
                : defaultFeatureToggleUser.GetUser().CreateUser();

            return launchDarklyClient.BoolVariation(flagKey, Context.FromUser(user));
        }
    }
}
