using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace OrderGateway.Common.FeatureToggle
{
    public class LaunchDarklyFeatureToggle : IFeatureToggle
    {
        /// <summary>
        /// The launch darkly client
        /// </summary>
        private ILdClient _LaunchDarklyClient;

        /// <summary>
        /// The feature toggle user
        /// </summary>
        private IFeatureToggleUser _featureToggleUser;

        /// <summary>
        /// Initializes a new instance of the <see cref="LaunchDarklyFeatureToggle"/> class.
        /// </summary>
        /// <param name="launchDarklyClient">LD Client</param>
        /// <param name="featureToggleUser">The feature toggle user.</param>
        public LaunchDarklyFeatureToggle(ILdClient launchDarklyClient, IFeatureToggleUser featureToggleUser)
        {
            _LaunchDarklyClient = launchDarklyClient;
            _featureToggleUser = featureToggleUser;
        }

        /// <summary>
        /// Determines whether [is feature enabled] [the specified feature flag].
        /// </summary>
        /// <param name="featureFlag">The feature flag.</param>
        /// <param name="featureUser">If the "featureUser " is null, it will create a machine user</param>
        /// <returns>
        ///   <c>true</c> if [is feature enabled] [the specified feature flag]; otherwise, <c>false</c>.
        /// </returns>
        public bool IsFeatureEnabled(FeatureFlag featureFlag, FeatureUser? featureUser = null)
        {
            var user = featureUser != null
                ? featureUser.CreateUser()
                : _featureToggleUser.GetUser().CreateUser();

            bool ruleResult = _LaunchDarklyClient.BoolVariation(featureFlag.Key, Context.FromUser(user));

            return ruleResult;
        }
    }
}
