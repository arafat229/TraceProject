using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TraceProject.Hubs;

// A SignalR Hub is fundamentally different from a Controller: a Controller
// handles one request and responds once, then the connection ends. A Hub
// keeps a persistent WebSocket connection open, so the SERVER can push
// messages to the CLIENT at any time — not just in response to a request.
// This is what makes the dashboard update live without the browser polling.
//
// [Authorize] here means only logged-in users (valid JWT) can connect at
// all — matches accessTokenFactory in the frontend's signalr.js, which
// attaches the JWT when opening the connection.
[Authorize]
public class NotificationsHub : Hub
{
    // Right now this hub has no methods the CLIENT calls — it's purely a
    // broadcast channel. The SERVER pushes events into it from inside your
    // other controllers/services, e.g. from AttendanceService, after an
    // attendance record is created:
    //
    //   await hubContext.Clients.All.SendAsync("EmployeeCheckedIn", new {
    //       employeeName = employee.FullName,
    //       cameraName = camera.Name
    //   });
    //
    // "EmployeeCheckedIn" is the exact event name the frontend listens for
    // via onHubEvent("EmployeeCheckedIn", ...) in src/api/signalr.js — the
    // string must match exactly on both sides, or nothing happens with no
    // error thrown anywhere, which makes a typo here very easy to miss.

    public override async Task OnConnectedAsync()
    {
        // Optional hook — useful later if you want to log who's currently
        // connected, or add users to SignalR "groups" (e.g. per-department)
        // so you can push notifications to a subset of users instead of
        // everyone. Left as a plain pass-through for now.
        await base.OnConnectedAsync();
    }
}
