using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace Order.MessageOperations.Mcp.Prompts;

/// <summary>
/// MCP prompts that provide orchestrated scenario workflows.
/// When the AI invokes a prompt, it receives structured instructions
/// that guide it through a multi-step workflow using the available tools.
/// Prompt templates are loaded from .md files in Prompts/Templates/ so they
/// can be edited without recompiling.
/// </summary>
[McpServerPromptType]
public class OrderPrompts
{
    private static readonly string TemplatesFolder = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Prompts", "Templates");

    private static string LoadTemplate(string fileName)
    {
        var path = Path.Combine(TemplatesFolder, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt template not found: {path}");
        return File.ReadAllText(path);
    }

    private static string LoadTemplate(string fileName, Dictionary<string, string> replacements)
    {
        var template = LoadTemplate(fileName);
        foreach (var (key, value) in replacements)
        {
            template = template.Replace($"{{{{{key}}}}}", value);
        }
        return template;
    }

    [McpServerPrompt(Name = "setup-localstack")]
    [Description("Set up the full local infrastructure (LocalStack, MongoDB, Redis, Keycloak) for order processing")]
    public static string SetupLocalStack()
    {
        return LoadTemplate("setup-localstack.md");
    }

    [McpServerPrompt(Name = "run-standard-orders")]
    [Description("Generate and send standard-priority test orders through the pipeline, then trace each one")]
    public static string RunStandardOrders(
        [Description("Number of orders to send (default: 5)")] int count = 5,
        [Description("Store ID (default: 10001 — enabled in local feature flags)")] string storeId = "10001")
    {
        var storeNote = $"Use storeId='{storeId}' for all orders. (Must be enabled in the OrderGateway feature flag 'orders.enableordergateway' or orders will be silently dropped.)";

        return LoadTemplate("run-standard-orders.md", new Dictionary<string, string>
        {
            ["count"] = count.ToString(),
            ["storeNote"] = storeNote
        });
    }

    [McpServerPrompt(Name = "run-express-orders")]
    [Description("Generate and send express-priority test orders through the pipeline, then trace each one")]
    public static string RunExpressOrders(
        [Description("Number of orders to send (default: 5)")] int count = 5,
        [Description("Store ID (default: 10001 — enabled in local feature flags)")] string storeId = "10001")
    {
        var storeNote = $"Use storeId='{storeId}' for all orders. (Must be enabled in the OrderGateway feature flag 'orders.enableordergateway' or orders will be silently dropped.)";

        return LoadTemplate("run-express-orders.md", new Dictionary<string, string>
        {
            ["count"] = count.ToString(),
            ["storeNote"] = storeNote
        });
    }

    [McpServerPrompt(Name = "end-to-end-trace")]
    [Description("Send a single order and trace it through the entire pipeline: queue → S3 → MongoDB")]
    public static string EndToEndTrace(
        [Description("Priority: 'standard' or 'express' (default: standard)")] string priority = "standard",
        [Description("Store ID (default: 10001 — enabled in local feature flags)")] string storeId = "10001")
    {
        var storeNote = $"Use storeId='{storeId}'. (Must be enabled in the OrderGateway feature flag 'orders.enableordergateway' or orders will be silently dropped.)";
        var downstreamQueue = priority == "express" ? "order-hub-express-order" : "order-hub-standard-order";

        return LoadTemplate("end-to-end-trace.md", new Dictionary<string, string>
        {
            ["priority"] = priority.ToUpper(),
            ["priorityLower"] = priority.ToLower(),
            ["downstreamQueue"] = downstreamQueue,
            ["storeNote"] = storeNote
        });
    }

    [McpServerPrompt(Name = "build-and-run")]
    [Description("Build both solutions and launch the Aspire AppHosts to run all services locally, then confirm end-to-end")]
    public static string BuildAndRun()
    {
        return LoadTemplate("build-and-run.md");
    }

    [McpServerPrompt(Name = "tear-down")]
    [Description("Stop all running services, clean up infrastructure containers and data for both OrderGateway and OrderHub")]
    public static string TearDown()
    {
        return LoadTemplate("tear-down.md");
    }
}
