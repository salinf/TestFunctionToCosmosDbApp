using System;
using System.Text.Json;

namespace TestFunctionToCosmosDbApp.DomainObjects;

public class Document
{
    public string id { get; set; }
    public string message { get; set; }
    public DateTime? CreateDate { get; set; }

    public bool TryPopulateFields(byte[] buffer)
    {
        Document? aDocument = JsonSerializer.Deserialize<Document>(buffer);

        if (aDocument != null)
        {
            id = aDocument.id;
            message = aDocument.message;
            CreateDate = aDocument.CreateDate;
            return true;
        }
        return false;
    }
}
