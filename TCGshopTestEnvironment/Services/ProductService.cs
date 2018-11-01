﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using TCGshopTestEnvironment.Models;
using TCGshopTestEnvironment.ViewModels;

namespace TCGshopTestEnvironment.Services
{
    public class ProductService : IProducts
    {
        private DBModel _context;

        public ProductService(DBModel context)
        {
            _context = context;
        }

        public void Add(Products NewProduct)
        {
            _context.Add(NewProduct);
            _context.SaveChanges();
            
        }

        public IEnumerable<Products> GetAll()
        {
            return _context.products;
        }

        public Products GetByID(int id)
        {
            
            return _context.products
                    .FirstOrDefault(product => product.ProductId == id);
        }

        public string GetName(int id)
        {
            return _context.products.FirstOrDefault(product => product.ProductId == id).Name;
        }

        public IQueryable<Productsandcategorie> GetbyCardType(string type)
        {
            if (type != "Default")
            {
                return from p in _context.products
                    join c in _context.ProductCategory on p.ProductId equals c.ProductId
                    let categorienames = from d in _context.ProductCategory
                        where p.ProductId == d.ProductId && d.CategoryName == type
                        select d.CategoryName
                       where c.CategoryName == type
                    select new Productsandcategorie { prods = p, Catnames = categorienames.ToList() };
            }
            else
            {
                return from p in _context.products
                    join c in _context.ProductCategory on p.ProductId equals c.ProductId
                    let categorienames = from d in _context.ProductCategory
                        where p.ProductId == d.ProductId
                        select d.CategoryName
                       select new Productsandcategorie { prods = p, Catnames = categorienames.ToList() };
            }
        }

        public IQueryable<Productsandcategorie> GetByNameSearch(string name)
        {
            return from p in _context.products
                join c in _context.ProductCategory on p.ProductId equals c.ProductId
                   where p.Name.ToLower() == name || p.Name.ToLower().Contains(name)
                         let categorienames = from d in _context.ProductCategory
                                              where p.ProductId == d.ProductId
                                              select d.CategoryName

                select new Productsandcategorie { prods = p, Catnames = categorienames.ToList() };
        }

        public List<string> GetCardCatagory(IEnumerable<Products> cards)
        {
            List<string>categoriesList = new List<string>();
            List<string>catagorieresResultList = new List<string>();
            foreach (var card in cards)
            {
                categoriesList = _context.ProductCategory.Where(x => x.ProductId == card.ProductId).Select(x => x.CategoryName).Concat(categoriesList).Distinct().ToList();

            }

            return categoriesList;
        }
    }
    }


