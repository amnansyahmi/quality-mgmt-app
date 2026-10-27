using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmaComplaints.Data;
using SmaComplaints.Models;
namespace SmaComplaints.Controllers;
[Authorize]
public sealed class NotificationsController(INotificationStore notifications):Controller
{
 Guid ActorId=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 [HttpGet]public async Task<IActionResult> Feed(){try{return Json(await notifications.FeedAsync(ActorId));}catch(WorkflowException ex){return StatusCode(503,new{error=ex.Message});}}
 [HttpPost]public async Task<IActionResult> Read(Guid? id,DateTimeOffset? through){if(id.HasValue&&await notifications.FindAsync(ActorId,id.Value) is null)return NotFound();if(!id.HasValue&&!through.HasValue)return BadRequest();await notifications.ReadAsync(ActorId,id,through??DateTimeOffset.UtcNow);return Json(new{ok=true});}
 [HttpPost]public async Task<IActionResult> Open(Guid id){var n=await notifications.FindAsync(ActorId,id);if(n is null)return NotFound();await notifications.ReadAsync(ActorId,id,DateTimeOffset.UtcNow);return LocalRedirect(n.Url);}
}
