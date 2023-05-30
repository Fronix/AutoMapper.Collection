using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AutoMapper.Collection.EquivalencyExpression;
using AutoMapper.EntityFramework;
using AutoMapper.EquivalencyExpression;
using FluentAssertions;
using Xunit;

namespace AutoMapper.Collection.EntityFramework.Tests
{
    public class EntityFramworkTests : MappingTestBase
    {
        private void ConfigureMapper(IMapperConfigurationExpression cfg)
        {
            cfg.AddCollectionMappers();
            cfg.CreateMap<ThingDto, Thing>().ReverseMap();
            cfg.CreateMap<SoftDeleteThingDto, SoftDeleteThing>();
            cfg.CreateMap<SoftDeleteProductDto, SoftDeleteProduct>()
                .ForMember(x => x.IsDeleted, opt => opt.Ignore())
                .EqualityComparison((dto, entity) => dto.ID == entity.ID);
            cfg.CreateMap<SoftDeleteThing, SoftDeleteThingDto>();
            cfg.CreateMap<SoftDeleteProduct, SoftDeleteProductDto>();
            cfg.SetGeneratePropertyMaps<GenerateEntityFrameworkPrimaryKeyPropertyMaps<DB>>();
        }

        [Fact]
        public void Should_Persist_To_Update()
        {
            var mapper = CreateMapper(ConfigureMapper);

            var db = new DB();
            db.Things.Add(new Thing { Title = "Test2" });
            db.Things.Add(new Thing { Title = "Test3" });
            db.Things.Add(new Thing { Title = "Test4" });
            db.SaveChanges();

            Assert.Equal(3, db.Things.Count());

            var item = db.Things.First();

            db.Things.Persist(mapper).InsertOrUpdate(new ThingDto { ID = item.ID, Title = "Test" });
            Assert.Equal(1, db.ChangeTracker.Entries<Thing>().Count(x => x.State == EntityState.Modified));

            Assert.Equal(3, db.Things.Count());

            db.Things.First(x => x.ID == item.ID).Title.Should().Be("Test");
        }

        [Fact]
        public void Should_Persist_To_Insert()
        {
            var mapper = CreateMapper(ConfigureMapper);

            var db = new DB();
            db.Things.Add(new Thing { Title = "Test2" });
            db.Things.Add(new Thing { Title = "Test3" });
            db.Things.Add(new Thing { Title = "Test4" });
            db.SaveChanges();

            Assert.Equal(3, db.Things.Count());

            db.Things.Persist(mapper).InsertOrUpdate(new ThingDto { Title = "Test" });
            Assert.Equal(3, db.Things.Count());
            Assert.Equal(1, db.ChangeTracker.Entries<Thing>().Count(x => x.State == EntityState.Added));

            db.SaveChanges();

            Assert.Equal(4, db.Things.Count());

            db.Things.OrderByDescending(x => x.ID).First().Title.Should().Be("Test");
        }

        [Fact]
        public void Should_Call_Delete()
        {
            var mapper = CreateMapper(ConfigureMapper);

            var db = new DB();
            db.SoftDeleteThings.Add(new SoftDeleteThing { 
                Title = "Thing 1",
                Products = new List<SoftDeleteProduct> { 
                    new SoftDeleteProduct { Name = "Product 1" },
                    new SoftDeleteProduct { Name = "Product 2" },
                    new SoftDeleteProduct { Name = "Product 3" },
                    new SoftDeleteProduct { Name = "Product 4" }
                }
            });
            db.SaveChanges();

            Assert.Equal(1, db.SoftDeleteThings.Count());
            Assert.Equal(4, db.SoftDeleteProducts.Count());

            var softThings = db.SoftDeleteThings.Include(x => x.Products).Single();
            var softThingDto = mapper.Map<SoftDeleteThing, SoftDeleteThingDto>(softThings);

            // Remove Product 4 from dto
            softThingDto.Products.RemoveAt(3);

            // Map from dto to entity
            var updated = mapper.Map(softThingDto, softThings);
            db.SaveChanges();

            Assert.Single(db.SoftDeleteProducts.Where(x => x.IsDeleted));
            Assert.Equal(4, db.SoftDeleteProducts.Count());
        }

        public class DB : DbContext
        {
            public DB()
                : base(Effort.DbConnectionFactory.CreateTransient(), contextOwnsConnection: true)
            {
                Things.RemoveRange(Things);
                SoftDeleteThings.RemoveRange(SoftDeleteThings);
                SoftDeleteProducts.RemoveRange(SoftDeleteProducts);
                SaveChanges();
            }

            public DbSet<Thing> Things { get; set; }

            public DbSet<SoftDeleteThing> SoftDeleteThings { get; set; }

            public DbSet<SoftDeleteProduct> SoftDeleteProducts { get; set; }
        }

        public class Thing
        {
            public int ID { get; set; }
            public string Title { get; set; }
            public override string ToString() { return Title; }
        }

        public class ThingDto
        {
            public int ID { get; set; }
            public string Title { get; set; }
        }

        public class SoftDeleteThing
        {
            public int ID { get; set; }
            public string Title { get; set; }

            public ICollection<SoftDeleteProduct> Products { get; set; }
        }

        public class SoftDeleteProduct : ISoftDelete
        {
            public int ID { get; set; }
            public string Name { get; set; }

            public bool IsDeleted { get; set; }

            public void Delete() => IsDeleted = true;
        }

        public class SoftDeleteThingDto
        {
            public int ID { get; set; }

            public string Title { get; set; }

            public List<SoftDeleteProductDto> Products { get; set; }
        }

        public class SoftDeleteProductDto
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }
    }
}
