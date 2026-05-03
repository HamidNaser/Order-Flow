using System.Diagnostics;
using OrderHub.Common.Exceptions;
using OrderHub.Common.Telemetry;
using OrderHub.Contracts.Common.Enums;
using OrderHub.Contracts.Ingest;
using Serilog;
using Serilog.Context;
using Priority = OrderHub.Common.Models.Components.Priority;

namespace OrderHub.Common.Managers
{
    public class OrderIngestManagerLogDecorator(IOrderIngestManager inner, IOrderMetrics metrics) : IOrderIngestManager
    {
        public async Task<AddOrderResult> AddOrderAsync(OrderRequest request, Priority priority)
        {
            using var requestDisposable = LogContext.PushProperty(
                nameof(OrderRequest),
                request,
                destructureObjects: true
            );
            using var channelTypeDisposable = LogContext.PushProperty(nameof(ChannelType), request.ChannelType);
            using var priorityDisposable = LogContext.PushProperty(nameof(Priority), priority);

            var timer = Stopwatch.StartNew();

            try
            {
                ReportMetrics(request);

                var response = await inner.AddOrderAsync(request, priority);

                if (response.Status == AddOrderResultStatus.DUPLICATE_REQUEST)
                {
                    Log
                        .ForContext<OrderIngestManagerLogDecorator>()
                        .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                        .Information(
                            "Duplicate order detected for SourceOrderId: {SourceOrderId}, Priority: {Priority}, ChannelType: {ChannelType}, returning existing OrderId: {OrderId}",
                            request.Merchant.OrderId,
                            priority,
                            request.ChannelType,
                            response.OrderId);
                }

                Log
                    .ForContext<OrderIngestManagerLogDecorator>()
                    .ForContext(nameof(OrderResponse), response, destructureObjects: true)
                    .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                    .Debug(nameof(AddOrderAsync));

                return response;
            }
            catch (OrderException ex)
            {
                Log
                    .ForContext<OrderIngestManagerLogDecorator>()
                    .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                    .Error(ex, "Error adding order.");

                throw;
            }
            catch (Exception ex)
            {
                Log
                    .ForContext<OrderIngestManagerLogDecorator>()
                    .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                    .Error(ex, "Unexpected exception adding order.");

                throw;
            }
            finally
            {
                timer.Stop();
            }
        }

        private void ReportMetrics(OrderRequest request)
        {
            var contentLength = request.Content?.Length ?? 0;

            metrics.AddCustomAttribute("Custom/ContentSize", contentLength);
            metrics.AddCustomAttribute("Custom/StoreId", request.StoreId);
            metrics.AddCustomAttribute("Custom/AgentId", request.AgentId ?? string.Empty);
            metrics.AddCustomAttribute("Custom/Merchant", request.Merchant);

            switch (request)
            {
                case AddDigitalOrderRequest addTextRequest:
                    ReportTextMetrics(addTextRequest, contentLength);
                    break;

                case AddShipmentOrderRequest addOrderRequest:
                    ReportOrderMetrics(addOrderRequest, contentLength);
                    break;
            }
        }

        private void ReportTextMetrics(AddDigitalOrderRequest request, int contentLength)
        {

            var sizeBucket = contentLength switch
            {
                < 100 => "0-100",
                < 500 => "100-500",
                < 1000 => "500-1000",
                < 2000 => "1000-2000",
                _ => "2000+"
            };

            metrics.IncrementCounter($"Custom/Type/text");
            metrics.IncrementCounter($"Custom/ContentSize/{sizeBucket}");
        }

        private void ReportOrderMetrics(AddShipmentOrderRequest request, int contentLength)
        {

            var sizeBucket = contentLength switch
            {
                < 2500 => "0-2500",
                < 10000 => "2501-10000",
                < 25000 => "10001-25000",
                < 50000 => "25001-50000",
                < 75000 => "50001-75000",
                < 100000 => "75001-100000",
                < 150000 => "100001-150000",
                < 300000 => "150001-300000",
                < 500000 => "300001-500000",
                _ => "500001+"
            };

            metrics.IncrementCounter($"Custom/Type/shipment");
            metrics.IncrementCounter($"Custom/ContentSize/{sizeBucket}");
        }

    }
}
