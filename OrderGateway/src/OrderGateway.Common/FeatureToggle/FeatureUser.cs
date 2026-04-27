namespace OrderGateway.Common.FeatureToggle
{
    public class FeatureUser
    {
        /// <summary>
        /// Gets or sets the store identifier.
        /// </summary>
        /// <value>
        /// The store identifier.
        /// </value>
        public int? StoreId { get; set; }
        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        public required string Key { get; set; }

    }
}

