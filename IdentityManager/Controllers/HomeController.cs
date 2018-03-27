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
        private readonly Dictionary<string, string> _claimTypes;

        public HomeController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ILogger<HomeController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;

            _roles = roleManager.Roles.ToDictionary(r => r.Id, r => r.Name);
            var fldInfo = typeof(ClaimTypes).GetFields(BindingFlags.Static | BindingFlags.Public);
            _claimTypes = fldInfo.ToDictionary(i => i.Name, i => (string)i.GetValue(null));
        }

        public IActionResult Index()
        {
            ViewBag.Roles = _roles;
            ViewBag.ClaimTypes = _claimTypes.Keys.OrderBy(s => s);
            return View();
        }

        public IActionResult Roles()
        {
            return View();
        }

        [HttpGet("api/[action]")]
        public IActionResult UserList(int draw, List<Dictionary<string, string>> columns, List<Dictionary<string, string>> order, int start, int length, Dictionary<string, string> search)
        {
            var users = _userManager.Users.Include(u => u.Roles).Include(u => u.Claims);

            string filter = search["value"];
            var qry = users.Where(u =>
                (String.IsNullOrWhiteSpace(filter) || u.Email.Contains(filter)) ||
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
                    LockedOut = u.LockoutEnd == null ? String.Empty : "Yes",
                    Roles = u.Roles.Select(r => _roles[r.RoleId]),
                    Claims = u.Claims.Select(c => new KeyValuePair<string, string>(_claimTypes.Single(x => x.Value == c.ClaimType).Key, c.ClaimValue)),
                    DisplayName = u.Claims.FirstOrDefault(c => c.ClaimType == ClaimTypes.Name).ClaimValue,
                    UserName = u.UserName
                }).Skip(start).Take(length).ToArray()
            };

            return Json(result);
        }

        [HttpPost("api/[action]")]
        public async Task<IActionResult> CreateUser(string userName, string name, string email, string password)
        {
            try
            {
                var user = new ApplicationUser() { Email = email, UserName = userName };
                
                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Created user {userName}.", userName);

                    if (name != null)
                        await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Name, name));

                    return NoContent();
                }
                else
                    return BadRequest(result.Errors.First().Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure creating user {userName}.", userName);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("api/[action]")]
        public async Task<ActionResult> UpdateUser(string id, string email, string locked, string[] roles, List<KeyValuePair<string, string>> claims)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound("User not found.");

                user.Email = email;
                user.LockoutEnd = locked == null ? default(DateTimeOffset?) : DateTimeOffset.MaxValue;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Updated user {name}.", user.UserName);

                    var userRoles = await _userManager.GetRolesAsync(user);

                    foreach (string role in roles.Except(userRoles))
                        await _userManager.AddToRoleAsync(user, role);

                    foreach (string role in userRoles.Except(roles))
                        await _userManager.RemoveFromRoleAsync(user, role);

                    var userClaims = await _userManager.GetClaimsAsync(user);

                    foreach (var kvp in claims.Where(a => !userClaims.Any(b => _claimTypes[a.Key] == b.Type && a.Value == b.Value)))
                        await _userManager.AddClaimAsync(user, new Claim(_claimTypes[kvp.Key], kvp.Value));

                    foreach (var claim in userClaims.Where(a => !claims.Any(b => a.Type == _claimTypes[b.Key] && a.Value == b.Value)))
                        await _userManager.RemoveClaimAsync(user, claim);

                    return NoContent();
                }
                else
                    return BadRequest(result.Errors.First().Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure updating user {userId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("api/[action]")]
        public async Task<ActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound("User not found.");

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Deleted user {name}.", user.UserName);
                    return NoContent();
                }
                else
                    return BadRequest(result.Errors.First().Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure deleting user {userId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("api/[action]")]
        public IActionResult RoleList(int draw, List<Dictionary<string, string>> columns, List<Dictionary<string, string>> order, int start, int length, Dictionary<string, string> search)
        {
            var roles = _roleManager.Roles;

            string filter = search["value"];
            var qry = roles.Where(r =>
                (String.IsNullOrWhiteSpace(filter) || r.Name.Contains(filter))
            );

            var idx = Int32.Parse(order[0]["column"]);
            var dir = order[0]["dir"];
            var col = columns[idx]["data"];

            var propInfo = typeof(IdentityRole).GetProperty(col, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (dir == "asc")
                qry = qry.OrderBy(r => propInfo.GetValue(r));
            else
                qry = qry.OrderByDescending(r => propInfo.GetValue(r));

            var result = new
            {
                draw = draw,
                recordsTotal = roles.Count(),
                recordsFiltered = qry.Count(),
                data = qry.Select(r => new {
                    Id = r.Id,
                    Name = r.Name
                }).Skip(start).Take(length).ToArray()
            };

            return Json(result);
        }

        [HttpPost("api/[action]")]
        public async Task<IActionResult> CreateRole(string name)
        {
            try
            {
                var role = new IdentityRole(name);

                var result = await _roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Created role {name}.", name);
                    return NoContent();
                }
                else
                    return BadRequest(result.Errors.First().Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure creating role {name}.", name);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("api/[action]")]
        public async Task<ActionResult> DeleteRole(string id)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(id);
                if (role == null)
                    return NotFound("Role not found.");

                var result = await _roleManager.DeleteAsync(role);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Deleted role {name}.", role.Name);
                    return NoContent();
                }
                else
                    return BadRequest(result.Errors.First().Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure deleting role {roleId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
