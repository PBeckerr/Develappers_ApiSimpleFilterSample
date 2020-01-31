using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SimpleFilterApi.Core;
using SimpleFilterApi.Database;
using SimpleFilterApi.Models;

namespace SimpleFilterApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PersonController : ControllerBase
    {
        private readonly ApplicationDbContext _applicationDbContext;

        public PersonController(ApplicationDbContext applicationDbContext)
        {
            this._applicationDbContext = applicationDbContext;
        }

        [HttpGet("expression")]
        public ActionResult<IEnumerable<Data.Person>> FilterByExpression([FromQuery] PersonFilter filter)
        {
            return this.Ok(this._applicationDbContext.Persons.FilterByExpr(filter));
        }

        [HttpGet("dynamic")]
        public ActionResult<IEnumerable<Data.Person>> FilterByDynamic([FromQuery] PersonFilter filter)
        {
            return this.Ok(this._applicationDbContext.Persons.FilterByDynamic(filter));
        }
        
        [HttpGet]
        public ActionResult Default()
        {
            return this.Content("Nothing to see here");
        }
    }
}