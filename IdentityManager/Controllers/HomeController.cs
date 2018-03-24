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
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityManager.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _roles;

        public HomeController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ILogger<HomeController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _roles = roleManager.Roles.ToDictionary(r => r.Id, r => r.Name);
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("api/[action]")]
        public IActionResult UserList(int draw, List<Dictionary<string, string>> columns, List<Dictionary<string, string>> order, int start, int length, Dictionary<string, string> search)
        {
            var users = _userManager.Users.Include(u => u.Roles).Include(u => u.Claims);

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
                    LockedOut = u.LockoutEnd != null,
                    Roles = String.Join(",", u.Roles.Select(r => _roles[r.RoleId])),
                    DisplayName = u.Claims.SingleOrDefault(c => c.ClaimType == ClaimTypes.Name).ClaimValue,
                    UserName = u.UserName
                }).Skip(start).Take(length).ToArray()
            };

            return Json(result);
        }

        [HttpPost("api/[action]")]
        public async Task<IActionResult> CreateUser(string email, string password)
        {
            try
            {
                var user = new ApplicationUser() { Email = email, UserName = email };
                
                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Created user {email}.", email);
                    return Accepted();
                }
                else
                    return BadRequest(result.Errors.First().Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure creating user {email}.", email);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("api/[action]")]
        public async Task<ActionResult> DeleteUser(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return NotFound("User not found.");

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Deleted user {email}.", email);
                    return Accepted();
                }
                else
                    return BadRequest(result.Errors.First().Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure deleting user {email}.", email);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
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
