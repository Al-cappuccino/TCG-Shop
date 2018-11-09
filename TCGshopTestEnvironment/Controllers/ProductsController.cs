using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2;
using TCGshopTestEnvironment.Services;
using TCGshopTestEnvironment.ViewModels;
using X.PagedList.Mvc;
using X.PagedList;
using Microsoft.EntityFrameworkCore;
using TCGshopTestEnvironment.Models;
using TCGshopTestEnvironment.Models.JoinTables;
using Amazon.S3;
using Amazon;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Amazon.S3.Transfer;
using System.IO;
using System.Net;

namespace TCGshopTestEnvironment.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IProducts _assets;

        private readonly DBModel _context;

        //S3
        public static AmazonS3Config config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.USEast1, // MUST set this before setting ServiceURL and it should match the `MINIO_REGION` enviroment variable.
            ServiceURL = "http://10.0.0.10:9000", // replace http://localhost:9000 with URL of your minio server
            ForcePathStyle = true // MUST be true to work correctly with Minio server
        };
        public static AmazonS3Client amazonS3Client = new AmazonS3Client(Startup.accessKey, Startup.secretKey, config);
        //S3


        public ProductsController(IProducts assets, DBModel context)
        {
            _assets = assets;
            _context = context;
        }

        public async Task<IActionResult> Index(int? page, int? pageAmount, string cardType, string sortBy, [FromQuery] List<string> catagorie, [FromQuery] List<string> grades, float? priceLow, float? priceHigh)
        {
            //if page/pageamount/price parameters are empty, fill with standard value
            var pageNumber = page ?? 1;
            var pageAmnt = pageAmount ?? 16;

            float priceL = priceLow ?? 0;
            float priceH = priceHigh ?? 10000;


            //queries to get cards and catagories from database
            var assetModels = _assets.GetbyCardType(cardType);
            List<string> cardscategory = new List<string>();
            foreach (var item in assetModels.Select(x => x.Catnames).Distinct())
            {
                foreach (var catagory in item.Distinct())
                {
                    if (!cardscategory.Contains(catagory))
                    {
                        cardscategory.Add(catagory);
                    }
                }

            }

            //viewbags to send to the view
            ViewBag.page = page;
            ViewBag.PageAmount = pageAmount;
            ViewBag.name = "Name";
            ViewBag.totalCategory = cardscategory;
            ViewBag.catagorie = catagorie;
            ViewBag.catagoriestring = "";
            ViewBag.grades = grades;
            ViewBag.PriceLow = priceL;
            ViewBag.PriceHigh = priceH;
            ViewBag.cardType = cardType;

            // sorting list for product sorting
            var sorting = new List<SelectListItem>
                {
                    new SelectListItem {Text = "Name A-Z", Value = "name"},
                    new SelectListItem {Text = "Name Z-A", Value = "name_desc"},
                    new SelectListItem {Text = "Price High-Low", Value = "Price"},
                    new SelectListItem {Text = "Price Low-High", Value = "price_desc"}
                };
            ViewBag.Sorting = sorting;
            ViewBag.SelectSort = sortBy ?? "Name A-Z";
            ViewBag.sortBy = sortBy;

            //bind products from query(assetModels) to productviewmodel(listingResult) that is used in the view.
            var listingResult = assetModels
                .Select(result => new ProductsViewModel
                {
                    Id = result.prods.ProductId,
                    Name = result.prods.Name.Length < 20 ? result.prods.Name : result.prods.Name.Substring(0, 15) + "...",
                    Price = result.prods.Price,
                    ImageUrl = result.prods.ImageUrl,
                    Grade = result.prods.Grade,
                    Stock = result.prods.Stock,
                    CardCatagoryList = result.Catnames

                });

            //filters
            if (catagorie.Count > 0)
            {
                //if statement to make sure when in catagorie if you select same catagorie name it doesn't run any code, for performance
                if (catagorie.Count == 1 && catagorie.Contains(cardType))
                {

                }
                else
                {
                    listingResult = listingResult.Where(x => x.CardCatagoryList.Intersect(catagorie).Any());
                }

            }

            if (priceL > 0 || priceH < 10000)
            {
                listingResult = listingResult.Where(x => x.Price >= priceL && x.Price <= priceH);
            }

            //viewbag for the view with all the grades in it.
            ViewBag.Grade = listingResult.Select(x => x.Grade).Distinct();

            if (grades.Count > 0 && listingResult.Count(x => grades.Contains(x.Grade)) > 0)
            {
                listingResult = listingResult.Where(x => grades.Contains(x.Grade));
            }

            //sorting
            switch (sortBy)
            {
                case "name_desc":
                    listingResult = listingResult.OrderByDescending(s => s.Name);
                    break;
                case "Price":
                    listingResult = listingResult.OrderByDescending(s => s.Price);
                    break;
                case "price_desc":
                    listingResult = listingResult.OrderBy(s => s.Price);
                    break;
                default:
                    listingResult = listingResult.OrderBy(s => s.Name);
                    break;
            }

            var onePageOfProducts = await listingResult.AsNoTracking().ToPagedListAsync(pageNumber, pageAmnt);
            ViewBag.OnePageOfProducts = onePageOfProducts;

            if (cardType == "Pokemon")
            {
                return View("~/Views/Products/Pokemon/Pokemon.cshtml");
            }
            if (cardType == "YuGiOh")
            {
                return View("~/Views/Products/YuGiOh/YuGiOh.cshtml");
            }
            if (cardType == "Magic")
            {
                return View("~/Views/Products/Magic/Magic.cshtml");
            }
            return View();
            
        }

        public IActionResult Detail(int id)
        {
            var asset = _assets.GetByID(id);

            var model = new ProductsDetailModel
            {
                Description = asset.Description,
                Grade = asset.Grade,
                Name = asset.Name,
                Price = asset.Price,
                Stock = asset.Stock,
                ImageUrl = asset.ImageUrl,

            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Search(int? page, int? pageAmount, string name, string sortBy, [FromQuery] List<string> catagorie, [FromQuery] List<string> grades, float? priceLow, float? priceHigh)
        {
            if (!String.IsNullOrEmpty(name))
            {
                var pageNmber = page ?? 1;
                var pageAmnt = pageAmount ?? 16;

                float priceL = priceLow ?? 0;
                float priceH = priceHigh ?? 10000;


                //queries to get items and catagories from database
                var assetmodel = _assets.GetByNameSearch(name.ToLower());
                List<string> cardscategory = new List<string>();
                foreach (var item in assetmodel.Select(x => x.Catnames).Distinct())
                {
                    foreach (string item2 in item)
                    {
                        if (!cardscategory.Contains(item2))
                        {
                            cardscategory.Add(item2);
                        }
                    }
                    
                }


                //viewbags to send to the view
                ViewBag.page = page;
                ViewBag.PageAmount = pageAmount;
                ViewBag.name = name;
                ViewBag.totalCategory = cardscategory;
                ViewBag.catagorie = catagorie;
                ViewBag.catagoriestring = "";
                ViewBag.grades = grades;
                ViewBag.PriceLow = priceL;
                ViewBag.PriceHigh = priceH;
                ViewBag.cardType = "";

                // sorting list
                List<SelectListItem> Sorting = new List<SelectListItem>
                {
                    new SelectListItem {Text = "Name A-Z", Value = "name"},
                    new SelectListItem {Text = "Name Z-A", Value = "name_desc"},
                    new SelectListItem() {Text = "Price High-Low", Value = "Price"},
                    new SelectListItem() {Text = "Price Low-High", Value = "price_desc"}
                };
                ViewBag.Sorting = Sorting;
                ViewBag.SelectSort = sortBy ?? "Name A-Z";
                ViewBag.sortBy = sortBy;


                // bind all products from database to productviewmodel
                var listingResult = assetmodel
                    .Select(result => new ProductsViewModel
                    {
                        Id = result.prods.ProductId,
                        Name = result.prods.Name.Length < 20 ? result.prods.Name : result.prods.Name.Substring(0, 15) + "...",
                        Price = result.prods.Price,
                        ImageUrl = result.prods.ImageUrl,
                        Grade = result.prods.Grade,
                        Stock = result.prods.Stock,
                        CardCatagoryList = result.Catnames
                        
                    });

                
                //filters
                if (catagorie.Count > 0)
                {
                    listingResult = listingResult.Where(x => x.CardCatagoryList.Intersect(catagorie).Any());
                }


                if (priceL > 0 || priceH < 10000)
                {
                    listingResult = listingResult.Where(x => x.Price >= priceL && x.Price <= priceH);
                }

                //viewbag for the view with all the grades in it.
                ViewBag.Grade = listingResult.Select(x => x.Grade).Distinct();

                if (grades.Count > 0 && listingResult.Count(x => grades.Contains(x.Grade)) > 0)
                {
                    listingResult = listingResult.Where(x => grades.Contains(x.Grade));
                }

                //sorting
                switch (sortBy)
                {
                    case "name_desc":
                        listingResult = listingResult.OrderByDescending(s => s.Name);
                        break;
                    case "Price":
                        listingResult = listingResult.OrderByDescending(s => s.Price);
                        break;
                    case "price_desc":
                        listingResult = listingResult.OrderBy(s => s.Price);
                        break;
                    default:
                        listingResult = listingResult.OrderBy(s => s.Name);
                        break;
                }


                var onePageOfProducts = await listingResult.AsNoTracking().ToPagedListAsync(pageNmber, pageAmnt);
                ViewBag.OnePageOfProducts = onePageOfProducts;
                return View();
            }
            else
            {
                return RedirectToAction("Index","Home");
            }
        }

        public JsonResult CardAutoCompleteResult(string text)
        {

            IEnumerable<string> cardname = _assets.GetByNameSearch(text).Select(x => x.prods.Name).ToList();

            return Json(cardname);
        }




        //Adding New product
        [HttpGet]
        public IActionResult NewProduct()
        {
            return View();

        }

        [HttpGet]
        public IActionResult GetCategoryAll()
        {
            IEnumerable<string> categories = _context.categories.Select(x => x.CategoryName).ToList();
            return Json(categories);

        }

        [HttpPost]
        public async Task<IActionResult> NewProduct(ProductsNewProductViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var ImageUrl = vm.ImageUrl;

                Products Product = new Products
                {
                    Name = vm.Name,
                    
                    Price = vm.Price,
                    Grade = vm.Grade,
                    Stock = vm.Stock,
                };
                _context.Add(Product);

                IEnumerable<string> categories = _context.categories.Select(x => x.CategoryName).ToList();
                foreach (string TestCategory in vm.Category)
                {
                    if (categories.Contains(TestCategory))
                    {
                        Console.WriteLine("Category: " + TestCategory + "is in database");

                    }
                    else
                    {
                        Console.WriteLine("Category: " + TestCategory + " is NOT in database, ADDING!");

                        //Here code to add new category to database
                        Category category = new Category
                        {
                            CategoryName = TestCategory,
                            Description = "NULL"
                        };
                        _context.Add(category);
                    }

                    //Adding date to merge Table
                    ProductCategory productCategory = new ProductCategory
                    {
                        ProductId = Product.ProductId,
                        CategoryName = TestCategory
                    };
                    _context.Add(productCategory);
                }
                
                _context.SaveChanges();
                

            }

            return View();
        }

        //Uploading Image
        [HttpGet]
        public async Task<IActionResult> S3Test()
        {
            // uncomment the following line if you like to troubleshoot communication with S3 storage and implement private void OnAmazonS3Exception(object sender, Amazon.Runtime.ExceptionEventArgs e)
            // amazonS3Client.ExceptionEvent += OnAmazonS3Exception;
            var listBucketResponse = await amazonS3Client.ListBucketsAsync();
            var response = new List<string>();
            foreach (var bucket in listBucketResponse.Buckets)
            {
                response.Add("bucket '" + bucket.BucketName + "' created at " + bucket.CreationDate);
            }
            if (listBucketResponse.Buckets.Count > 0)
            {
                var bucketName = listBucketResponse.Buckets[0].BucketName;

                var listObjectsResponse = await amazonS3Client.ListObjectsAsync(bucketName);

                foreach (var obj in listObjectsResponse.S3Objects)
                {
                    response.Add("key = '" + obj.Key + "' | size = " + obj.Size + " | tags = '" + obj.ETag + "' | modified = " + obj.LastModified);
                }
            }
            return Json(response);
        }

        [HttpPost]
        public async Task<IActionResult> FileUpload(FileUpload formFile)
        {
            // Perform an initial check to catch FileUpload class attribute violations.
            if (!ModelState.IsValid)

            {
                return Json(new { status = "error", message = "The model is not correct" });
            }


            //Check MIME
            if (formFile.CardImageUpload.ContentType.ToLower() != "image/png")
                return Json(new
                {
                    status = "error",
                    message = "The file mime must be image/png"
                });

            //Check if size is between 0 and 10MB
            if (formFile.CardImageUpload.Length == 0)
            {
                return Json(new
                {
                    status = "error",
                    message = "Upload Failed. The selected file is empty."
                });
            }
            else if (formFile.CardImageUpload.Length > 10485760)
            {
                return Json(new
                {
                    status = "error",
                    message = "The selected file exceeds 10 MB."
                });
            }

            //Generate Random Name
            //string ext = System.IO.Path.GetExtension(formFile.CardImageUpload.FileName); //Get the file extension

            
            var filePath = System.IO.Path.GetTempFileName() + Guid.NewGuid() + ".png"; //Create Temp File with Random GUID

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await formFile.CardImageUpload.CopyToAsync(fileStream);
            }

            return Json(new
            {
                status = "Uploaded",
                message = filePath
            });
        }

        [HttpGet]
        public IActionResult FileUpload()
        {
            return View();

        }






        //public IActionResult UploadFiles(List<IFormFile> files)
        //{
        //    long size = files.Sum(f => f.Length);

        //    foreach (var formFile in files)
        //    {
        //        if (formFile.Length > 0)
        //        {
        //            var filename = ContentDispositionHeaderValue
        //                .Parse(formFile.ContentDisposition)
        //                .FileName
        //                .TrimStart().ToString();
        //            filename = _hostingEnvironment.WebRootPath + $@"\uploads" + $@"\{formFile.FileName}";
        //            size += formFile.Length;
        //            using (var fs = System.IO.File.Create(filename))
        //            {
        //                formFile.CopyTo(fs);
        //                fs.Flush();
        //            }//these code snippets saves the uploaded files to the project directory

        //            uploadToS3(filename);//this is the method to upload saved file to S3

        //        }
        //    }

        //    return Ok(true);
        //}
    }
}

