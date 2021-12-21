using KitapEvi.DataAccess.Data;
using KitapEvi.DataAccess.Repository.IRepository;
using KitapEvi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KitapEvi.DataAccess.Repository
{
    public class CategoryRepository : Repository<Category>, ICategoryRepository
    {
        private readonly ApplicationDbContext _db;

        public CategoryRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(Category category)
        {
            var objFromDb = _db.Categories.FirstOrDefault(s => s.Id == category.Id);
            if (objFromDb != null)
            {
                objFromDb.Name = category.Name;
                _db.SaveChanges();
            }
        }
    }
}
