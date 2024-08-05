using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LearningApp;

public class AzureResourceAction
{

    private ArmClient _armClient;

    public AzureResourceAction()
    {
        _armClient = new ArmClient(new DefaultAzureCredential());
    }

    [KernelFunction,
     Description("Add tag to an azure resource based on resourceid and provided key and value")]
    async public Task AddTags(
        [Description("ResourceId of the azure resource")] string resourceId,
        [Description("key for the tag")] string key,
        [Description("value for the tag")] string value
    )
    {
        Console.WriteLine("Identified plugin: AddTags");
        var genericResource = _armClient.GetGenericResource(new ResourceIdentifier(resourceId));
        await genericResource.AddTagAsync(key, value);
    }
}