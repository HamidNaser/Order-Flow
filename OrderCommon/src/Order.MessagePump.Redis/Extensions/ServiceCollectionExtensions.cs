using Order.MessagePump.Locks;
using Order.MessagePump.Redis.Locks;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;

namespace Order.MessagePump.Redis.Extensions
{
    /// <summary>
    /// DI extension methods for registering Redis-backed lock management.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a <see cref="SimpleRedisLockManager"/> as <see cref="ILockManager"/>
        /// using the provided Redis connection string.
        /// </summary>
        public static IServiceCollection AddRedisLockManager(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Redis connection string must not be null or empty.", nameof(connectionString));

            services.Add(new ServiceDescriptor(typeof(ILockManager), _ => new SimpleRedisLockManager(connectionString), lifetime));

            return services;
        }

        /// <summary>
        /// Registers a <see cref="SimpleRedisLockManager"/> as <see cref="ILockManager"/>
        /// using an existing <see cref="IConnectionMultiplexer"/>.
        /// </summary>
        public static IServiceCollection AddRedisLockManager(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            services.Add(new ServiceDescriptor(typeof(ILockManager), sp =>
            {
                var connection = sp.GetRequiredService<IConnectionMultiplexer>();
                return new SimpleRedisLockManager(connection);
            }, lifetime));

            return services;
        }
    }
}
