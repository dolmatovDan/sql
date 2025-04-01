using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class TerminateInput
{
    [Required] public String Token { get; set; } = String.Empty;
}


[Route("api/v1/terminate")]
public class TerminateController : Controller
{
    private readonly IHostApplicationLifetime _lifetime;

    public TerminateController(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    [HttpPost]
    public IActionResult Post([FromBody] TerminateInput input)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (input.Token == ServiceContext.Instance.Token)
        {
            _lifetime.StopApplication();
            return Ok();
        }

        return StatusCode(StatusCodes.Status403Forbidden);
    }
}
