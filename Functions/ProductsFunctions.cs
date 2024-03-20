using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using badmincorazonrosa.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace badmincorazonrosa.Functions
{
    public class ProductsFunctions
    {
        private readonly ILogger<ProductsFunctions> _logger;
        private readonly IMongoCollection<Product> _productCollection;

        public ProductsFunctions(ILogger<ProductsFunctions> log)
        {
            _logger = log;
            string connectionString = Environment.GetEnvironmentVariable("MongoConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("MongoConnectionString no está configurado en las variables de entorno.");
            }

            var mongoClient = new MongoClient(connectionString);
            var database = mongoClient.GetDatabase("corazonrosadb");
            _productCollection = database.GetCollection<Product>("products");
        }

        [FunctionName("GetProducts")]
        [OpenApiOperation(operationId: "GetProducts", tags: new[] { "Product" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Product[]), Description = "The OK response")]
        public async Task<IActionResult> GetProducts(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetProducts")] HttpRequest req)
        {
            _logger.LogInformation("Getting all products.");

            try
            {
                var filter = Builders<Product>.Filter.Empty;
                var products = await _productCollection.Find(filter).ToListAsync();

                return new OkObjectResult(products);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting products: {ex.Message}");
                return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
            }
        }

        [FunctionName("CreateProduct")]
        [OpenApiOperation(operationId: "CreateProduct", tags: new[] { "Product" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Product), Required = true, Description = "Product object that needs to be added")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> CreateProduct(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "CreateProduct")] HttpRequest req)
        {
            _logger.LogInformation("Creating a new product.");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var product = JsonConvert.DeserializeObject<Product>(requestBody);
                product.Id = ObjectId.GenerateNewId().ToString();

                await _productCollection.InsertOneAsync(product);

                return new OkObjectResult("Product created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating product: {ex.Message}");
                return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
            }
        }

        [FunctionName("UpdateProduct")]
        [OpenApiOperation(operationId: "UpdateProduct", tags: new[] { "Product" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "ID of the product to update")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Product), Required = true, Description = "Product object with updated information")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "text/plain", bodyType: typeof(string), Description = "Product not found")]
        public async Task<IActionResult> UpdateProduct(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "UpdateProduct/{id}")] HttpRequest req,
            string id)
        {
            _logger.LogInformation($"Updating product with id: {id}");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var updatedProduct = JsonConvert.DeserializeObject<Product>(requestBody);

                var filter = Builders<Product>.Filter.Eq(p => p.Id, id);
                var result = await _productCollection.ReplaceOneAsync(filter, updatedProduct);

                if (result.IsAcknowledged && result.ModifiedCount > 0)
                {
                    return new OkObjectResult("Product updated successfully.");
                }
                else
                {
                    return new NotFoundObjectResult("Product not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating product: {ex.Message}");
                return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
            }
        }


        [FunctionName("DeleteProduct")]
        [OpenApiOperation(operationId: "DeleteProduct", tags: new[] { "Product" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "ID of the product to delete")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "text/plain", bodyType: typeof(string), Description = "Product not found")]
        public async Task<IActionResult> DeleteProduct(
           [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "DeleteProduct/{id}")] HttpRequest req,
           string id)
        {
            _logger.LogInformation($"Deleting product with id: {id}");

            try
            {
                var filter = Builders<Product>.Filter.Eq(p => p.Id, id);
                var result = await _productCollection.DeleteOneAsync(filter);

                if (result.IsAcknowledged && result.DeletedCount > 0)
                {
                    return new OkObjectResult("Product deleted successfully.");
                }
                else
                {
                    return new NotFoundObjectResult("Product not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting product: {ex.Message}");
                return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
            }
        }

    }
}