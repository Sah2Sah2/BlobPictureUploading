using Azure.Storage.Blobs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        // Load the connection string from appsettings.json
        string connectionString = LoadConnectionStringFromConfig();
        string containerName = "product-images";

        // Path to JSON file (Copy if newer in properties)
        string jsonFilePath = Path.Combine("Resources", "ListOfProducts.json");

        // Read JSON file
        var json = await File.ReadAllTextAsync(jsonFilePath);
        var products = JsonConvert.DeserializeObject<List<Product>>(json);

        // Create a BlobServiceClient to interact with Blob Storage
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        await containerClient.CreateIfNotExistsAsync();

        // Go over each product in the JSON file
        foreach (var product in products)
        {
            try
            {
                // Download the image from the URL
                var imageBytes = await DownloadImageAsync(product.ImageUrl);

                // Sanitize filename
                string safeName = SanitizeFileName(product.Name);
                string imageFileName = $"{safeName}_{product.Id}.jpg";

                // Upload the image to Azure Blob Storage
                await UploadImageToBlobAsync(containerClient, imageFileName, imageBytes);

                Console.WriteLine($"✅ Successfully uploaded image for: {product.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error uploading image for {product?.Name ?? "Unknown"}: {ex}");
            }
        }
    }

    static async Task<byte[]> DownloadImageAsync(string imageUrl)
    {
        using (var httpClient = new HttpClient())
        {
            return await httpClient.GetByteArrayAsync(imageUrl);
        }
    }

    static async Task UploadImageToBlobAsync(BlobContainerClient containerClient, string fileName, byte[] imageBytes)
    {
        var blobClient = containerClient.GetBlobClient(fileName);
        using (var stream = new MemoryStream(imageBytes))
        {
            await blobClient.UploadAsync(stream, overwrite: true);
        }
    }

    static string SanitizeFileName(string name)
    {
        // Replace invalid characters with _
        return Regex.Replace(name, @"[^a-zA-Z0-9_\-]", "_");
    }

    static string LoadConnectionStringFromConfig()
    {
        try
        {
            var configPath = "appsettings.json";
            if (!File.Exists(configPath))
                throw new FileNotFoundException("appsettings.json file not found");

            var jsonString = File.ReadAllText(configPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<Config>(jsonString);

            if (config?.AzureStorage?.ConnectionString == null)
                throw new InvalidOperationException("Connection string is missing in the config file.");

            return config.AzureStorage.ConnectionString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading connection string: {ex.Message}");
            throw;
        }
    }
}

// Define the structure of your JSON data
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Company { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public string Genre { get; set; }
    public double Price { get; set; }
    public string ImageUrl { get; set; }
    public int Stock { get; set; }
    public int PGRating { get; set; }
}

public class Config
{
    public AzureStorageConfig AzureStorage { get; set; }
}

public class AzureStorageConfig
{
    public string ConnectionString { get; set; }
}
