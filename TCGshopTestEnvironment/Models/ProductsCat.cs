﻿using System;
using System.ComponentModel.DataAnnotations;

namespace TCGshopTestEnvironment.Models
{
    public class ProductsCat
    {
        [Key]
        public int ProductId { get; set; }

        public string Name { get; set; }

        public UserAccount Owner { get; set; }

        public decimal Price { get; set; }

        public string Description { get; set; }

        public string Grade { get; set; }

        public int Stock { get; set; }
        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }

        public int ViewsListed { get; set; }

        public int ViewsDetails { get; set; }

        public string ImageUrl { get; set; }

        public string CategoryName { get; set; }

        public DateTime AuctionEndTime { get; set; }
    }
}