using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmaComplaints.Data;
using SmaComplaints.Models;
using SmaComplaints.Services;
namespace SmaComplaints.Controllers;

[Authorize(Roles=Roles.Admin)]
public sealed class AdminController(IComplaintStore store,Workflow flow,IPasswordHasher<AppUser> hasher):AppController(store,flow)
{
    public async Task<IActionResult> Index()=>View(await Store.UsersAsync());
    public async Task<IActionResult> Edit(Guid? id)
    {
        if(id==null)return View(new UserEditVm());var u=await Store.UserAsync(id.Value);if(u==null)return NotFound();
        return View(new UserEditVm{Id=u.Id,Name=u.Name,Email=u.Email,Role=u.Role,Department=u.Department,Active=u.Active});
    }
    [HttpPost]
    public async Task<IActionResult> Edit(UserEditVm m)
    {
        if(!Roles.All.Contains(m.Role))ModelState.AddModelError("Role","Select a valid role.");
        var existing=await Store.UserByEmailAsync(m.Email);
        if(existing!=null&&existing.Id!=m.Id)ModelState.AddModelError("Email","This email already has an account.");
        if(m.Id==null&&string.IsNullOrWhiteSpace(m.Password))ModelState.AddModelError("Password","Set a password of at least 12 characters.");
        var actor=await Actor();if(m.Id==actor.Id&&(!m.Active||m.Role!=Roles.Admin))ModelState.AddModelError("","You cannot deactivate or remove your own administrator role.");
        if(!ModelState.IsValid)return View(m);
        var u=m.Id.HasValue?await Store.UserAsync(m.Id.Value):new AppUser();if(u==null)return NotFound();
        u.Name=m.Name;u.Email=m.Email.Trim().ToLowerInvariant();u.Role=m.Role;u.Department=m.Department;u.Active=m.Active;
        if(!string.IsNullOrWhiteSpace(m.Password))u.PasswordHash=hasher.HashPassword(u,m.Password);
        await Store.SaveUserAsync(u);TempData["Success"]="User account saved.";return RedirectToAction(nameof(Index));
    }
}
