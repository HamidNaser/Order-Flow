namespace OrderGateway.Common.FeatureToggle
{
    public class MachineFeatureToggleUser : IFeatureToggleUser
    {
        public FeatureUser GetUser()
        {
            return new FeatureUser
            {
                Key = Environment.MachineName,
                StoreId = -1
            };
        }
    }
}
