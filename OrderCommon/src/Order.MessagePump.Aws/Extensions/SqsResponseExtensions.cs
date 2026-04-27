using Amazon.SQS.Model;

namespace Order.MessagePump.Aws.Extensions;

public static class SqsResponseExtensions
{
    public static void EnsureSuccess(this GetQueueUrlResponse response)
    {
        if (response == null || response.HttpStatusCode < System.Net.HttpStatusCode.OK || response.HttpStatusCode >= System.Net.HttpStatusCode.MultipleChoices)
            throw new Exception("SQS GetQueueUrl failed.");
    }

    public static void EnsureSuccess(this CreateQueueResponse response)
    {
        if (response == null || response.HttpStatusCode < System.Net.HttpStatusCode.OK || response.HttpStatusCode >= System.Net.HttpStatusCode.MultipleChoices)
            throw new Exception("SQS CreateQueue failed.");
    }

    public static void EnsureSuccess(this DeleteMessageResponse response)
    {
        if (response == null || response.HttpStatusCode < System.Net.HttpStatusCode.OK || response.HttpStatusCode >= System.Net.HttpStatusCode.MultipleChoices)
            throw new Exception("SQS DeleteMessage failed.");
    }

    public static void EnsureSuccess(this ReceiveMessageResponse response)
    {
        if (response == null || response.HttpStatusCode < System.Net.HttpStatusCode.OK || response.HttpStatusCode >= System.Net.HttpStatusCode.MultipleChoices)
            throw new Exception("SQS ReceiveMessage failed.");
    }

    public static void EnsureSuccess(this ChangeMessageVisibilityResponse response)
    {
        if (response == null || response.HttpStatusCode < System.Net.HttpStatusCode.OK || response.HttpStatusCode >= System.Net.HttpStatusCode.MultipleChoices)
            throw new Exception("SQS ChangeMessageVisibility failed.");
    }

    public static void EnsureSuccess(this SendMessageResponse response)
    {
        if (response == null || response.HttpStatusCode < System.Net.HttpStatusCode.OK || response.HttpStatusCode >= System.Net.HttpStatusCode.MultipleChoices)
            throw new Exception("SQS SendMessage failed.");
    }

    public static void EnsureSuccess(this SendMessageBatchResponse response)
    {
        if (response == null || response.HttpStatusCode < System.Net.HttpStatusCode.OK || response.HttpStatusCode >= System.Net.HttpStatusCode.MultipleChoices)
            throw new Exception("SQS SendMessageBatch failed.");
    }
}
