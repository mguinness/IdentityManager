using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using IdentityManager.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace IdentityManager.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("api/[action]")]
        public IActionResult UserList(int draw, List<Dictionary<string, string>> columns, List<Dictionary<string, string>> order, int start, int length, Dictionary<string, string> search)
        {
            var users = _userManager.Users;

            string filter = search["value"];
            var qry = users.Where(u =>
                (String.IsNullOrWhiteSpace(filter) || u.Email.Contains(filter)) &&
                (String.IsNullOrWhiteSpace(filter) || u.UserName.Contains(filter))
            );

            var idx = Int32.Parse(order[0]["column"]);
            var dir = order[0]["dir"];
            var col = columns[idx]["data"];

            var propInfo = typeof(ApplicationUser).GetProperty(col, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (dir == "asc")
                qry = qry.OrderBy(u => propInfo.GetValue(u));
            else
                qry = qry.OrderByDescending(u => propInfo.GetValue(u));

            var result = new
            {
                draw = draw,
                recordsTotal = users.Count(),
                recordsFiltered = qry.Count(),
                data = qry.Select(u => new {
                    Id = u.Id,
                    Email = u.Email,
                    UserName = u.UserName
                }).Skip(start).Take(length).ToArray()
            };

            return Json(result);
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
