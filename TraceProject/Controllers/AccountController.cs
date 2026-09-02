using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using TraceProject.Data;
using TraceProject.Dto;
using TraceProject.Helpers;
using TraceProject.Model;

namespace TraceProject.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<AccountController> logger;
    private readonly IConfiguration config;


    public AccountController(ApplicationDbContext dbContext,  ILogger<AccountController> logger, IConfiguration config)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.config = config;
    }


}

 