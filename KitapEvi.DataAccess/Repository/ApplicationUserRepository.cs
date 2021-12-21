using KitapEvi.DataAccess.Data;
using KitapEvi.DataAccess.Repository.IRepository;
using KitapEvi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KitapEvi.DataAccess.Repository
{
    public class ApplicationUserRepository : Repository<ApplicationUser>, IApplicationUserRepository
    {
        private readonly ApplicationDbContext _db;

        public ApplicationUserRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

    }
}
