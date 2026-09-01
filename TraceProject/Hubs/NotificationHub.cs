using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TraceProject.Hubs;



//  Hub permet de garder la connexion server client overte pour
//  le server puisse envoyer des notifications au client (le react dashboard du frontend)
//ce qui facilite le real time notification ou (le live update)
//sans que le browser n'est a telecharger
//tout le temps la page 

// [Authorize] parce que seul les utilisateurs authentifies avec leur jwt token peuvent
// avoir access au real time notification 

[Authorize]
public class NotificationHub : Hub
    {

    }

