using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Services;
using OrderHub.Contracts.Ingest;
using OrderHub.Contracts.Utility;
using MongoDB.Bson;
using Serilog;
using Priority = OrderHub.Common.Models.Components.Priority;
using MerchantName = OrderHub.Common.Models.Components.MerchantName;

namespace OrderHub.Common.Managers
{
    public class OrderIngestManager(IS3Service s3Service, S3Config s3Config) : IOrderIngestManager
    {
        public async Task<AddOrderResult> AddOrderAsync(OrderRequest request, Priority priority)
        {
            var existingOrder = await GetExistingOrder(request, priority);

            if (existingOrder != null)
            {
                return AddOrderResult.DuplicateRequest(existingOrder.OrderId);
            }

            // Generate orderId before pre-signed URL generation
            var orderId = ObjectId.GenerateNewId().ToString();

            // Construct S3 key for this order
            var s3OrderKey = new S3OrderKey
            {
                Priority = priority,
                MerchantName = (MerchantName)request.Merchant.Name,
                ChannelType = request.ChannelType,
                SourceOrderId = request.Merchant.OrderId,
                OrderId = orderId
            };

            // Persist order to S3 after URL generation
            await PersistOrderRequest(request, s3OrderKey);

            return AddOrderResult.NewOrder(orderId);
        }

        private async Task<S3OrderKey?> GetExistingOrder(
            OrderRequest request,
            Priority priority)
        {
            var prefix = S3OrderKey.GenerateDuplicateProtectionPrefix(
                priority,
                (MerchantName)request.Merchant.Name,
                request.ChannelType,
                request.Merchant.OrderId);

            var existingKeys = await s3Service.GetObjectKeysByPrefix(s3Config.OrderBucketName, prefix);

            if (existingKeys.Count == 0) return null;

            var keyToUse = existingKeys[0];

            if (existingKeys.Count > 1)
            {
                Log.Warning("Multiple S3 Objects found matching prefix: {Prefix}. Proceeding with first match: {FirstMatch}", prefix, keyToUse);
            }

            if (S3OrderKey.TryParse(keyToUse, out var orderKey))
            {
                return orderKey;
            }

            Log.Error("Could not parse Order S3 Key: {Key}", keyToUse);
            return null;
        }

        private async Task PersistOrderRequest(
            OrderRequest request,
            S3OrderKey s3OrderKey
        )
        {
            var s3SaveRequest = new S3PutObjectRequest<OrderRequest>
            {
                Key = s3OrderKey.ToKeyString(),
                BucketName = s3Config.OrderBucketName,
                Payload = request
            };

            await s3Service.PutObjectAsync(s3SaveRequest);
        }
    }
}
