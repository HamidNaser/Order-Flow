namespace OrderGateway.Common.FeatureToggle
{
    public interface IFeatureToggle
    {
        /// <summary>
        /// Determines whether [the feature is enabled] [for the specified feature user].
        /// </summary>
        /// <param name="featureFlag">The feature flag.</param>
        /// <param name="featureUser">If the "featureUser " is null, it will create a machine user</param>
        /// <returns>
        /// <c>true</c> if [is feature enabled] [the specified feature flag]; otherwise, <c>false</c>.
        /// </returns>
        bool IsFeatureEnabled(FeatureFlag featureFlag, FeatureUser? featureUser = null);
    }
}
