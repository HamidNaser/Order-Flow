using LaunchDarkly.Sdk;

namespace OrderHub.Common.FeatureToggle
{
    internal static class FeatureUserExtensions
    {
        private const string storeId = nameof(storeId);

        public static User CreateUser(this FeatureUser featureUser)
        {
            var userBuilder = User
               .Builder(featureUser.Key);

            if (featureUser.CommonOrgId != null)
            {
                userBuilder.Custom(storeId, featureUser.CommonOrgId);
            }

            return userBuilder.Build();
        }
    }
}
