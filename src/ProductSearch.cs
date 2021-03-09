using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Io;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Newtonsoft.Json;

namespace CamelizeProductAvailable
{
    public class ProductSearch
    {
        private readonly ILogger _logger;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
        private readonly string camelProductPrefixUrl = "https://camelcamelcamel.com/product/";
        private const string MailTitle = "Product Availability Update";
        private const string ProductsJson = "products.json";
        private const int SleepSeconds = 1000 * 5;
        private const string AmazonUrl = "www.amazon.com";
        private const string CamelUrl = "camelcamelcamel.com";

        public ProductSearch(ILogger<ProductSearch> logger, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }
        internal async Task Run()
        {
            _logger.LogInformation("Application Started at {dateTime}", DateTime.UtcNow);

            
            var myJsonString = await File.ReadAllTextAsync(ProductsJson);
            var myJsonObject = JsonConvert.DeserializeObject<MyJsonType>(myJsonString);
            var emailText = new StringBuilder();
            foreach (var url in myJsonObject.OutOfStockProductUrl)
            {
                Console.WriteLine(url);
                var result = await CheckForUpdates(url);
                
                Thread.Sleep(SleepSeconds);
                emailText.Append(result);
            }
            if (emailText.Length != 0)
            {               
                await SendEmail(emailText.ToString(), MailTitle);
            }

            _logger.LogInformation("Application Ended at {dateTime}", DateTime.UtcNow);
        }

        private async Task SendEmail(string msgBody, string msgSubject)
        {
            string hostAddress = _config.GetValue<string>("Email:HostAddress");
            int hostPort = _config.GetValue<int>("Email:HostPort");
            string senderEmail = _config.GetValue<string>("Email:Sender:Email");
            string senderPassword = _config.GetValue<string>("Email:Sender:Password");
            string recipientEmail = _config.GetValue<string>("Email:RecipientEmail");

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("Price Alert", senderEmail));
            msg.To.Add(new MailboxAddress("Me", recipientEmail));

            msg.Subject = msgSubject;
            msg.Body = new TextPart("plain") { Text = msgBody };

            using var client = new SmtpClient
            {
                ServerCertificateValidationCallback = (s, c, h, e) => true
            };
            await client.ConnectAsync(hostAddress, hostPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(senderEmail, senderPassword);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }

        private async Task<List<dynamic>> GetPageData(string url, List<dynamic> results)
        {
            var requester = new DefaultHttpRequester("Mozilla/5.0 (Macintosh; Intel Mac OS X 10.14; rv:68.0) Gecko/20100101 Firefox/68.0");

            var config = Configuration.Default.With(requester).WithDefaultLoader().WithDefaultCookies();
            var context = BrowsingContext.New(config);

            try
            {
                IDocument document;
                
                if (url.Contains(AmazonUrl))
                {
                    //var productId = url.Substring(url.LastIndexOf("/", StringComparison.Ordinal) + 1);
                    var productId = url[(url.LastIndexOf("/", StringComparison.Ordinal) + 1)..];
                    var parsedCamelUrl = camelProductPrefixUrl + productId;
                    document = await context.OpenAsync(parsedCamelUrl);
                }
                else
                {
                    if (url.Contains(CamelUrl))
                    {
                        document = await context.OpenAsync(url);
                    }
                    else
                    {
                        return null;
                    }
                }

                ParseCamelData(document, out var camelProductUrl, out var camelProductLookUpKey, out var productDescription, out IHtmlCollection<IElement> currentProductPriceElements);


                bool isOutOfStock = IsProductOutOfStock(currentProductPriceElements);

                var product = new Product
                {
                    Description = string.IsNullOrEmpty(productDescription) ? "Description Not Available" : productDescription,
                    IsOutOfStock = isOutOfStock,
                    ProductAmazonUrl = "https://www.amazon.com/dp/" + camelProductLookUpKey,
                    ProductCamelCamelCamelUrl = camelProductUrl,
                    LastUpdated = DateTime.Now,
                    Created = DateTime.Now
                };
                results.Add(product);

                return results;

            }
            catch(Exception e) 
            {
                _logger.LogError(e, e.Message);
                return null;
            }
        }

        private static bool IsProductOutOfStock(IHtmlCollection<IElement> currentProductPriceElements)
        {
            var isOutOfStock = true;

            foreach (var row in currentProductPriceElements)
            {
                if (!row.InnerHtml.Contains("Not in Stock"))
                {
                    isOutOfStock = false;
                    break;
                }
            }

            return isOutOfStock;
        }

        private void ParseCamelData(IDocument document, out string camelProductUrl, out string camelProductLookUpKey, out string productDescription, out IHtmlCollection<IElement> currentProductPriceElements)
        {
            if (document.DocumentElement.OuterHtml.Contains("Please turn JavaScript on and reload the page."))
            {
                _logger.LogInformation("DDOS protection enabled");
            }
            
            var productDescriptionElements = document.QuerySelectorAll("div.row.column.show-for-medium h2:nth-child(2) a:nth-child(1)");
            var camelProductUrlElement = document.QuerySelectorAll("meta[property='og:url']").Select(m => m.GetAttribute("content"));
            camelProductUrl = camelProductUrlElement.FirstOrDefault();
            camelProductLookUpKey = camelProductUrl?.Substring(camelProductUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);
            productDescription = productDescriptionElements.FirstOrDefault()?.InnerHtml;
            currentProductPriceElements = document.QuerySelectorAll("table.product_pane tbody tr:nth-child(1) td:nth-child(2)");
        }

        private async Task<string> CheckForUpdates(string url)
        {            
            var products = new List<dynamic>();
            await GetPageData(url, products);
            var mailBody = "";

            foreach (Product product in products)
            {
                if (!product.IsOutOfStock)
                {
                    mailBody += $"NEW: {product.Description} for {product.IsOutOfStock} has been added ({product.ProductCamelCamelCamelUrl})\n";
                }
            }

            return mailBody;           

        }
    }
}
