using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FunctionApp
{
    public class Orders
    {
        [FunctionName("RegisterOrder")]
        public static async Task RegisterOrder(
            [ServiceBusTrigger("orders", Connection = "AzureServiceBus")] ServiceBusReceivedMessage message,
            [Blob("orders/{rand-guid}.json", FileAccess.Write, Connection = "AzureWebJobsStorage")] Stream blobStream,
            ILogger logger)
        {
            var order = JsonConvert.DeserializeObject<OrderDto<int>>(message.Body.ToString());

            if (!order.OrderItems.Any())
            {
                var errorMessage = $"Order with ID {order.id} has no order items.";
                logger.LogError(errorMessage); 
                throw new Exception(errorMessage);
            }

            await message.Body.ToStream().CopyToAsync(blobStream);
        }

        [FunctionName("RegisterOrderForDelivery")]
        public static IActionResult RegisterOrderForDelivery(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            [CosmosDB(
                databaseName: "eshop",
                containerName: "orders",
                Connection = "CosmosDBConnection",
                CreateIfNotExists = true)] out dynamic document,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to write to Cosmos DB.");

            string requestBody = new StreamReader(req.Body).ReadToEnd();

            if (string.IsNullOrEmpty(requestBody))
            {
                document = null;
                return new BadRequestObjectResult("Please pass a JSON document in the request body.");
            }

            var dto = JsonConvert.DeserializeObject<OrderDto<int>>(requestBody);
            document = new OrderDto<string>
            {
                id = dto.id.ToString(),
                BuyerId = dto.BuyerId,
                OrderDate = dto.OrderDate,
                ShipToAddress = new AddressDto
                {
                    Street = dto.ShipToAddress.Street,
                    City = dto.ShipToAddress.City,
                    State = dto.ShipToAddress.State,
                    Country = dto.ShipToAddress.Country,
                    ZipCode = dto.ShipToAddress.ZipCode
                },
                OrderItems = dto.OrderItems.Select(item => new OrderItemDto
                {
                    ItemOrdered = new CatalogItemOrderedDto
                    {
                        CatalogItemId = item.ItemOrdered.CatalogItemId,
                        ProductName = item.ItemOrdered.ProductName,
                        PictureUri = item.ItemOrdered.PictureUri
                    },
                    UnitPrice = item.UnitPrice,
                    Units = item.Units
                })
            };

            string responseMessage = "This HTTP triggered function executed successfully and wrote a document to Cosmos DB.";

            return new OkObjectResult(responseMessage);
        }
    }

    file class OrderDto<T>
    {
        public T id { get; set; }
        public string BuyerId { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public AddressDto ShipToAddress { get; set; }
        public IEnumerable<OrderItemDto> OrderItems { get; set; }
    }

    file class AddressDto
    {
        public string Street { get; set; }

        public string City { get; set; }

        public string State { get; set; }

        public string Country { get; set; }

        public string ZipCode { get; set; }
    }

    file class OrderItemDto
    {
        public CatalogItemOrderedDto ItemOrdered { get; set; }
        public decimal UnitPrice { get; set; }
        public int Units { get; set; }
    }

    file class CatalogItemOrderedDto
    {
        public int CatalogItemId { get; set; }
        public string ProductName { get; set; }
        public string PictureUri { get; set; }
    }
}
