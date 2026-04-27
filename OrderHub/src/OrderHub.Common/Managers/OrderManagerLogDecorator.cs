using System.Diagnostics;
using OrderHub.Common.Exceptions;
using OrderHub.Common.Models;
using OrderHub.Common.Services;
using Serilog;
using Serilog.Context;

namespace OrderHub.Common.Managers;

public class OrderManagerLogDecorator(IOrderManager inner) : IOrderManager
{
    public async Task<(long ordersCount, List<ChannelOrder> results)> ReadCustomerOrdersAsync(string storeId, string customerId, int page = 1, int pageSize = 25)
    {
        using var storeIdDisposable = LogContext.PushProperty(nameof(storeId), storeId);
        using var pageSizeDisposable = LogContext.PushProperty(nameof(pageSize), pageSize);
        using var pageDisposable = LogContext.PushProperty(nameof(page), page);

        var timer = Stopwatch.StartNew();

        try
        {
            var result = await inner.ReadCustomerOrdersAsync(storeId, customerId, page, pageSize);

            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Debug(nameof(ReadCustomerOrdersAsync));

            return result;
        }
        catch (OrderException ex)
        {
            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Error(ex, "Error getting orders.");

            throw;
        }
        catch (Exception ex)
        {
            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Error(ex, "Unexpected exception getting orders.");

            throw;
        }
        finally
        {
            timer.Stop();
        }
    }

    public async Task<(ChannelOrder? Order, string? Content)> GetFullOrderByIdAsync(
        string storeId,
        string orderId
    )
    {
        using var storeIdDisposable = LogContext.PushProperty(nameof(storeId), storeId);
        using var orderIdDisposable = LogContext.PushProperty(nameof(orderId), orderId);

        var timer = Stopwatch.StartNew();

        try
        {
            var (order, content) = await inner.GetFullOrderByIdAsync(storeId, orderId);

            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(ChannelOrder), order, destructureObjects: true)
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Debug(nameof(GetFullOrderByIdAsync));

            return (order, content);
        }
        catch (OrderException ex)
        {
            Log
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .ForContext<OrderManagerLogDecorator>()
                .Error(ex, "Error getting a full order.");

            throw;
        }
        catch (Exception ex)
        {
            Log
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .ForContext<OrderManagerLogDecorator>()
                .Error(ex, "Unexpected exception getting a full order.");

            throw;
        }
        finally
        {
            timer.Stop();
        }
    }

    public async Task BulkDeleteOrdersAsync(string storeId, List<string> orderIds)
    {
        using var storeIdDisposable = LogContext.PushProperty(nameof(storeId), storeId);
        using var countDisposable = LogContext.PushProperty("OrderIdCount", orderIds.Count);

        var timer = Stopwatch.StartNew();

        try
        {
            await inner.BulkDeleteOrdersAsync(storeId, orderIds);

            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Debug(nameof(BulkDeleteOrdersAsync));
        }
        catch (OrderException ex)
        {
            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Error(ex, "Error deleting orders.");

            throw;
        }
        catch (Exception ex)
        {
            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Error(ex, "Unexpected exception deleting orders.");

            throw;
        }
        finally
        {
            timer.Stop();
        }
    }

    public async Task<string?> GetOrderContentByEncodedKeyAsync(string encodedKey)
    {
        using var encodedKeyDisposable = LogContext.PushProperty(nameof(encodedKey), encodedKey);

        var timer = Stopwatch.StartNew();

        try
        {
            var result = await inner.GetOrderContentByEncodedKeyAsync(encodedKey);

            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Debug(nameof(GetOrderContentByEncodedKeyAsync));

            return result;
        }
        catch (Exception ex)
        {
            Log
                .ForContext<OrderManagerLogDecorator>()
                .ForContext(nameof(timer.ElapsedMilliseconds), timer.ElapsedMilliseconds)
                .Error(ex, "Unexpected exception getting order content by encoded key.");

            throw;
        }
        finally
        {
            timer.Stop();
        }
    }
}
