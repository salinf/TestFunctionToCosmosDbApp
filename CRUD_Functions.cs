using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TestFunctionToCosmosDbApp.DomainObjects;


namespace TestFunctionToCosmosDbApp;

public static class CRUD_Functions
{
    private const string _DatabaseId = "testDb2"; //Cosmos Db Id (string name) of database in Azure Cosmos db must be set correctly to do anything beyond test action 
    private const string _ContainerId = "itemsTest"; //Cosmos Container Id (string name) of Container in Azure Cosmos db must be set correctly to do anything beyond test action    

    //Container is like a table name, it is part of a database, it is a container for objects/documents, like a db table that contains rows.

    [FunctionName("Create")] //This is no longer needed, it is superseded by upsert which does everything this does and more depending on your inputs
    public static IActionResult Create([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest request, ILogger log,
        [CosmosDB(databaseName: _DatabaseId, containerName: _ContainerId, Connection = "connection", CreateIfNotExists = false)] out Document document)
    {
        log.LogInformation("Function CreateDocument has started running");

        document = new Document();
        int size = (int)request.Body.Length;
        byte[] buffer = new byte[size];
        request.Body.Read(buffer, 0, size);

        log.LogInformation($"buffer size in bytes: {buffer.Length}");

        if (!document.TryPopulateFields(buffer))
        {
            return new StatusCodeResult(500);
        }

        document.id = Guid.NewGuid().ToString();
        document.CreateDate = DateTime.Now;
        return new OkObjectResult(document);
    }

    [FunctionName("Delete")]
    public static IActionResult Delete([HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequest request, ILogger log)
    {
        log.LogInformation("Function DeleteDocument has started running");

        string id = request.Query["id"];
        if (id == null || id == string.Empty) { return new StatusCodeResult(500); }

        using CosmosClient client = new(connectionString: Environment.GetEnvironmentVariable("connection")!);
        Container container = client.GetContainer("testDb2", "itemsTest");
        PartitionKey partitionKey = new(id);

        string message = String.Empty;
        var result = container.DeleteItemAsync<Document>(id, partitionKey, null).GetAwaiter().GetResult();
        if (result.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            message = "Delete successful";
        }

        return new OkObjectResult(message);
    }

    [FunctionName("QueryById")]
    public static IActionResult QueryById([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest request, ILogger log)
    {
        log.LogInformation("Function QueryById has started running");
        string id = request.Query["id"];
        if (id == null || id == string.Empty) { return new StatusCodeResult(500); }
        log.LogInformation($"QueryById got id {id}");

        using CosmosClient client = new(connectionString: Environment.GetEnvironmentVariable("connection")!);
        log.LogInformation("got a fucking connection");
        Container container = client.GetContainer(_DatabaseId, _ContainerId);
        PartitionKey partitionKey = new(id);

        string message = String.Empty;
        var result = container.ReadItemAsync<Document>(id, partitionKey, null).GetAwaiter().GetResult();
        if (result.StatusCode == System.Net.HttpStatusCode.OK)
        {
            return new OkObjectResult(result.Resource);
        }

        return new NotFoundResult();
    }

    [FunctionName("QueryByMessage")]
    public static IActionResult QueryByMessage([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest request, ILogger log)
    {
        log.LogInformation("Function QueryDocMessages has started running");
        string message = request.Query["message"];
        if (message == null || message == string.Empty) { return new StatusCodeResult(500); }

        using CosmosClient client = new(connectionString: Environment.GetEnvironmentVariable("connection")!);
        Container container = client.GetContainer(_DatabaseId, _ContainerId);
        PartitionKey partitionKey = new("id");

        var queryable = container.GetItemLinqQueryable<Document>(true);
        var result = queryable.Where(p => p.message == message || p.message.Contains(message)).ToList();

        if (result != null)
        {
            return new OkObjectResult(result);
        }

        return new NotFoundResult();
    }

    [FunctionName("JustReturnTest")]
    public static async Task<IActionResult> JrTest([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest request, ILogger log)
    {
        log.LogInformation("JustReturnTest HTTP trigger function processed a request. ");

        return new OkObjectResult("Just return");
    }

    [FunctionName("Test")]
    public static async Task<IActionResult> Test([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest request, ILogger log)
    {
        log.LogInformation("Testing HTTP trigger function processed a request. ");
        log.LogInformation("If you set a name query string parameter or posted a name in JSON it should be echoed back in response.");

        string name = request.Query["name"];

        string requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        dynamic? data = null;
        if (requestBody != string.Empty)
        {
            data = JsonSerializer.Deserialize<dynamic>(requestBody);
        }

        name = name ?? data?.name;

        string responseMessage = string.IsNullOrEmpty(name)
            ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body."
            : $"You submitted name: {name}. This HTTP triggered function executed successfully.";

        return new OkObjectResult(responseMessage);
    }

    [FunctionName("Upsert")]
    public static IActionResult Upsert([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest request, ILogger log)
    {
        log.LogInformation("Function UpsertDocument has started running");

        Document document = new Document();
        int size = (int)request.Body.Length;
        byte[] buffer = new byte[size];
        request.Body.Read(buffer, 0, size);

        log.LogInformation($"buffer size in bytes: {buffer.Length}");

        if (!document.TryPopulateFields(buffer))
        {
            return new StatusCodeResult(500);
        }
        document.id = document.id != null ? document.id : Guid.NewGuid().ToString();
        document.CreateDate = document.CreateDate == DateTime.MinValue ? document.CreateDate : DateTime.Now;
        //document.message = Encoding.UTF8.GetString(buffer); ----already set by TryPopulateFields, or deserialize had an error, nothing to do about it here

        using CosmosClient client = new(connectionString: Environment.GetEnvironmentVariable("connection")!);
        Container container = client.GetContainer(_DatabaseId, _ContainerId);
        PartitionKey partitionKey = new("id");

        string message = String.Empty;
        var result = container.UpsertItemAsync(document).GetAwaiter().GetResult();
        if (result.StatusCode == System.Net.HttpStatusCode.OK)
        {
            message = $"Upsert successful, resource:{JsonSerializer.Serialize(result.Resource)}";
        }
        return new OkObjectResult(message);
    }

}

