namespace OrderGateway.Common.FeatureToggle
{
    public struct FeatureFlag : IEquatable<FeatureFlag>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureFlag"/> struct.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="name">The name.</param>
        internal FeatureFlag(string key, string name)
        {
            Key = key;
            Name = name;
        }

        /// <summary>
        /// Gets the identifying key of the feature flag defined in Launch Darkly.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the friendly name of the feature flag defined in Launch Darkly.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(FeatureFlag other) => Key == other.Key && Name == other.Name;

        /// <summary>
        /// Determines whether the specified <see cref="Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object? obj) => obj is FeatureFlag other && Equals(other);

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Key != null ? Key.GetHashCode() : 0) * 397) ^ (Name != null ? Name.GetHashCode() : 0);
            }
        }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="String" /> that represents this instance.
        /// </returns>
        public override string ToString() => Key;
    }
}
