using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using AwsSdkOptions = Amazon.Extensions.NETCore.Setup.AWSOptions;

namespace Template_Api.Core.Security.Config
{
    public class AwsConfig
    {
        public const string SectionName = "Aws";

        public static void AddAwsServices(IServiceCollection services, IConfiguration config)
        {
            services.Configure<AwsOptions>(config.GetSection(SectionName));
            services.AddDefaultAWSOptions(BuildAwsSdkOptions(config));
            services.AddAWSService<IAmazonS3>();
        }

        #region Private
        private static AwsSdkOptions BuildAwsSdkOptions(IConfiguration config)
        {
            var aws = config.GetSection(SectionName).Get<AwsOptions>() ?? new AwsOptions();
            var sdkOptions = new AwsSdkOptions();

            if (aws.Enabled && string.IsNullOrWhiteSpace(aws.BucketName))
                throw new InvalidOperationException("Aws:BucketName not found.");

            if (aws.Enabled && string.IsNullOrWhiteSpace(aws.Region))
                throw new InvalidOperationException("Aws:Region not found.");

            if (!string.IsNullOrWhiteSpace(aws.Region))
                sdkOptions.Region = RegionEndpoint.GetBySystemName(aws.Region);

            // Static keys are optional on purpose: without them the SDK falls back to its
            // credential chain, which is what lets an IAM role work in a real deployment.
            if (!string.IsNullOrWhiteSpace(aws.AccessKey) && !string.IsNullOrWhiteSpace(aws.SecretKey))
                sdkOptions.Credentials = new BasicAWSCredentials(aws.AccessKey, aws.SecretKey);

            return sdkOptions;
        }
        #endregion

        public class AwsOptions
        {
            public bool Enabled { get; set; }
            public string BucketName { get; set; }
            public string AccessKey { get; set; }
            public string SecretKey { get; set; }
            public string Region { get; set; }
            public int SignedUrlMinutes { get; set; }
        }
    }
}
