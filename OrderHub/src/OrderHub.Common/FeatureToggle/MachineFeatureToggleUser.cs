namespace OrderHub.Common.FeatureToggle
{
    public class MachineFeatureToggleUser : IFeatureToggleUser
    {
        public FeatureUser GetUser()
        {
            return new FeatureUser
            {
                Key = Environment.MachineName,
                CommonOrgId = ""
            };
        }
    }
}
