namespace OrderHub.Common.FeatureToggle
{
    public interface IFeatureToggle
    {   
        bool IsFeatureEnabled(string flagKey, FeatureUser? featureUser = null);
    }
}
