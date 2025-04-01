using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MknImmiSql.Api.V1;

public class ServiceInfo
{
    [Required] public String Timestamp { get; }
    [Required] public Int32 ProcessId { get; }
    [Required] public String TerminationToken { get; set; }

    public ServiceInfo()
    {
        Timestamp = DateTime.Now.ToString("O");
        ProcessId = Environment.ProcessId;
        TerminationToken = ServiceContext.Instance.Token;
    }
}

[Route("/api/v1/info")]
public class InfoController : Controller
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new ServiceInfo());
    }
}
