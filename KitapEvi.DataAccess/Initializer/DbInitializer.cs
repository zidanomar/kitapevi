using KitapEvi.DataAccess.Data;
using KitapEvi.Models;
using KitapEvi.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KitapEvi.DataAccess.Initializer
{
	public class DbInitializer : IDbInitializer
	{
		private readonly ApplicationDbContext _db;
		private readonly UserManager<IdentityUser> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;

		public DbInitializer (ApplicationDbContext db, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
		{
			_db = db;
			_userManager = userManager;
			_roleManager = roleManager;
		}

		public void Initialize()
		{
			try
			{
				if (_db.Database.GetPendingMigrations().Count() > 0)
				{
					_db.Database.Migrate();
				}
			}
			catch (Exception)
			{
				throw;
			}

			if (_db.Roles.Any(r => r.Name == SharedDetail.Role_Admin)) return;

			_roleManager.CreateAsync(new IdentityRole(SharedDetail.Role_Admin)).GetAwaiter().GetResult();
			_roleManager.CreateAsync(new IdentityRole(SharedDetail.Role_Employee)).GetAwaiter().GetResult();
			_roleManager.CreateAsync(new IdentityRole(SharedDetail.Role_User_Indi)).GetAwaiter().GetResult();
			_roleManager.CreateAsync(new IdentityRole(SharedDetail.Role_User_Comp)).GetAwaiter().GetResult();

			_userManager.CreateAsync(new ApplicationUser
			{
				UserName = "b181210568@sakarya.edu.tr",
				Email = "b181210568@sakarya.edu.tr",
				EmailConfirmed = true,
				Name = "Zidan Omar",
			}, "123").GetAwaiter().GetResult();

			ApplicationUser user = _db.ApplicationUsers.Where(u => u.Email == "b181210568@sakarya.edu.tr").FirstOrDefault();

			_userManager.AddToRoleAsync(user, SharedDetail.Role_Admin).GetAwaiter().GetResult();
		}
	}
}
