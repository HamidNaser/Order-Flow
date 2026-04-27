namespace OrderGateway.Common.FeatureToggle
{
    public class FeatureFlags
    {
        public static readonly FeatureFlag OrderGatewayEnabledStoresV2 = new FeatureFlag(
                   "orders.enableordergateway",
                   "Enable ingestion of Order events for MDV2");
    }
}
