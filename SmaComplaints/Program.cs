using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmaComplaints.Data;
using SmaComplaints.Models;
using SmaComplaints.Services;

var builder = WebApplication.CreateBuilder(args);
if(int.TryParse(Environment.GetEnvironmentVariable("PORT"),out var hostPort))builder.WebHost.UseUrls($"http://0.0.0.0:{hostPort}");
var demo = builder.Configuration.GetValue<bool>("DemoMode");
if (demo && !builder.Environment.IsDevelopment()) throw new InvalidOperationException("DemoMode is permitted only in Development. Configure SQL Server for deployment.");
if(!demo&&builder.Configuration.GetValue<bool>("Mail:Enabled"))
{
    foreach(var key in new[]{"Mail:Host","Mail:From","Mail:BaseUrl"})if(string.IsNullOrWhiteSpace(builder.Configuration[key]))throw new InvalidOperationException($"Configure {key} before enabling email notifications.");
    if(!Uri.TryCreate(builder.Configuration["Mail:BaseUrl"],UriKind.Absolute,out var mailUrl)||mailUrl.Scheme!="https")throw new InvalidOperationException("Mail:BaseUrl must be the HTTPS address of the deployed app.");
}
builder.Services.AddControllersWithViews(o=>o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())).AddMvcOptions(o=>o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes=true);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"]??Path.Combine(builder.Environment.ContentRootPath,"App_Data","Keys")));
builder.Services.AddSingleton<Workflow>();
builder.Services.AddSingleton<DocumentFormImporter>();
builder.Services.AddSingleton<FormRuntime>();
if(demo){builder.Services.AddSingleton<DemoNotificationStore>();builder.Services.AddSingleton<INotificationStore>(sp=>sp.GetRequiredService<DemoNotificationStore>());}else builder.Services.AddScoped<INotificationStore,SqlNotificationStore>();
if(demo)builder.Services.AddSingleton<IFormStore,DemoFormStore>();else builder.Services.AddScoped<IFormStore,SqlFormStore>();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<IPasswordHasher<AppUser>,PasswordHasher<AppUser>>();
if(demo) builder.Services.AddSingleton<IComplaintStore,DemoStore>(); else builder.Services.AddScoped<IComplaintStore,SqlComplaintStore>();
if(!demo)builder.Services.AddHostedService<MailWorker>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>
{
    o.LoginPath="/Account/Login";o.AccessDeniedPath="/Account/Denied";o.Cookie.Name="SmaComplaints.Session";
    o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
    o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=true;
    o.Events.OnValidatePrincipal=async context=>
    {
        var store=context.HttpContext.RequestServices.GetRequiredService<IComplaintStore>();
        var id=context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var u=Guid.TryParse(id,out var key)?await store.UserAsync(key):null;
        if(u is null || !u.Active || u.Role!=context.Principal?.FindFirstValue(ClaimTypes.Role) || u.PasswordHash!=context.Principal?.FindFirstValue("password_stamp"))
        {context.RejectPrincipal();await context.HttpContext.SignOutAsync();}
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("login",x=>{x.PermitLimit=10;x.Window=TimeSpan.FromMinutes(1);x.QueueLimit=0;}));
var app=builder.Build();
if(args.Contains("--initialize-database"))
{
    if(demo) throw new InvalidOperationException("Disable DemoMode before initializing SQL Server.");
    await using var conn=new Microsoft.Data.SqlClient.SqlConnection(builder.Configuration.GetConnectionString("Complaints"));await conn.OpenAsync();
    await using var cmd=conn.CreateCommand();cmd.CommandText=await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath,"deployment","001_schema.sql"))+"\n"+await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath,"deployment","002_forms.sql"))+"\n"+await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath,"deployment","003_notifications.sql"));await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("SQL Server schema initialized.");return;
}
if(args.Contains("--bootstrap-admin"))
{
    if(demo)throw new InvalidOperationException("Disable DemoMode before bootstrapping a SQL Server account.");
    var email=Environment.GetEnvironmentVariable("SMA_ADMIN_EMAIL")??throw new InvalidOperationException("Set SMA_ADMIN_EMAIL.");
    var password=Environment.GetEnvironmentVariable("SMA_ADMIN_PASSWORD")??"";
    if(password.Length<12)throw new InvalidOperationException("SMA_ADMIN_PASSWORD must have at least 12 characters.");
    using var scope=app.Services.CreateScope();var store=scope.ServiceProvider.GetRequiredService<IComplaintStore>();
    if((await store.UsersAsync()).Count>0)throw new InvalidOperationException("Bootstrap is available only for an empty user table. Use the admin screen for subsequent accounts.");
    var user=new AppUser{Name=Environment.GetEnvironmentVariable("SMA_ADMIN_NAME")??"Administrator",Email=email,Role=Roles.Admin,Department="Administration"};
    user.PasswordHash=scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>().HashPassword(user,password);await store.SaveUserAsync(user);
    Console.WriteLine("Administrator created. Clear SMA_ADMIN_PASSWORD from your environment.");return;
}
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();app.UseHttpsRedirection();}
app.Use(async(context,next)=>
{
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    context.Response.Headers["X-Frame-Options"]="SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"]="strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";
    if(!context.Request.Path.StartsWithSegments("/css")&&!context.Request.Path.StartsWithSegments("/js"))context.Response.Headers.CacheControl="no-store";
    await next();
});
app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute("default","{controller=Complaints}/{action=Index}/{id?}");
app.MapBlazorHub().RequireAuthorization();
app.Run();
public partial class Program { }
