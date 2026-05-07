using Azure.AI.OpenAI;
using Azure.Identity;
using CRUDTasksWithAgent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;

namespace CRUDTasksWithAgent.Services
{
    // This provider uses lazy initialization - the agent is only created when first accessed.
    // This allows:
    // 1. TaskList.razor to work independently without agent configuration
    // 2. Agent creation errors only affect the agent page, not the whole app
    // 3. Follows DI best practices while maintaining graceful degradation

    public interface IAgentFrameworkProvider
    {
        IChatClient? ChatClient { get; }
        List<AIFunction>? Tools { get; }
    }

    public class AgentFrameworkProvider : IAgentFrameworkProvider
    {
        private readonly Lazy<(IChatClient?, List<AIFunction>?)> _lazyAgentData;

        public IChatClient? ChatClient => _lazyAgentData.Value.Item1;
        public List<AIFunction>? Tools => _lazyAgentData.Value.Item2;

        public AgentFrameworkProvider(IConfiguration config, IServiceProvider sp)
        {
            // Use Lazy<T> to defer agent creation until first access
            _lazyAgentData = new Lazy<(IChatClient?, List<AIFunction>?)>(() =>
            {
                // Get Azure OpenAI configuration
                var deployment = config["ModelDeployment"];
                var endpoint = config["AzureOpenAIEndpoint"];
                if (string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(endpoint))
                {
                    return (null, null);
                }

                try
                {
                    // Get TaskCrudTool instance from service provider
                    var taskCrudTool = sp.GetRequiredService<TaskCrudTool>();

                    // Create IChatClient
                    IChatClient chatClient = new AzureOpenAIClient(
                            new Uri(endpoint),
                            new DefaultAzureCredential())
                        .GetChatClient(deployment)
                        .AsIChatClient();

                    // Create list of tools
                    var tools = new List<AIFunction>
                    {
                        AIFunctionFactory.Create(taskCrudTool.CreateTaskAsync),
                        AIFunctionFactory.Create(taskCrudTool.ReadTasksAsync),
                        AIFunctionFactory.Create(taskCrudTool.UpdateTaskAsync),
                        AIFunctionFactory.Create(taskCrudTool.DeleteTaskAsync)
                    };

                    return (chatClient, tools);
                }
                catch
                {
                    // If agent creation fails, return null
                    // This prevents the entire app from crashing
                    return (null, null);
                }
            });
        }
    }
}