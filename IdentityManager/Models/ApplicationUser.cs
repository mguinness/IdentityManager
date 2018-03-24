using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace IdentityManager.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public virtual ICollection<IdentityUserRole<string>> Roles { get; set; }
        public virtual ICollection<IdentityUserClaim<string>> Claims { get; set; }
    }
}
