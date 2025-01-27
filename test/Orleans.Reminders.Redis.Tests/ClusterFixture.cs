﻿using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

using Orleans.Hosting;
using Orleans.TestingHost;

using StackExchange.Redis;

namespace Orleans.Reminders.Redis.Tests
{
    public class ClusterFixture : IDisposable
    {
        private readonly ConnectionMultiplexer _redis;

        public ClusterFixture()
        {
            TestClusterBuilder builder = new TestClusterBuilder(1);
            builder.Options.ServiceId = "Service";
            builder.Options.ClusterId = "TestCluster";
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();

            string redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "127.0.0.1";
            string redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
            string redisConnectionString = $"{redisHost}:{redisPort}, allowAdmin=true";

            builder.ConfigureHostConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>()
                {
                    { "RedisConnectionString", redisConnectionString }
                });
            });

            Cluster = builder.Build();

            Cluster.Deploy();
            Cluster.InitializeClient();
            Client = Cluster.Client;

            ConfigurationOptions redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            _redis = ConnectionMultiplexer.ConnectAsync(redisOptions).Result;
            Database = _redis.GetDatabase();
        }

        public TestCluster Cluster { get; }
        public IGrainFactory GrainFactory => Cluster.GrainFactory;
        public IClusterClient Client { get; }
        public IDatabase Database { get; }

        public class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder builder)
            {
                //get the redis connection string from the testcluster's config
                string redisEP = builder.GetConfigurationValue("RedisConnectionString");

                builder.UseRedisReminderService(options =>
                {
                    options.ConnectionString = redisEP;
                });
            }
        }

        public void Dispose()
        {
            Database.ExecuteAsync("FLUSHALL").Wait();
            Client.Dispose();
            Cluster.StopAllSilos();
            _redis?.Dispose();
        }
    }
}
