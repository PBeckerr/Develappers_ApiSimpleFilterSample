using System;
using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using SimpleFilterApi.Models;

namespace SimpleFilterApi.Database
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Data.Person> Persons { get; set; }

        public static void SeedData(ApplicationDbContext dbContext)
        {
            if (dbContext.Persons.Any())
            {
                return;
            }

            dbContext.Persons.AddRange(new Data.Person(Guid.Parse("F544AA70-7959-4069-9B90-6B3FE6E07D5F"), "Foo")
                                       {
                                           Age = 10
                                       },
                                       new Data.Person(Guid.Parse("A7B3A9BF-7F3A-4E66-B0A3-BE4A640A2F3E"), "Bar")
                                       {
                                           Age = 50
                                       },
                                       new Data.Person(Guid.Parse("A7549D64-BB9B-4290-83ED-41E838D03A2C"), "FooBar")
                                       {
                                           Age = 60
                                       });

            dbContext.SaveChanges();
        }
    }
}