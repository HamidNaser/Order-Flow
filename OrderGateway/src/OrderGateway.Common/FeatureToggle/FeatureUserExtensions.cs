using LaunchDarkly.Sdk;

namespace OrderGateway.Common.FeatureToggle
{
    internal static class FeatureUserExtensions
    {
        private const string
            storeId = nameof(storeId),
            userId = nameof(userId);

        /// <summary>
        /// Creates the user.
        /// </summary>
        /// <param name="featureUser">The feature user.</param>
        /// <returns></returns>
        public static User CreateUser(this FeatureUser featureUser)
        {
            var userBuilder = User
               .Builder(featureUser.Key);
            if (featureUser.StoreId != null)
            {
                userBuilder.Custom(storeId, featureUser.StoreId.Value);
            }

            return userBuilder.Build();
        }
    }
}
