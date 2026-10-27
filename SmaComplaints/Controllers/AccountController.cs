using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmaComplaints.Data;
using SmaComplaints.Models;
namespace SmaComplaints.Controllers;

public sealed class AccountController(IComplaintStore store,IPasswordHasher<AppUser> hasher,IWebHostEnvironment environment,IConfiguration config) : Controller
{
    [HttpGet] public IActionResult Login(string? returnUrl=null)=>View(new LoginVm{ReturnUrl=returnUrl});
    [HttpPost,EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginVm model)
    {
        if(!ModelState.IsValid)return View(model);
        var user=await store.UserByEmailAsync(model.Email);
        var dummy=new AppUser();var hash=user?.PasswordHash;
        if(string.IsNullOrWhiteSpace(hash))hash=hasher.HashPassword(dummy,"not-a-login-credential");
        var result=hasher.VerifyHashedPassword(user??dummy,hash,model.Password);
        if(user==null||!user.Active||result==PasswordVerificationResult.Failed){ModelState.AddModelError("","Email or password is incorrect.");return View(model);}
        if(result==PasswordVerificationResult.SuccessRehashNeeded){user.PasswordHash=hasher.HashPassword(user,model.Password);await store.SaveUserAsync(user);}
        await SignIn(user);return Url.IsLocalUrl(model.ReturnUrl)?LocalRedirect(model.ReturnUrl!):RedirectToAction("Index","Complaints");
    }
    [HttpPost] public async Task<IActionResult> Demo(string role)
    {
        if(!environment.IsDevelopment()||!config.GetValue<bool>("DemoMode"))return NotFound();
        var u=(await store.UsersAsync()).FirstOrDefault(x=>x.Role==role && x.Active);if(u==null)return BadRequest();
        await SignIn(u);return RedirectToAction("Index","Complaints");
    }
    private async Task SignIn(AppUser u)
    {
        var claims=new[]{new Claim(ClaimTypes.NameIdentifier,u.Id.ToString()),new Claim(ClaimTypes.Name,u.Name),new Claim(ClaimTypes.Email,u.Email),new Claim(ClaimTypes.Role,u.Role),new Claim("password_stamp",u.PasswordHash)};
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));
    }
    [HttpPost] public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync();return RedirectToAction(nameof(Login));}
    public IActionResult Denied(){Response.StatusCode=403;return View();}
}
