using System;
using System.ComponentModel.DataAnnotations;

namespace CamelizeProductAvailable
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }
        public string ProductAmazonUrl { get; set; }
        public string ProductCamelCamelCamelUrl { get; set; }
        public string SearchString { get; set; }
        public string Description { get; set; }
        public bool IsOutOfStock { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
